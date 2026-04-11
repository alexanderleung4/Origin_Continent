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
    
    // 单体变小队 (Party & Roster)
    // ==========================================
    public List<PlayerSaveData> roster;       // 名册：所有已解锁的队友数据
    public List<string> activePartyIDs;       // 排序：当前上阵队员的 characterID 列表

    // --- 记忆系统 (Memory) ---
    // 记录所有发生过的一次性事件 (e.g. "Quest_01_Done", "Boss_Killed")
    public List<string> eventMemory; 

    // --- 任务系统 (Quests) ---
    public List<QuestSaveData> activeQuests; 

    // --- 背包系统 (Inventory) ---
    public List<InventorySaveData> inventory; 

    // 商店库存记忆 (Shop Stocks)
    public List<ShopSaveData> shopStates;

    // 三维度羁绊记忆 (Affinity States)
    public List<CharacterAffinitySaveNode> affinityStates;
    
    // 每日行动点数记忆 (防止S/L大法无限刷)
    public int currentInteractionPoints;
}

// ===================================================================================
// 2. 子数据结构 (Sub-Structures)
// ===================================================================================

[System.Serializable]
public struct PlayerSaveData
{
    // 必须知道这份数据是谁的，读档时才能去 Resources 找对应的立绘和配置！
    public string characterID;
    public int level;
    public int currentExp;
    public int hp;
    public int mp;
    public int stamina;
    public int gold;
    public int talentPoints;

    // 字典无法直接序列化，拆分成 List<Struct> [cite: 76, 95]
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
    public RuntimeEquipmentSaveData equipData;
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
    public RuntimeEquipmentSaveData equipData; // 实体肉身数据

    // 在构造函数中补全 equipData 的默认初始化
    public EquipmentEntry(EquipmentSlot s, string id) 
    { 
        slot = s; 
        itemID = id; 
        equipData = null; 
    }
}

[System.Serializable]
public class RuntimeEquipmentSaveData
{
    public string uid;
    public string blueprintID;
    public int level;
    public int currentExp;
    public int rarity; // 使用 int 存储枚举
    public int currentDurability;
    public List<ItemAffix> affixes;
}

[System.Serializable]
public struct ShopSaveData
{
    public string shopID; // 商店配置文件的文件名 (e.g., "Shop_Village")
    public List<ShopItemStockSaveData> itemStocks;
}

[System.Serializable]
public struct ShopItemStockSaveData
{
    public string itemID; // 物品的文件名
    public int currentStock;
}

[System.Serializable]
public struct CharacterAffinitySaveNode
{
    public string characterID;
    public int trust;       // 信任度
    public int intimacy;    // 亲密度
    public int dependency;  // 依赖度
}