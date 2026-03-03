using UnityEngine;
using System.Collections.Generic;

// 👇 核心设定：装备的稀有度法则
public enum EquipmentRarity 
{ 
    Common,     // 普通 (白) - 1.0x 属性
    Rare,       // 稀有 (蓝) - 1.2x 属性
    Epic,       // 史诗 (紫) - 1.5x 属性
    Legendary   // 传说 (金) - 2.0x 属性
}

// 👇 核心设定：随机词条结构
[System.Serializable]
public class ItemAffix
{
    public StatType statType; 
    public float value;          // 数值
    public bool isPercent;       // 是固定数值还是百分比加成
}

[System.Serializable]
public class RuntimeEquipment
{
    public string uid;                  // 唯一标识符，全服不重样
    public EquipmentData blueprint;     // 溯源图纸 (查图标、名字、模型用)
    
    public int level = 0;               // 强化等级 (+1, +2...)
    public EquipmentRarity rarity;      // 当前品质
    public int currentDurability;       // 独立耐久度

    // 🌟 动态计算后的最终面板白值 (图纸基础值 * 品质乘区 * 强化乘区)
    public int DynamicDamage { get; private set; }
    public int DynamicDefense { get; private set; }
    public int DynamicMaxHP { get; private set; }
    public int DynamicMaxMP { get; private set; }

    // 随机抽卡出来的词条池
    public List<ItemAffix> affixes = new List<ItemAffix>();

    /// <summary>
    /// 锻造/掉落 时的装备实例化构造函数
    /// </summary>
    public RuntimeEquipment(EquipmentData sourceData, EquipmentRarity targetRarity = EquipmentRarity.Common)
    {
        blueprint = sourceData;
        uid = System.Guid.NewGuid().ToString(); // 诞生即唯一
        level = 0;
        
        // 如果 maxDurability <= 0，代表永不磨损，设为 -1
        currentDurability = sourceData.maxDurability > 0 ? sourceData.maxDurability : -1;
        rarity = targetRarity;
        
        CalculateDynamicStats();
    }

    /// <summary>
    /// 计算物理法则乘区
    /// </summary>
    public void CalculateDynamicStats()
    {
        float rarityMult = 1.0f;
        switch(rarity) 
        {
            case EquipmentRarity.Rare: rarityMult = 1.2f; break;
            case EquipmentRarity.Epic: rarityMult = 1.5f; break;
            case EquipmentRarity.Legendary: rarityMult = 2.0f; break;
        }

        float levelMult = 1f + (level * 0.1f); // 每强化1级，基础属性膨胀 10%

        float totalMult = rarityMult * levelMult;

        DynamicDamage = Mathf.RoundToInt(blueprint.baseDamage * totalMult);
        DynamicDefense = Mathf.RoundToInt(blueprint.baseDefense * totalMult);
        DynamicMaxHP = Mathf.RoundToInt(blueprint.baseMaxHP * totalMult);
        DynamicMaxMP = Mathf.RoundToInt(blueprint.baseMaxMP * totalMult);
    }
}