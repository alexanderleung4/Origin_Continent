using UnityEngine;
using System.Collections.Generic;

// 库存刷新类型
public enum StockRefreshType
{
    Daily,      // 每日刷新 (面包、药水)
    Never,      // 永不刷新/一次性 (技能书、神器)
    Unlimited   // 无限库存 (基础物资)
}

[System.Serializable]
public struct ShopItemEntry
{
    public ItemData item;
    public int maxStock;        
    public StockRefreshType refreshType;
    
    [HideInInspector] public int currentStock; 

    [Header("✨ 神装定制 (仅对装备图纸有效)")]
    [Tooltip("勾选后，玩家买到的将不再是白板，而是指定品质并带有随机词条的神装！")]
    public bool overrideEquipment; 
    public EquipmentRarity targetRarity;
}

[CreateAssetMenu(fileName = "NewShop", menuName = "Origin/Shop Data")]
public class ShopData : ScriptableObject
{
    public string shopName;
    
    // 👇 升级：从 ItemData 列表变为 Entry 列表
    public List<ShopItemEntry> stockItems;
}