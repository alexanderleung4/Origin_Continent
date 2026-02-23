using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class LootDrop
{
    public ItemData item;
    [Range(0f, 100f)] public float dropChance; // 掉落率 (0-100%)
    public int minAmount = 1;
    public int maxAmount = 1;
}

[CreateAssetMenu(fileName = "NewLootTable", menuName = "Origin/Loot Table")]
public class LootTable : ScriptableObject
{
    public List<LootDrop> potentialDrops;

    // --- 计算掉落逻辑 ---
    public List<InventorySlot> GenerateLoot()
    {
        List<InventorySlot> rewards = new List<InventorySlot>();

        foreach (var drop in potentialDrops)
        {
            // 掷骰子 (0-100)
            float roll = Random.Range(0f, 100f);
            if (roll <= drop.dropChance)
            {
                int count = Random.Range(drop.minAmount, drop.maxAmount + 1);
                rewards.Add(new InventorySlot(drop.item, count));
            }
        }
        return rewards;
    }
}