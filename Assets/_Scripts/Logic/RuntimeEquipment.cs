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
    public int currentExp = 0;          // 当前强化经验
    public const int MAX_LEVEL = 15;    // 强化等级上限设为 +15
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

    /// <summary>
    /// 获取升到下一级所需的经验值
    /// </summary>
    public int GetExpToNextLevel()
    {
        if (level >= MAX_LEVEL) return 0;

        // 公式：(当前等级 + 1) * 100 * 品质倍率
        // 品质越好，升级需要的经验越多
        float rarityMult = 1.0f;
        switch(rarity) 
        {
            case EquipmentRarity.Rare: rarityMult = 1.5f; break;
            case EquipmentRarity.Epic: rarityMult = 2.0f; break;
            case EquipmentRarity.Legendary: rarityMult = 3.0f; break;
        }
        
        return Mathf.RoundToInt((level + 1) * 100 * rarityMult);
    }

    /// <summary>
    /// 注入经验，处理升级，并返回升了多少级
    /// </summary>
    public void AddExp(int amount, out int levelsGained)
    {
        levelsGained = 0;
        if (level >= MAX_LEVEL) return;

        int startLevel = level; // 记录初始等级用于判定里程碑
        currentExp += amount;
        
        // 循环判定升级（支持一次吃大量狗粮连升多级）
        while (level < MAX_LEVEL && currentExp >= GetExpToNextLevel())
        {
            currentExp -= GetExpToNextLevel();
            level++;
            levelsGained++;
        }

        // 满级后经验清零防溢出
        if (level >= MAX_LEVEL)
        {
            currentExp = 0; 
        }

        // 发生升级时，重新计算白值乘区并判定词条觉醒
        if (levelsGained > 0)
        {
            CalculateDynamicStats();

            // 👇 核心补回：里程碑觉醒判定 (+5, +10, +15)
            int oldMilestone = startLevel / 5;
            int newMilestone = level / 5;
            int awakenTimes = newMilestone - oldMilestone;

            for (int i = 0; i < awakenTimes; i++)
            {
                AwakenRandomAffix();
            }
        }
    }

    /// <summary>
    /// 当这件装备被当作狗粮喂给别人时，它能提供多少总经验？
    /// </summary>
    public int GetTotalFeedValue()
    {
        int baseValue = blueprint.feedExpValue;
        
        // 如果这件装备本身被强化过，返还曾投入经验的 80%
        int investedExp = 0;
        float rarityMult = 1.0f;
        switch(rarity) 
        {
            case EquipmentRarity.Rare: rarityMult = 1.5f; break;
            case EquipmentRarity.Epic: rarityMult = 2.0f; break;
            case EquipmentRarity.Legendary: rarityMult = 3.0f; break;
        }

        // 累加之前每一级的所需经验
        for (int i = 0; i < level; i++)
        {
            investedExp += Mathf.RoundToInt((i + 1) * 100 * rarityMult);
        }
        investedExp += currentExp;

        return baseValue + Mathf.RoundToInt(investedExp * 0.8f);
    }

    /// <summary>
    /// 词条觉醒：随机挑选一条现有词条，使其数值提升 20%~30%
    /// </summary>
    private void AwakenRandomAffix()
    {
        if (affixes == null || affixes.Count == 0) return;

        int targetIndex = UnityEngine.Random.Range(0, affixes.Count);
        ItemAffix targetAffix = affixes[targetIndex];

        float buffMultiplier = UnityEngine.Random.Range(1.2f, 1.3f);
        
        float oldVal = targetAffix.value;
        targetAffix.value = Mathf.Round(targetAffix.value * buffMultiplier * 10f) / 10f; 
        
        affixes[targetIndex] = targetAffix;

        Debug.Log($"[觉醒] 突破里程碑！装备词条 [{targetAffix.statType}] 强化：{oldVal} -> {targetAffix.value}");
    }
}