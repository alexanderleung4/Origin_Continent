using System.Collections.Generic;
using UnityEngine;

// ===================================================================================
// 1. 根存档文件 (The Root)
// ===================================================================================
[System.Serializable]
public class SaveData
{
    // --- 存档元数据 (Meta Info) ---
    public string saveName;      // 存档显示的标题 (e.g. "Lv.5 Warrior - 2026/02/07")
    public string timestamp;     // 存档时间 (用于排序)
    public string version = "0.9"; // 版本号 (用于未来兼容性检查) [cite: 133]

    // --- 世界状态 (World State) ---
    public string locationID;    // 当前场景文件名 (e.g. "Loc_Home") [cite: 57]
    public int day;              // 第几天 [cite: 58]
    public int time;             // 分钟数 [cite: 59]
    
    // --- 玩家状态 (Player) ---
    public PlayerSaveData player; 

    // --- 记忆系统 (Memory) ---
    // 记录所有发生过的一次性事件 (e.g. "Quest_01_Done", "Boss_Killed")
    public List<string> eventMemory; 

    // --- 任务系统 (Quests) ---
    public List<QuestSaveData> activeQuests; 

    // --- 背包系统 (Inventory) ---
    public List<InventorySaveData> inventory; 
}

// ===================================================================================
// 2. 子数据结构 (Sub-Structures)
// ===================================================================================

[System.Serializable]
public struct PlayerSaveData
{
    public int level;
    public int currentExp;
    public int hp;
    public int mp;
    public int stamina;
    public int gold;
    public int talentPoints;

    // ⚠️ 难点 B: 字典无法直接序列化，拆分成 List<Struct> [cite: 76, 95]
    // 对应 RuntimeCharacter.allocatedTalents
    public List<TalentEntry> allocatedTalents; 
    
    // 对应 RuntimeCharacter.equipment
    public List<EquipmentEntry> equipment; 

    public List<TraitSaveEntry> traits;
}
[System.Serializable]
public struct TraitSaveEntry
{
    public string traitID;
    public int level;
}

[System.Serializable]
public struct QuestSaveData
{
    public string questID;           // 任务文件名 (ID) [cite: 83]
    public bool isCompleted;         // 是否已达成目标但未提交 [cite: 84]
    
    // ⚠️ 难点 C: 任务进度的保存 [cite: 99]
    // 对应 QuestData.objectives 的 currentAmount
    // 例如: objectives[0] 是杀怪，这里存 3; objectives[1] 是收集，这里存 1
    public List<int> objectivesProgress; 
}

[System.Serializable]
public struct InventorySaveData
{
    public string itemID; // 物品文件名 (e.g. "Item_Potion") [cite: 45]
    public int amount;    // 数量
}

// ===================================================================================
// 3. 辅助转换结构 (Helpers for Dictionaries)
// ===================================================================================

// 用于解决 Dictionary<StatType, int> 序列化问题
[System.Serializable]
public struct TalentEntry
{
    public StatType statType;
    public int points;

    public TalentEntry(StatType type, int val) { statType = type; points = val; }
}

// 用于解决 Dictionary<EquipmentSlot, EquipmentData> 序列化问题
[System.Serializable]
public struct EquipmentEntry
{
    public EquipmentSlot slot;
    public string itemID; // 存文件名

    public EquipmentEntry(EquipmentSlot s, string id) { slot = s; itemID = id; }
}