using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class LootDrop
{
    public ItemData item;
    [Range(0f, 100f)] public float dropChance; 
    public int minAmount = 1;
    public int maxAmount = 1;

    [Header("🎲 装备爆率权重 (仅图纸有效)")]
    public bool useRNGDrop = false; // 是否开启此装备的抽卡掉落
    public float chanceCommon = 60f;
    public float chanceRare = 30f;
    public float chanceEpic = 9f;
    public float chanceLegendary = 1f; // 1% 爆出金装！
}

[CreateAssetMenu(fileName = "NewLootTable", menuName = "Origin/Loot Table")]
public class LootTable : ScriptableObject
{
    public List<LootDrop> potentialDrops;

    public List<InventorySlot> GenerateLoot()
    {
        List<InventorySlot> rewards = new List<InventorySlot>();

        foreach (var drop in potentialDrops)
        {
            if (Random.Range(0f, 100f) <= drop.dropChance)
            {
                int count = Random.Range(drop.minAmount, drop.maxAmount + 1);
                
                // 👇 如果掉落的是武器图纸，且开启了抽卡机制
                if (drop.item is EquipmentData equipData && drop.useRNGDrop)
                {
                    for(int i = 0; i < count; i++) 
                    {
                        EquipmentRarity rarity = EquipmentRarity.Common;
                        float roll = Random.Range(0f, 100f);
                        
                        if (roll < drop.chanceLegendary) rarity = EquipmentRarity.Legendary;
                        else if (roll < drop.chanceLegendary + drop.chanceEpic) rarity = EquipmentRarity.Epic;
                        else if (roll < drop.chanceLegendary + drop.chanceEpic + drop.chanceRare) rarity = EquipmentRarity.Rare;

                        // 呼叫母机造出这把带词条的随机武器
                        RuntimeEquipment equip = ForgeEngine.Generate(equipData, rarity);
                        rewards.Add(new InventorySlot(equipData, 1, equip)); 
                    }
                }
                else
                {
                    // 普通物品或未开启抽卡的装备
                    rewards.Add(new InventorySlot(drop.item, count));
                }
            }
        }
        return rewards;
    }
}