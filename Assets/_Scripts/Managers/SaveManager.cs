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
        allMembersToSave.AddRange(GameManager.Instance.activeParty);
        allMembersToSave.AddRange(GameManager.Instance.reserveParty);

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
            foreach (var t in member.traits)
                if (t.data != null) pData.traits.Add(new TraitSaveEntry { traitID = t.data.traitID, level = t.level });

            pData.allocatedTalents = new List<TalentEntry>();
            foreach (var kvp in member.allocatedTalents)
                pData.allocatedTalents.Add(new TalentEntry(kvp.Key, kvp.Value));

            pData.equipment = new List<EquipmentEntry>();
            foreach (var kvp in member.equipment)
                // 👇 修复点 1：读取肉身内部的蓝图名字
                if (kvp.Value != null) pData.equipment.Add(new EquipmentEntry(kvp.Key, kvp.Value.blueprint.name));

            data.roster.Add(pData);
            data.activePartyIDs.Add(member.data.characterID); 
        }

        for (int i = 0; i < 6; i++)
        {
            var m = GameManager.Instance.activeFormation[i];
            data.activePartyIDs.Add(m != null ? m.data.characterID : ""); 
        }

        if (GameManager.Instance.currentLocation != null) data.locationID = GameManager.Instance.currentLocation.name;
        data.eventMemory = new List<string>(GameManager.Instance.eventMemory);

        data.inventory = new List<InventorySaveData>();
        if (InventoryManager.Instance != null)
        {
            foreach (var slot in InventoryManager.Instance.inventory)
                if (slot.itemData != null)
                    data.inventory.Add(new InventorySaveData { itemID = slot.itemData.name, amount = slot.amount });
        }

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
                        EquipmentData equip = Resources.Load<EquipmentData>($"Items/{entry.itemID}");
                        // 👇 修复点 2：将图纸重新捏造成肉身实例 (暂时赋予普通品质)
                        if (equip != null) rc.equipment.Add(entry.slot, new RuntimeEquipment(equip, EquipmentRarity.Common));
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
                ItemData item = Resources.Load<ItemData>($"Items/{invData.itemID}");
                if (item != null) InventoryManager.Instance.AddItem(item, invData.amount);
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

        Debug.Log($"[Load] 读档完成。");
        if(UIManager.Instance != null) UIManager.Instance.RefreshPlayerStatus();
        if(UI_SaveMenu.Instance != null) UI_SaveMenu.Instance.CloseMenu();
        if (UIManager.Instance != null) UIManager.Instance.RefreshPlayerStatus();
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
}