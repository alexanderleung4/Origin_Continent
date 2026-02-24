using UnityEngine;
using System.Collections.Generic; 

// ===================================================================================
// 1. 装备专用枚举 (Equipment Enums)
// ===================================================================================

// 根据白皮书 V1.3 定义的7个槽位 
public enum EquipmentSlot 
{
    Weapon, // 主武器
    Head,   // 头部
    Neck,   // 颈部
    Body,   // 身体 (关联爆衣逻辑)
    Hands,  // 手部
    Legs,   // 腿部
    Feet    // 脚部
}

// ⚠️ 注意：StatType 已经在 CharacterData.cs 中定义过了，这里必须删除，否则报 CS0101 错误！
// public enum StatType { ... }  <-- 已删除

public enum ModifierType
{
    Flat,       // 固定值 (例如 +10)
    Percent     // 百分比 (例如 +15 代表 +15%)
}

// ===================================================================================
// 2. 辅助结构体 (Helper Structs)
// ===================================================================================

[System.Serializable]
public struct StatModifier 
{
    // 这里直接引用 CharacterData.cs 里定义的 StatType
    public StatType statType; 
    public ModifierType type; 
    public int value;
}

// ===================================================================================
// 3. 装备数据类 (Equipment Data Class)
// ===================================================================================

[CreateAssetMenu(fileName = "NewEquipment", menuName = "Origin/Equipment Data")]
public class EquipmentData : ItemData
{
    // ItemData 已包含 icon, itemName, description 等字段

    [Header("🛡️ Type Config (类型配置)")]
    public EquipmentSlot slotType;  
    
    [Header("📊 Stats (属性加成)")]
    // 支持同时加多个属性 (e.g. 攻+10, 防+5)
    public List<StatModifier> modifiers = new List<StatModifier>(); 

    [Header("⚡ Special Logic (特殊数值)")]
    public int baseDamage;  // 武器特有 (基础面板)
    public int baseDefense; // 防具特有 (基础面板)
    public int baseMaxHP;   // 饰品特有
    public int baseMaxMP;
    
    // 只有身体防具(Body)需要填写，用于爆衣系统
    public int maxDurability;       

    private void OnValidate()
    {
        type = ItemType.Equipment; 
        isStackable = false;       // 装备绝对不可堆叠
        maxStack = 1;
    }
}