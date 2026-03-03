using UnityEngine;
using System.Collections.Generic; 

public enum EquipmentSlot 
{
    Weapon, Head, Neck, Body, Hands, Legs, Feet 
}

public enum ModifierType { Flat, Percent }

[System.Serializable]
public struct StatModifier 
{
    public StatType statType; 
    public ModifierType type; 
    public int value;
}

[CreateAssetMenu(fileName = "NewEquipment", menuName = "Origin/Equipment Data")]
public class EquipmentData : ItemData
{
    [Header("🛡️ Type Config (类型配置)")]
    public EquipmentSlot slotType;  
    
    [Header("⚡ Base Stats (基础白值)")]
    [Tooltip("这里的数值会受品质(金紫蓝白)和强化等级的乘区放大！")]
    public int baseDamage;  
    public int baseDefense; 
    public int baseMaxHP;   
    public int baseMaxMP;
    public int maxDurability;       

    [Header("📌 Innate Modifiers (固有属性/死数值)")]
    [Tooltip("只要穿上这件装备就必给的属性，不受品质放大。例如匕首天生自带 +10速度")]
    public List<StatModifier> modifiers = new List<StatModifier>(); 

    [Header("🎲 RNG Affix Pool (锻造随机词条池)")]
    [Tooltip("这把武器在锻造时能摇出什么词条？如果留空，则在全属性中随机。例如剑可以只配置: Attack, CritRate, CritDamage")]
    public List<StatType> possibleAffixes = new List<StatType>();

    private void OnValidate()
    {
        type = ItemType.Equipment; 
        isStackable = false;       // 装备绝对不可堆叠
        maxStack = 1;
    }
    // 👇 新增：拆解产物配置
    [Header("♻️ Salvage Rewards (拆解保底产物)")]
    [Tooltip("拆解这件装备时，保底能获得什么？高品质装备会提供产量倍率放大！")]
    public List<CraftingIngredient> salvageRewards = new List<CraftingIngredient>();
}