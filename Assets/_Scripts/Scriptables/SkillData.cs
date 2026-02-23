using UnityEngine;
using System.Collections.Generic;

// ==========================================
// 1. 基础分类枚举 (保持不变)
// ==========================================
public enum SkillCategory { Attack, Defense, Heal }
public enum DamageType { Physical, Magical, None }
public enum TargetScope { Self, Single_Enemy, Single_Ally, All_Enemies, Random_Enemies }
public enum CutInAnimType { Hard_Impact, Slow_Zoom, Soft_Fade }

// ==========================================
// 2. 👇 新增: 技能效果核心枚举
// ==========================================
public enum EffectType 
{ 
    Damage,     // 造成伤害
    Heal,       // 恢复生命
    ApplyBuff   // 施加状态 (预留)
}

public enum EffectTarget 
{ 
    SelectedTarget, // 作用于技能选定的目标 (比如对敌人造成伤害)
    Self            // 作用于施法者自身 (比如吸血、给自己加Buff)
}

public enum ScalingStat 
{ 
    None,           // 纯固定数值
    Attack,         // 攻击力加成
    Defense,        // 防御力加成
    MaxHP,          // 最大生命加成
    CurrentHP,      // 当前生命加成
    MaxMP,          // 最大法力加成
    CurrentMP,      // 当前法力加成
    Speed           // 速度加成
}

// ==========================================
// 3. 👇 新增: 技能效果模块 (可序列化，能在面板里配置)
// ==========================================
[System.Serializable]
public class SkillEffect
{
    [Header("1. 效果定性 (What & Who)")]
    public EffectType effectType;       // 是伤害还是治疗？
    public EffectTarget effectTarget;   // 打别人还是打自己？

    [Header("2. 数值计算 (How much)")]
    public int baseValue;               // 固定基础值 (例如: 100)
    public ScalingStat scalingStat;     // 吃什么属性加成？(例如: Attack)
    public float scalingMultiplier;     // 加成多少倍？ (例如: 1.5 = 150%)

    [Header("3. 特殊修饰 (Modifiers)")]
    [Min(1)] 
    public int hitCount = 1;            // 连击次数 (1就是单段伤害，3就是三连击)
    
    [Range(0f, 1f)] 
    public float lifestealPercent = 0f; // 吸血比例 (0.5 = 造成伤害的50%转化为治疗)

    // 预留给未来的 Buff 系统
    public BuffData buffToApply; 
}

// ==========================================
// 4. 技能数据主体
// ==========================================
[CreateAssetMenu(fileName = "NewSkill", menuName = "Origin/Skill Data")]
public class SkillData : ScriptableObject
{
    [Header("Basic Info (基础信息)")]
    public string skillID;
    public string skillName;
    [TextArea] public string description;
    public Sprite icon;

    [Header("Type Config (类型配置)")]
    public SkillCategory category;      // UI分类菜单
    public DamageType damageType;       // 物理/魔法 (影响诅咒和资源消耗)
    public TargetScope targetScope;     // 目标选择范围

    [Header("Costs (消耗)")]
    public int mpCost;          
    public int staminaCost;     
    public int cooldownTurns;   
    public int curseCost = 0;   

    [Header("✨ Skill Effects (技能效果列表) ✨")]
    // 👇 核心机制：一个技能可以包含无限个效果组合
    public List<SkillEffect> effects = new List<SkillEffect>();

    [Header("Audio (音效)")]
    public AudioClip sfxClip;

    [Header("Visuals (视觉演出)")]
    public Sprite cutInImage; 
    public CutInAnimType cutInType;
    public GameObject hitVFXPrefab;
}