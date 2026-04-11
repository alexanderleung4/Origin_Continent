using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.SceneManagement;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    // --- 文件配置 ---
    private const string SAVE_FILE_PREFIX = "save_"; 
    private const string AUTO_SAVE_FILE = "save_auto";
    private const string EXTENSION = ".json";

    public static int AutoLoadSlot = -999; 
    public int currentSaveID = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
    private void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnDayChanged.RemoveListener(OnDayChangedAutoSave);
            TimeManager.Instance.OnDayChanged.AddListener(OnDayChangedAutoSave);
        }

        if (AutoLoadSlot != -999) StartCoroutine(ProcessAutoLoad());
    }

    private void OnDestroy()
    {
        if (TimeManager.Instance != null) TimeManager.Instance.OnDayChanged.RemoveListener(OnDayChangedAutoSave);
    }

    private IEnumerator ProcessAutoLoad()
    {
        yield return null; 
        if (AutoLoadSlot == -2) { Debug.Log("[SaveManager] >>> 开始新游戏 (New Game)"); GameManager.Instance.StartNewGame(); }
        else if (AutoLoadSlot != -999) { Debug.Log($"[SaveManager] >>> 读取存档 (Load Slot: {AutoLoadSlot})"); LoadGame(AutoLoadSlot); }
        AutoLoadSlot = -999; 
    }

    private void OnDayChangedAutoSave(int day) { Debug.Log($"[AutoSave] Day {day} - 自动存档中..."); SaveGame(-1); }

    public void SaveGame(int saveID)
    {
        currentSaveID = saveID; 
        SaveData data = new SaveData();
        
        data.saveName = (saveID == -1) ? "自动存档 (Auto)" : $"存档 {saveID + 1}";
        data.timestamp = System.DateTime.Now.ToString("yyyy/MM/dd HH:mm");

        data.roster = new List<PlayerSaveData>();
        data.activePartyIDs = new List<string>();
        List<RuntimeCharacter> allMembersToSave = new List<RuntimeCharacter>();
        
        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.activeParty != null) allMembersToSave.AddRange(GameManager.Instance.activeParty);
            if (GameManager.Instance.reserveParty != null) allMembersToSave.AddRange(GameManager.Instance.reserveParty);
        }

        // 1. 角色名册保存 (极严防爆)
        foreach (var member in allMembersToSave)
        {
            if (member == null || member.data == null) continue;
            
            PlayerSaveData pData = new PlayerSaveData();
            pData.characterID = member.data.characterID; 
            pData.level = member.Level;
            pData.currentExp = member.CurrentExp;
            pData.hp = member.CurrentHP;
            pData.mp = member.CurrentMP;
            pData.stamina = member.CurrentStamina;
            pData.gold = member.Gold; 
            pData.talentPoints = member.TalentPoints;

            pData.traits = new List<TraitSaveEntry>();
            if (member.traits != null)
            {
                foreach (var t in member.traits)
                    if (t != null && t.data != null) pData.traits.Add(new TraitSaveEntry { traitID = t.data.traitID, level = t.level });
            }

            pData.allocatedTalents = new List<TalentEntry>();
            if (member.allocatedTalents != null)
            {
                foreach (var kvp in member.allocatedTalents)
                    pData.allocatedTalents.Add(new TalentEntry(kvp.Key, kvp.Value));
            }

            pData.equipment = new List<EquipmentEntry>();
            if (member.equipment != null)
            {
                foreach (var kvp in member.equipment)
                {
                    // 🛡️ 绝对防爆：肉身和图纸缺一不可，否则丢弃不存
                    if (kvp.Value != null && kvp.Value.blueprint != null)
                    {
                        EquipmentEntry newEntry = new EquipmentEntry(kvp.Key, kvp.Value.blueprint.name);
                        newEntry.equipData = SerializeEquipment(kvp.Value);
                        pData.equipment.Add(newEntry);
                    }
                }
            }

            data.roster.Add(pData);
        }

        // 2. 阵型保存 (极严防爆)
        if (GameManager.Instance != null && GameManager.Instance.activeFormation != null)
        {
            for (int i = 0; i < 6; i++)
            {
                var m = GameManager.Instance.activeFormation[i];
                // 🛡️ 绝对防爆：如果位置有人，但没灵魂(data为null)，强行记为空位
                data.activePartyIDs.Add((m != null && m.data != null) ? m.data.characterID : ""); 
            }
        }

        if (GameManager.Instance != null && GameManager.Instance.currentLocation != null) 
            data.locationID = GameManager.Instance.currentLocation.name;
            
        data.eventMemory = new List<string>();
        if (GameManager.Instance != null && GameManager.Instance.eventMemory != null)
            data.eventMemory = new List<string>(GameManager.Instance.eventMemory);

        // 3. 背包保存 (极严防爆)
        data.inventory = new List<InventorySaveData>();
        if (InventoryManager.Instance != null && InventoryManager.Instance.inventory != null)
        {
            foreach (var slot in InventoryManager.Instance.inventory)
            {
                if (slot == null) continue;

                if (slot.equipmentInstance != null && slot.equipmentInstance.blueprint != null)
                {
                    InventorySaveData invData = new InventorySaveData { itemID = slot.equipmentInstance.blueprint.name, amount = slot.amount };
                    invData.equipData = SerializeEquipment(slot.equipmentInstance);
                    data.inventory.Add(invData);
                }
                else if (slot.itemData != null)
                {
                    data.inventory.Add(new InventorySaveData { itemID = slot.itemData.name, amount = slot.amount });
                }
            }
        }

        // 4. 任务保存 (极严防爆)
        data.activeQuests = new List<QuestSaveData>();
        if (QuestManager.Instance != null && QuestManager.Instance.activeQuests != null)
        {
            foreach (var q in QuestManager.Instance.activeQuests)
            {
                if (q == null) continue; // 防止列表里有空元素
                QuestSaveData qData = new QuestSaveData();
                qData.questID = q.name;
                qData.isCompleted = q.isCompleted;
                qData.objectivesProgress = new List<int>();
                if (q.objectives != null)
                {
                    foreach(var obj in q.objectives) qData.objectivesProgress.Add(obj.currentAmount);
                }
                data.activeQuests.Add(qData);
            }
        }
        
        // 5. 商店记忆保存
        if (ShopManager.Instance != null)
            data.shopStates = ShopManager.Instance.GetStockStateForSave();
        else
            data.shopStates = new List<ShopSaveData>();

        // 👇 --- 核心新增 6：羁绊与互动精力记忆保存 ---
        if (AffinityManager.Instance != null)
        {
            AffinityManager.Instance.SaveTo(data);
        }

        // 写入硬盘
        string path = GetPath(saveID);
        File.WriteAllText(path, JsonUtility.ToJson(data, true));
        Debug.Log($"[Save] 成功写入: {path}");
        if (UI_SaveMenu.Instance != null) UI_SaveMenu.Instance.RefreshList();
    }

    public void LoadGame(int saveID)
    {
        currentSaveID = saveID; 
        string path = GetPath(saveID);
        if (!File.Exists(path)) { Debug.LogWarning($"[Load] 文件不存在: {path}"); return; }

        SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));

        GameManager.Instance.activeParty.Clear();
        GameManager.Instance.unlockedCharacters.Clear();

        Dictionary<string, RuntimeCharacter> loadedCharacters = new Dictionary<string, RuntimeCharacter>();

        if (data.roster != null)
        {
            foreach (var pData in data.roster)
            {
                CharacterData cData = Resources.Load<CharacterData>($"Characters/{pData.characterID}");
                if (cData == null) continue;

                RuntimeCharacter rc = new RuntimeCharacter(cData);
                rc.Level = pData.level;
                rc.CurrentExp = pData.currentExp;
                rc.CurrentHP = pData.hp;
                rc.CurrentMP = pData.mp;
                rc.CurrentStamina = pData.stamina;
                rc.Gold = pData.gold;
                rc.TalentPoints = pData.talentPoints;

                rc.traits.Clear();
                if (pData.traits != null)
                {
                    foreach (var tSave in pData.traits)
                    {
                        TraitData loadedTrait = Resources.Load<TraitData>($"Traits/{tSave.traitID}");
                        if (loadedTrait != null) rc.traits.Add(new RuntimeCharacter.ActiveTrait { data = loadedTrait, level = tSave.level, remainingDays = loadedTrait.isPermanent ? -1 : loadedTrait.durationDays });
                    }
                }

                rc.allocatedTalents.Clear();
                if (pData.allocatedTalents != null)
                    foreach (var entry in pData.allocatedTalents) rc.allocatedTalents.Add(entry.statType, entry.points);

                rc.equipment.Clear();
                if (pData.equipment != null)
                {
                    foreach (var entry in pData.equipment)
                    {
                        // 👇 核心修复 3：精准还原身上的肉身，兼容老版本旧档
                        if (entry.equipData != null && !string.IsNullOrEmpty(entry.equipData.uid))
                        {
                            RuntimeEquipment restoredEquip = DeserializeEquipment(entry.equipData);
                            if (restoredEquip != null) rc.equipment.Add(entry.slot, restoredEquip);
                        }
                        else
                        {
                            // 兼容以前只存了 itemID 的老存档
                            EquipmentData equip = Resources.Load<EquipmentData>($"Items/{entry.itemID}");
                            if (equip != null) rc.equipment.Add(entry.slot, new RuntimeEquipment(equip, EquipmentRarity.Common));
                        }
                    }
                }

                loadedCharacters[pData.characterID] = rc;
                GameManager.Instance.unlockedCharacters.Add(cData); 
            }
        }

        GameManager.Instance.activeFormation = new RuntimeCharacter[6];
        if (data.activePartyIDs != null)
        {
            for (int i = 0; i < 6 && i < data.activePartyIDs.Count; i++)
            {
                string id = data.activePartyIDs[i];
                if (!string.IsNullOrEmpty(id) && loadedCharacters.ContainsKey(id))
                {
                    GameManager.Instance.activeFormation[i] = loadedCharacters[id];
                    loadedCharacters.Remove(id); 
                }
            }
        }

        GameManager.Instance.reserveParty.Clear();
        foreach (var remaining in loadedCharacters.Values) GameManager.Instance.reserveParty.Add(remaining);

        GameManager.Instance.eventMemory.Clear();
        foreach(var evt in data.eventMemory) GameManager.Instance.eventMemory.Add(evt);
        
        if (!string.IsNullOrEmpty(data.locationID))
        {
            LocationData loc = Resources.Load<LocationData>($"Locations/{data.locationID}");
            if (loc != null) GameManager.Instance.GoToLocation(loc);
        }

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.inventory.Clear();
            foreach(var invData in data.inventory)
            {
                // 👇 核心修复 4：还原背包内的实体装备
                if (invData.equipData != null && !string.IsNullOrEmpty(invData.equipData.uid))
                {
                    RuntimeEquipment restoredEquip = DeserializeEquipment(invData.equipData);
                    if (restoredEquip != null) 
                    {
                        // 这里调用专属于实体的 AddItem (带有 isSilent=true 防止读档疯狂弹窗)
                        InventoryManager.Instance.AddItem(restoredEquip, 1, true); 
                    }
                }
                else
                {
                    ItemData item = Resources.Load<ItemData>($"Items/{invData.itemID}");
                    if (item != null) InventoryManager.Instance.AddItem(item, invData.amount);
                }
            }
        }

        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.activeQuests.Clear();
            foreach (var qSave in data.activeQuests)
            {
                QuestData q = Resources.Load<QuestData>($"Quests/{qSave.questID}");
                if (q != null)
                {
                    q.isAccepted = true; q.isSubmitted = false; q.isCompleted = qSave.isCompleted;
                    if (qSave.objectivesProgress != null && q.objectives != null)
                    {
                        for (int i = 0; i < q.objectives.Count; i++)
                            if (i < qSave.objectivesProgress.Count) q.objectives[i].currentAmount = qSave.objectivesProgress[i];
                    }
                    QuestManager.Instance.activeQuests.Add(q);
                }
            }
        }
        if (ShopManager.Instance != null && data.shopStates != null)
        {
            ShopManager.Instance.RestoreStockState(data.shopStates);
        }
        // 👇 --- 核心新增 6：羁绊与互动精力记忆读取 ---
        if (AffinityManager.Instance != null)
        {
            AffinityManager.Instance.LoadFrom(data);
        }

        Debug.Log($"[Load] 读档完成。");
        if(UIManager.Instance != null) UIManager.Instance.RefreshPlayerStatus();
        if(UI_SaveMenu.Instance != null) UI_SaveMenu.Instance.CloseMenu();
        Debug.Log($"[SaveManager] 存档 {saveID} 读取完毕，UI 已刷新。");
    }

    public void DeleteSave(int saveID)
    {
        string path = GetPath(saveID);
        if (File.Exists(path)) { File.Delete(path); if (UI_SaveMenu.Instance != null) UI_SaveMenu.Instance.RefreshList(); }
    }

    public static bool CheckSaveExists(int saveID)
    {
        string fileName = (saveID == -1) ? "save_auto.json" : $"save_{saveID}.json";
        return File.Exists(Path.Combine(Application.persistentDataPath, fileName));
    }

    public static int GetLatestSaveID()
    {
        int latestID = -999;
        System.DateTime latestTime = System.DateTime.MinValue;

        string autoPath = Path.Combine(Application.persistentDataPath, "save_auto.json");
        if (File.Exists(autoPath)) { latestID = -1; latestTime = File.GetLastWriteTime(autoPath); }

        for (int i = 0; i < 10; i++)
        {
            string path = Path.Combine(Application.persistentDataPath, $"save_{i}.json");
            if (File.Exists(path))
            {
                System.DateTime t = File.GetLastWriteTime(path);
                if (t > latestTime) { latestTime = t; latestID = i; }
            }
        }
        return latestID;
    }

    private string GetPath(int saveID)
    {
        string fileName = (saveID == -1) ? AUTO_SAVE_FILE + EXTENSION : SAVE_FILE_PREFIX + saveID + EXTENSION;
        return Path.Combine(Application.persistentDataPath, fileName);
    }
    
    public SaveData GetSaveInfo(int saveID)
    {
        string path = GetPath(saveID);
        if (!File.Exists(path)) return null;
        try { return JsonUtility.FromJson<SaveData>(File.ReadAllText(path)); }
        catch { return null; }
    }

    public static SaveData GetSaveInfoStatic(int saveID)
    {
        string fileName = (saveID == -1) ? "save_auto.json" : $"save_{saveID}.json";
        string path = Path.Combine(Application.persistentDataPath, fileName);
        if (!File.Exists(path)) return null;
        try { return JsonUtility.FromJson<SaveData>(File.ReadAllText(path)); }
        catch { return null; }
    }

    [ContextMenu("Open Folder")] public void OpenSaveFolder() => Application.OpenURL(Application.persistentDataPath);

    // ==========================================
    // 👇 新增：装备肉身转换工具组
    // ==========================================
    private RuntimeEquipmentSaveData SerializeEquipment(RuntimeEquipment equip)
    {
        // 🛡️ 防爆盾 1：肉身或灵魂(蓝图)丢失，拒绝序列化
        if (equip == null || equip.blueprint == null) return null; 

        return new RuntimeEquipmentSaveData
        {
            uid = equip.uid,
            blueprintID = equip.blueprint.name,
            level = equip.level,
            currentExp = equip.currentExp,
            rarity = (int)equip.rarity,
            currentDurability = equip.currentDurability,
            // 🛡️ 防爆盾 2：如果旧存档或测试代码导致 affixes 为空，安全地赋予一个空列表，防止 new List(null) 崩溃
            affixes = equip.affixes != null ? new List<ItemAffix>(equip.affixes) : new List<ItemAffix>() 
        };
    }

    private RuntimeEquipment DeserializeEquipment(RuntimeEquipmentSaveData save)
    {
        EquipmentData blueprint = Resources.Load<EquipmentData>($"Items/{save.blueprintID}");
        if (blueprint == null) return null;

        // 根据读取的品质生成肉身
        RuntimeEquipment equip = new RuntimeEquipment(blueprint, (EquipmentRarity)save.rarity);
        
        // 覆盖还原具体数值
        equip.uid = save.uid;
        equip.level = save.level;
        equip.currentExp = save.currentExp;
        equip.currentDurability = save.currentDurability;
        equip.affixes = save.affixes != null ? new List<ItemAffix>(save.affixes) : new List<ItemAffix>();
        
        // 强制重算乘区 (恢复原本在那个等级应有的白值)
        equip.CalculateDynamicStats(); 
        
        return equip;
    }
}