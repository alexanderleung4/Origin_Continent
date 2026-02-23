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
    public int maxStock;        // 最大库存 (-1 代表无限)
    public StockRefreshType refreshType;
    
    [HideInInspector] public int currentStock; // 运行时库存 (非序列化，由 Manager 管理)
}

[CreateAssetMenu(fileName = "NewShop", menuName = "Origin/Shop Data")]
public class ShopData : ScriptableObject
{
    public string shopName;
    
    // 👇 升级：从 ItemData 列表变为 Entry 列表
    public List<ShopItemEntry> stockItems;
}