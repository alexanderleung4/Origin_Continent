using UnityEngine;
using System.Collections.Generic;

public enum TraitType { Buff, Debuff, Unique }

[CreateAssetMenu(fileName = "NewTrait", menuName = "Origin/Trait Data")]
public class TraitData : ScriptableObject
{
    [Header("Basic Info (基础信息)")]
    public string traitID;          
    public string traitName;        
    public TraitType type;
    public Sprite icon;             
    [TextArea] public string baseDescription;

    [Header("Duration (期限设定)")]
    public bool isPermanent = true; // 是否永久
    public int durationDays = 0;    // 如果不是永久，持续几天？

    [Header("Leveling Mechanics (层数机制)")]
    public bool isLevelable = true; 
    public int maxLevel = 3;        

    [Header("Effects Per Level (各层级效果)")]
    public List<TraitLevel> levels = new List<TraitLevel>();
}

[System.Serializable]
public class TraitLevel
{
    [TextArea] public string levelDescription;
    public List<StatModifier> modifiers = new List<StatModifier>();
    
    // 👇 核心改动：把字符串换成您提议的“可拖拽脚本”！
    [Tooltip("拖入特殊的逻辑插件 (TraitEffectBase)")]
    public List<TraitEffectBase> specialEffects = new List<TraitEffectBase>();
}