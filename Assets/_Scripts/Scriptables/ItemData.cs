using UnityEngine;

public enum ItemType
{
    Consumable, // 消耗品 (药水)
    Material,   // 材料 (史莱姆粘液)
    Equipment,  // 装备 (剑/盾) - 暂时只做占位，以后做装备系统细化
    KeyItem     // 关键道具 (钥匙)
}

[CreateAssetMenu(fileName = "NewItem", menuName = "Origin/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Basic Info")]
    public string itemID;       // 唯一ID (如: item_potion_hp_s)
    public string itemName;     // 显示名
    [TextArea] public string description;
    public Sprite icon;         // 图标
    public ItemType type;

    [Header("Behavior")]
    public bool isStackable = true; // 是否可堆叠
    public int maxStack = 99;

    [Header("Economy (经济)")]
    public int buyPrice = 100;  // 从商店买的价格
    public int sellPrice = 50;  // 卖给商店的价格 (通常是买价的一半)
    public bool isSellable = true; // 关键道具设为 false

    [Header("Consumable Stats (仅消耗品有效)")]
    public int healAmount;      // 回血量
    public int manaAmount;      // 回蓝量
    public int staminaAmount;   // 回精力量
    // public int buffID;       // 以后做Buff用
}