using UnityEngine;
using System.Collections.Generic;

// ===================================================================================
// 1. 核心枚举定义 (放在这里方便全局调用)
// ===================================================================================

// [核心] 属性类型：用于加点、装备加成计算
public enum StatType
{
    MaxHP,
    MaxMP,
    MaxStamina,
    Attack,
    Defense,
    Speed,
    CritRate,   // 暴击率
    CritDamage  // 暴击伤害
}

// [核心] 阵营：用于判定敌人还是朋友
public enum TeamType
{
    Player,     // 玩家阵营 (队友)
    Enemy,      // 敌对阵营 (怪物)
    Neutral     // 中立 (NPC)
}

// ===================================================================================
// 2. 数据类定义
// ===================================================================================

[System.Serializable]
public struct DefeatCGConfig
{
    [Tooltip("击杀者的 CharacterID (留空或填 Default 代表默认CG)")]
    public string killerID;
    [Tooltip("触发的 CG Event ID")]
    public string cgEventID;
}

[CreateAssetMenu(fileName = "NewCharacter", menuName = "Origin/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("📝 Identity (身份信息)")]
    public string characterName; 
    public string characterID;    // ✅ [核心] 存档和查找的关键 ID
    [TextArea] public string description; 

    [Header("⚔️ Combat Identity (战斗身份)")] 
    public TeamType team; 
    
    // 🗓️ [未来] AI 系统 (Phase 3 战斗完善时用到)
    public AIProfileBase aiProfile; 
    
    [Header("🎨 Visuals (美术表现)")]
    public Sprite portrait; // ✅ [核心] UI头像
    
    // 🗓️ [未来] 动态立绘系统 (VFXManager 会用到)
    public Sprite bodySprite_Normal; 
    public Sprite bodySprite_Hit;  
    public Sprite bodySprite_Dead; 
    // 🗓️ [未来] 服装破坏 (绅士系统?)
    public Sprite bodySprite_HalfBroken; 
    public Sprite bodySprite_Broken; 

    [Range(0.5f, 3f)] public float visualScale = 1.0f; // ✅ [核心] 战斗时调整怪物大小

    [Header("💬 Interaction (交互配置)")]
    // ✅ [核心] 商店系统
    public ShopData linkedShop; 
    
    // 🗓️ [未来] 好感度系统
    public int defaultAffinity = 0;

    [Header("🗣️ Dialogue (对话配置)")]
    // ✅ [核心] 闲聊
    public DialogueData defaultDialogue; 
    // ✅ [核心] 任务/剧情对话索引
    public string currentStoryCSV;

    [Header("📊 Base Stats (基础白值 - Lv.1)")]
    // ✅ [核心] 所有的计算基石
    public int maxHP;       
    public int maxMP;       
    public int maxStamina = 100; 
    public int attack;      
    public int defense;     
    public int speed;       

    // ✅ [核心] 暴击参数
    [Range(0f, 1f)] public float baseCritRate = 0.15f;    // 默认 15%
    [Range(1f, 3f)] public float baseCritDamage = 1.5f;   // 默认 150%

    [Header("📈 Growth System (成长配置)")]
    public int maxLevel = 100; 

    // ✅ [核心] 属性成长曲线 (Lv 1 -> Lv 100 属性翻几倍)
    public AnimationCurve statGrowthCurve = AnimationCurve.Linear(1, 1, 100, 5);
    
    // ✅ [核心] 升级经验曲线 (X:等级, Y:所需总经验)
    public AnimationCurve expCurve = AnimationCurve.EaseInOut(1, 0, 100, 100000);

    [Range(0f, 1f)] public float growthRate = 1.2f; 

    [Header("💀 Drops & Rewards (掉落与奖励)")]
    public int killExpReward = 50;  // ✅ [核心] 怪物给经验
    public int killGoldReward = 20; // ✅ [核心] 怪物给钱
    public LootTable lootTable;     // ✅ [核心] 掉落包

    [Header("🎬 Defeat & CG (战败与演出)")]
    public List<DefeatCGConfig> defeatCGs = new List<DefeatCGConfig>();

    public string GetDefeatCG(string killerID)
    {
        if (defeatCGs == null || defeatCGs.Count == 0) return string.Empty;
        string defaultCG = string.Empty;
        foreach (var config in defeatCGs)
        {
            if (config.killerID == killerID) return config.cgEventID;
            if (string.IsNullOrEmpty(config.killerID) || config.killerID.ToLower() == "default") defaultCG = config.cgEventID;
        }
        return defaultCG;
    }

    [Header("💪 Skills (技能)")]
    public List<SkillData> startingSkills; // ✅ [核心] 初始技能

}