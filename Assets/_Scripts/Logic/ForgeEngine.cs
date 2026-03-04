using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 装备实例化与词条摇骰核心引擎 (The Smart Factory)
/// </summary>
public static class ForgeEngine
{
    public static RuntimeEquipment Generate(EquipmentData blueprint, EquipmentRarity rarity = EquipmentRarity.Common)
    {
        // 1. 赋予肉身基础躯壳
        RuntimeEquipment equip = new RuntimeEquipment(blueprint, rarity);
        equip.affixes = new List<ItemAffix>();

        // 2. 根据品质决定词条数量 (暗黑法则)
        int affixCount = rarity switch
        {
            EquipmentRarity.Common => 0,               // 白装无词条
            EquipmentRarity.Rare => Random.Range(1, 3),      // 蓝装 1~2 条
            EquipmentRarity.Epic => Random.Range(2, 4),      // 紫装 2~3 条
            EquipmentRarity.Legendary => Random.Range(3, 5), // 金装 3~4 条
            _ => 0
        };

        if (affixCount > 0)
        {
            // 3. 读取图纸的词条池，如果没有配置，就用全局保底池
            List<StatType> pool = (blueprint.possibleAffixes != null && blueprint.possibleAffixes.Count > 0)
                ? blueprint.possibleAffixes
                : new List<StatType> { StatType.Attack, StatType.Defense, StatType.MaxHP, StatType.Speed, StatType.CritRate, StatType.CritDamage };

            // 4. 疯狂摇骰子
            for (int i = 0; i < affixCount; i++)
            {
                StatType rolledStat = pool[Random.Range(0, pool.Count)];
                
                // 暴击类强制为百分比，其他属性 50% 概率为百分比
                bool isPct = (rolledStat == StatType.CritRate || rolledStat == StatType.CritDamage || Random.value > 0.5f);
                
                // 决定数值大小 (百分比一般数值小，固定值数值大)
                float val = isPct ? Random.Range(5f, 20f) : Random.Range(10f, 50f);
                val = Mathf.Round(val * 10f) / 10f; // 保留一位小数，防止界面显示太长

                equip.affixes.Add(new ItemAffix { statType = rolledStat, value = val, isPercent = isPct });
            }
        }
        
        // 5. 组装完毕，重新计算最终动态面板
        equip.CalculateDynamicStats();
        
        Debug.Log($"[Forge Engine] 🏭 打造出炉: {blueprint.itemName} [{rarity}] | 附带 {affixCount} 个词条");
        return equip;
    }
}