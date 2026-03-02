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

    // --- 信箱 (Messenger) ---
    // -2: New Game, -1: Auto Save, 0+: Manual Save, -999: None
    public static int AutoLoadSlot = -999; 
    // 新增：记录当前正在运行的存档槽位 (防遗忘)
    public int currentSaveID = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ✅ 新增: 监听场景加载事件
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // 每次切换场景完成时，Unity 都会自动调用这个方法
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 1. 重新绑定跨天事件 (因为 TimeManager 可能被销毁重建了)
        if (TimeManager.Instance != null)
        {
            // 先移除防止重复订阅
            TimeManager.Instance.OnDayChanged.RemoveListener(OnDayChangedAutoSave);
            TimeManager.Instance.OnDayChanged.AddListener(OnDayChangedAutoSave);
        }

        // 2. 检查信箱，决定是否读档
        // 只有当信箱里有信 (不是 -999) 时才处理
        if (AutoLoadSlot != -999)
        {
            StartCoroutine(ProcessAutoLoad());
        }
    }

    private void OnDestroy()
    {
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnDayChanged.RemoveListener(OnDayChangedAutoSave);
    }

    // --- 启动逻辑 ---
    private IEnumerator ProcessAutoLoad()
    {
        yield return null; // 等待 GameManager 初始化 Player

        if (AutoLoadSlot == -2)
        {
            Debug.Log("[SaveManager] >>> 开始新游戏 (New Game)");
            GameManager.Instance.StartNewGame();
        }
        else if (AutoLoadSlot != -999)
        {
            Debug.Log($"[SaveManager] >>> 读取存档 (Load Slot: {AutoLoadSlot})");
            LoadGame(AutoLoadSlot);
        }

        AutoLoadSlot = -999; // 重置信箱
    }

    private void OnDayChangedAutoSave(int day)
    {
        Debug.Log($"[AutoSave] Day {day} - 自动存档中...");
        SaveGame(-1); 
    }

    // ===================================================================================
    // 核心功能 (IO Operations)
    // ===================================================================================

    public void SaveGame(int saveID)
    {
        currentSaveID = saveID; // 👇 新增：存哪个档，当前进程就绑定哪个档
        SaveData data = new SaveData();
        
        // 1. Header
        data.saveName = (saveID == -1) ? "自动存档 (Auto)" : $"存档 {saveID + 1}";
        data.timestamp = System.DateTime.Now.ToString("yyyy/MM/dd HH:mm");

        // ==========================================
        // 2. 小队与名册存储 (Party & Roster)
        // ==========================================
        data.roster = new List<PlayerSaveData>();
        data.activePartyIDs = new List<string>();
        // 建立一个全员大名单 (首发 + 替补)
        List<RuntimeCharacter> allMembersToSave = new List<RuntimeCharacter>();
        allMembersToSave.AddRange(GameManager.Instance.activeParty);
        allMembersToSave.AddRange(GameManager.Instance.reserveParty);

        // 遍历当前出战队伍
        foreach (var member in allMembersToSave)
        {
            if (member == null || member.data == null) continue;
            
            PlayerSaveData pData = new PlayerSaveData();
            pData.characterID = member.data.characterID; // 极其关键！
            pData.level = member.Level;
            pData.currentExp = member.CurrentExp;
            pData.hp = member.CurrentHP;
            pData.mp = member.CurrentMP;
            pData.stamina = member.CurrentStamina;
            pData.gold = member.Gold; // 全队共享队长的钱包
            pData.talentPoints = member.TalentPoints;

            // 存特质
            pData.traits = new List<TraitSaveEntry>();
            foreach (var t in member.traits)
                if (t.data != null) pData.traits.Add(new TraitSaveEntry { traitID = t.data.traitID, level = t.level });

            // 存天赋
            pData.allocatedTalents = new List<TalentEntry>();
            foreach (var kvp in member.allocatedTalents)
                pData.allocatedTalents.Add(new TalentEntry(kvp.Key, kvp.Value));

            // 存装备
            pData.equipment = new List<EquipmentEntry>();
            foreach (var kvp in member.equipment)
                if (kvp.Value != null) pData.equipment.Add(new EquipmentEntry(kvp.Key, kvp.Value.name));

            data.roster.Add(pData);
            data.activePartyIDs.Add(member.data.characterID); // 记录站位顺序
        }
        // 在 foreach 结束后，精确记录 6 宫格的站位密码！
        for (int i = 0; i < 6; i++)
        {
            var m = GameManager.Instance.activeFormation[i];
            data.activePartyIDs.Add(m != null ? m.data.characterID : ""); // 空位存为空字符串
        }

        // 3. World
        if (GameManager.Instance.currentLocation != null)
            data.locationID = GameManager.Instance.currentLocation.name;
        data.eventMemory = new List<string>(GameManager.Instance.eventMemory);

        // 4. Inventory
        data.inventory = new List<InventorySaveData>();
        if (InventoryManager.Instance != null)
        {
            foreach (var slot in InventoryManager.Instance.inventory)
                if (slot.itemData != null)
                    data.inventory.Add(new InventorySaveData { itemID = slot.itemData.name, amount = slot.amount });
        }

        // 5. Quests
        data.activeQuests = new List<QuestSaveData>();
        if (QuestManager.Instance != null)
        {
            foreach (var q in QuestManager.Instance.activeQuests)
            {
                QuestSaveData qData = new QuestSaveData();
                qData.questID = q.name;
                qData.isCompleted = q.isCompleted;
                qData.objectivesProgress = new List<int>();
                foreach(var obj in q.objectives) qData.objectivesProgress.Add(obj.currentAmount);
                data.activeQuests.Add(qData);
            }
        }

        // Write to Disk
        string path = GetPath(saveID);
        File.WriteAllText(path, JsonUtility.ToJson(data, true));
        
        Debug.Log($"[Save] 成功写入: {path}");
        if (UI_SaveMenu.Instance != null) UI_SaveMenu.Instance.RefreshList();
    }

    public void LoadGame(int saveID)
    {
        currentSaveID = saveID; // 👇 新增：记住当前读的是哪个档
        string path = GetPath(saveID);
        if (!File.Exists(path)) { Debug.LogWarning($"[Load] 文件不存在: {path}"); return; }

        SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));

        // ==========================================
        // 1. 读取小队与名册 (Party & Roster)
        // ==========================================
        GameManager.Instance.activeParty.Clear();
        GameManager.Instance.unlockedCharacters.Clear();

        // 建立一个临时字典，用来装捏好的肉身，方便稍后按顺序排队
        Dictionary<string, RuntimeCharacter> loadedCharacters = new Dictionary<string, RuntimeCharacter>();

        if (data.roster != null)
        {
            foreach (var pData in data.roster)
            {
                // ⚠️ 核心寻址: 根据 ID 去 Resources 文件夹里把静态配置表抓出来！
                CharacterData cData = Resources.Load<CharacterData>($"Characters/{pData.characterID}");
                if (cData == null) 
                {
                    Debug.LogError($"[SaveManager] 读档警告：找不到角色配置表 Resources/Characters/{pData.characterID}，该队友丢失！");
                    continue;
                }

                // 捏造肉身
                RuntimeCharacter rc = new RuntimeCharacter(cData);
                rc.Level = pData.level;
                rc.CurrentExp = pData.currentExp;
                rc.CurrentHP = pData.hp;
                rc.CurrentMP = pData.mp;
                rc.CurrentStamina = pData.stamina;
                rc.Gold = pData.gold;
                rc.TalentPoints = pData.talentPoints;

                // 读特质
                rc.traits.Clear();
                if (pData.traits != null)
                {
                    foreach (var tSave in pData.traits)
                    {
                        TraitData loadedTrait = Resources.Load<TraitData>($"Traits/{tSave.traitID}");
                        // 兼容跨天流逝：如果是永久特质， remainingDays 设为 -1
                        if (loadedTrait != null) rc.traits.Add(new RuntimeCharacter.ActiveTrait { data = loadedTrait, level = tSave.level, remainingDays = loadedTrait.isPermanent ? -1 : loadedTrait.durationDays });
                    }
                }

                // 读天赋
                rc.allocatedTalents.Clear();
                if (pData.allocatedTalents != null)
                    foreach (var entry in pData.allocatedTalents) rc.allocatedTalents.Add(entry.statType, entry.points);

                // 读装备
                rc.equipment.Clear();
                if (pData.equipment != null)
                {
                    foreach (var entry in pData.equipment)
                    {
                        EquipmentData equip = Resources.Load<EquipmentData>($"Items/{entry.itemID}");
                        if (equip != null) rc.equipment.Add(entry.slot, equip);
                    }
                }

                loadedCharacters[pData.characterID] = rc;
                GameManager.Instance.unlockedCharacters.Add(cData); // 登记进名册
            }
        }

        // 按存档里的 6 宫格密码，把角色精准插回沙盘！
        GameManager.Instance.activeFormation = new RuntimeCharacter[6];
        if (data.activePartyIDs != null)
        {
            for (int i = 0; i < 6 && i < data.activePartyIDs.Count; i++)
            {
                string id = data.activePartyIDs[i];
                if (!string.IsNullOrEmpty(id) && loadedCharacters.ContainsKey(id))
                {
                    GameManager.Instance.activeFormation[i] = loadedCharacters[id];
                    loadedCharacters.Remove(id); // 放上场的，从临时字典里踢掉
                }
            }
        }

        // 剩下没上场的，统统塞进冷板凳！
        GameManager.Instance.reserveParty.Clear();
        foreach (var remaining in loadedCharacters.Values)
        {
            GameManager.Instance.reserveParty.Add(remaining);
        }

        // 2. World
        GameManager.Instance.eventMemory.Clear();
        foreach(var evt in data.eventMemory) GameManager.Instance.eventMemory.Add(evt);
        
        if (!string.IsNullOrEmpty(data.locationID))
        {
            LocationData loc = Resources.Load<LocationData>($"Locations/{data.locationID}");
            if (loc != null) GameManager.Instance.GoToLocation(loc);
        }

        // 3. Inventory
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.inventory.Clear();
            foreach(var invData in data.inventory)
            {
                ItemData item = Resources.Load<ItemData>($"Items/{invData.itemID}");
                if (item != null) InventoryManager.Instance.AddItem(item, invData.amount);
            }
        }

        // 4. Quests
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

        Debug.Log($"[Load] 读档完成。");
        if(UIManager.Instance != null) UIManager.Instance.RefreshPlayerStatus();
        if(UI_SaveMenu.Instance != null) UI_SaveMenu.Instance.CloseMenu();

        // 👇 新增: 数据全部加载完毕后，强制刷新一次 UI
        if (UIManager.Instance != null)
        {
            UIManager.Instance.RefreshPlayerStatus();
        }

        Debug.Log($"[SaveManager] 存档 {saveID} 读取完毕，UI 已刷新。");
    }

    public void DeleteSave(int saveID)
    {
        string path = GetPath(saveID);
        if (File.Exists(path))
        {
            File.Delete(path);
            if (UI_SaveMenu.Instance != null) UI_SaveMenu.Instance.RefreshList();
        }
    }

    // --- 静态辅助方法 (供 UI_TitleScreen 使用) ---
    
    // 检查某个存档是否存在 (无需实例化)
    public static bool CheckSaveExists(int saveID)
    {
        string fileName = (saveID == -1) ? "save_auto.json" : $"save_{saveID}.json";
        return File.Exists(Path.Combine(Application.persistentDataPath, fileName));
    }

    // 获取最新的存档 ID (用于 Continue)
    public static int GetLatestSaveID()
    {
        int latestID = -999;
        System.DateTime latestTime = System.DateTime.MinValue;

        // 查 Auto
        string autoPath = Path.Combine(Application.persistentDataPath, "save_auto.json");
        if (File.Exists(autoPath))
        {
            latestID = -1;
            latestTime = File.GetLastWriteTime(autoPath);
        }

        // 查 Save 0-9
        for (int i = 0; i < 10; i++)
        {
            string path = Path.Combine(Application.persistentDataPath, $"save_{i}.json");
            if (File.Exists(path))
            {
                System.DateTime t = File.GetLastWriteTime(path);
                if (t > latestTime)
                {
                    latestTime = t;
                    latestID = i;
                }
            }
        }
        return latestID;
    }

    // --- 内部辅助 ---
    private string GetPath(int saveID)
    {
        string fileName = (saveID == -1) ? AUTO_SAVE_FILE + EXTENSION : SAVE_FILE_PREFIX + saveID + EXTENSION;
        return Path.Combine(Application.persistentDataPath, fileName);
    }
    
    // 获取信息用于 UI 显示
    public SaveData GetSaveInfo(int saveID)
    {
        string path = GetPath(saveID);
        if (!File.Exists(path)) return null;
        try { return JsonUtility.FromJson<SaveData>(File.ReadAllText(path)); }
        catch { return null; }
    }
    // --- 👇 新增/修改: 静态版本的获取信息 (UI专用) ---
    // 这样主菜单不需要实例化 SaveManager 也能读到存档信息
    public static SaveData GetSaveInfoStatic(int saveID)
    {
        string fileName = (saveID == -1) ? "save_auto.json" : $"save_{saveID}.json";
        string path = Path.Combine(Application.persistentDataPath, fileName);
        
        if (!File.Exists(path)) return null;
        try 
        {
            string json = File.ReadAllText(path);
            return JsonUtility.FromJson<SaveData>(json);
        }
        catch { return null; }
    }

    [ContextMenu("Open Folder")] public void OpenSaveFolder() => Application.OpenURL(Application.persistentDataPath);
}