using UnityEngine;
using System.Collections.Generic;

public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance { get; private set; }

    // --- 运行时库存记录 ---
    // Key: ShopData (哪个商店)
    // Value: 字典 (Key: ItemData, Value: 剩余库存)
    private Dictionary<ShopData, Dictionary<ItemData, int>> shopStockState = new Dictionary<ShopData, Dictionary<ItemData, int>>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // 核心连接点: 监听时间管理器的跨天事件
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnDayChanged.AddListener(OnNewDayArrived);
        }
    }
    private void OnDestroy()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnDayChanged.RemoveListener(OnNewDayArrived);
        }
    }

    // --- 每日刷新回调 ---
    private void OnNewDayArrived(int newDay)
    {
        Debug.Log($"[ShopManager] 收到第 {newDay} 天的消息，正在进货...");
        RefreshDailyStock();
    }

    // --- 核心: 获取某商品的当前库存 ---
    public int GetStock(ShopData shop, ItemData item)
    {
        // 1. 如果库存状态还没初始化，先初始化
        if (!shopStockState.ContainsKey(shop)) InitializeShopStock(shop);

        // 2. 读取库存
        if (shopStockState[shop].ContainsKey(item))
        {
            return shopStockState[shop][item];
        }
        return 0;
    }

    // --- 初始化/重置库存 ---
    private void InitializeShopStock(ShopData shop)
    {
        Dictionary<ItemData, int> stockMap = new Dictionary<ItemData, int>();
        
        foreach (var entry in shop.stockItems)
        {
            // 如果是无限，我们用 -1 表示
            int stock = (entry.refreshType == StockRefreshType.Unlimited) ? -1 : entry.maxStock;
            stockMap.Add(entry.item, stock);
        }
        
        shopStockState.Add(shop, stockMap);
    }

    // --- 核心: 每日刷新逻辑 (等待 TimeManager) ---
    public void RefreshDailyStock()
    {
        // 遍历所有已加载的商店状态
        foreach (var shopEntry in shopStockState)
        {
            ShopData shopData = shopEntry.Key;
            var runtimeStock = shopEntry.Value;

            foreach (var configEntry in shopData.stockItems)
            {
                // 只有 Daily 类型的才回满
                if (configEntry.refreshType == StockRefreshType.Daily)
                {
                    if (runtimeStock.ContainsKey(configEntry.item))
                    {
                        runtimeStock[configEntry.item] = configEntry.maxStock;
                    }
                }
            }
        }
        Debug.Log("[ShopManager] 每日库存已刷新。");
    }

    // --- 购买逻辑 (更新版) ---
    public bool BuyItem(ShopData shop, ItemData item, int quantity = 1)
    {
        RuntimeCharacter player = GameManager.Instance.Player;
        if (player == null || item == null) return false;

        // 1. 检查库存
        int currentStock = GetStock(shop, item);
        
        // 如果不是无限(-1) 且 库存不足
        if (currentStock != -1 && currentStock < quantity)
        {
            Debug.Log("[Shop] 库存不足！");
            return false;
        }

        // 2. 检查金币 (👇 接入动态难度物价)
        int dynamicPrice = GameManager.Instance.GetDynamicBuyPrice(item);
        int totalCost = dynamicPrice * quantity;
        if (player.Gold < totalCost)
        {
            Debug.Log("[Shop] 金币不足！");
            return false;
        }

        // 3. 执行交易扣钱
        player.Gold -= totalCost;

        // 👇 4. 核心拦截：呼叫智能工厂发货！
        if (item is EquipmentData equipBlueprint)
        {
            // 找出这个商品在商店配置里的具体条目，看看策划有没有下达“定制指令”
            ShopItemEntry entry = shop.stockItems.Find(e => e.item == item);
            
            for (int i = 0; i < quantity; i++)
            {
                EquipmentRarity rarity = entry.overrideEquipment ? entry.targetRarity : EquipmentRarity.Common;
                // 呼叫母机，直接印出实体肉身
                RuntimeEquipment newEquip = ForgeEngine.Generate(equipBlueprint, rarity);
                
                // 将实体肉身强行塞入背包
                InventoryManager.Instance.AddItem(newEquip, 1);
            }
        }
        else
        {
            // 普通消耗品或材料，走旧通道
            InventoryManager.Instance.AddItem(item, quantity);
        }

        // 5. 扣除库存
        if (currentStock != -1)
        {
            shopStockState[shop][item] -= quantity;
        }

        Debug.Log($"购买成功: {item.itemName}");
        
        // 5. 刷新 UI
        InventoryManager.Instance.OnInventoryChanged?.Invoke(); 
        return true;
    }

    // --- 核心功能: 卖出 (Sell) ---
    public bool SellItem(ItemData item, int quantity = 1)
    {
        RuntimeCharacter player = GameManager.Instance.Player;
        if (player == null || item == null) return false;

        // 1. 检查能不能卖
        if (!item.isSellable) 
        {
            Debug.Log("这东西是非卖品！");
            return false;
        }

        // 2. 检查有没有货
        if (InventoryManager.Instance.HasItem(item, quantity))
        {
            int totalGain = item.sellPrice * quantity;

            // 3. 扣货
            InventoryManager.Instance.RemoveItem(item, quantity); // ⚠️ 需要在 InventoryManager 里加这个 Helper
            
            // 4. 给钱
            player.Gold += totalGain;

            Debug.Log($"[Economy] 出售成功: {item.itemName} x{quantity}, 获得: {totalGain}, 当前金币: {player.Gold}");
            
            // 5. 刷新
            InventoryManager.Instance.OnInventoryChanged?.Invoke();
            return true;
        }
        else
        {
            Debug.Log("你没有那么多货！");
            return false;
        }
    }
    // 👇 1. 定义一个变量，用来在 Inspector 里拖进去你要测试买的物品
    [Header("Debug Testing")]
    public ItemData debugItem; 

    // 测试函数
    [ContextMenu("Test Buy (测试购买)")]
    public void TestBuy()
    {
        if (debugItem != null)
        {
            // 因为现在的购买逻辑必须依赖一个 "ShopData" 来扣库存
            // 所以为了测试，我们现场捏造一个虚拟的 "Debug Shop"
            ShopData dummyShop = ScriptableObject.CreateInstance<ShopData>();
            dummyShop.shopName = "Debug Shop";
            dummyShop.stockItems = new List<ShopItemEntry>();
            
            // 给它添加无限库存
            dummyShop.stockItems.Add(new ShopItemEntry 
            { 
                item = debugItem, 
                maxStock = -1, 
                refreshType = StockRefreshType.Unlimited 
            });

            // 调用新的购买接口: (商店, 物品, 数量)
            BuyItem(dummyShop, debugItem, 1);
            
            // 销毁临时对象，保持内存清洁
            DestroyImmediate(dummyShop);
        }
        else
        {
            Debug.LogWarning("请先在 Inspector 里把 [Debug Item] 槽位填上！");
        }
    }
    
    // 👇 顺便也可以写个发钱的，方便测试
    [ContextMenu("Cheat: Add 1000 Gold")]
    public void CheatMoney()
    {
        if (GameManager.Instance != null && GameManager.Instance.Player != null)
        {
            GameManager.Instance.Player.Gold += 1000;
            Debug.Log($"[Cheat] 获得 1000 金币。当前: {GameManager.Instance.Player.Gold}");
        }
    }
    // ========================================================================
    // 9. 序列化与数据持久化对接 (Save System Integration)
    // ========================================================================
    
    /// <summary>
    /// 将内存中的库存状态打包，交给 SaveManager 存入硬盘
    /// </summary>
    public List<ShopSaveData> GetStockStateForSave()
    {
        List<ShopSaveData> list = new List<ShopSaveData>();
        foreach (var shopEntry in shopStockState)
        {
            // 🛡️ 核心防爆盾 1：如果商店配置表丢失或被代码销毁（如 TestBuy 中的临时对象），直接跳过！
            if (shopEntry.Key == null) continue; 

            ShopSaveData sData = new ShopSaveData();
            sData.shopID = shopEntry.Key.name; 
            sData.itemStocks = new List<ShopItemStockSaveData>();

            if (shopEntry.Value != null)
            {
                foreach (var itemEntry in shopEntry.Value)
                {
                    // 🛡️ 核心防爆盾 2：如果物品数据丢失，跳过！
                    if (itemEntry.Key == null) continue;

                    sData.itemStocks.Add(new ShopItemStockSaveData 
                    { 
                        itemID = itemEntry.Key.name, 
                        currentStock = itemEntry.Value 
                    });
                }
            }
            list.Add(sData);
        }
        return list;
    }

    /// <summary>
    /// 读档时，接收 SaveManager 传来的数据，覆盖当前内存
    /// </summary>
    public void RestoreStockState(List<ShopSaveData> savedStates)
    {
        shopStockState.Clear();
        if (savedStates == null || savedStates.Count == 0) return;

        foreach (var sData in savedStates)
        {
            // 去 Resources/Shops/ 目录下寻找商店配置 (请确保策划把商店SO建在这个目录下)
            ShopData shop = Resources.Load<ShopData>($"Shops/{sData.shopID}"); 
            if (shop != null)
            {
                Dictionary<ItemData, int> stockMap = new Dictionary<ItemData, int>();
                foreach (var iData in sData.itemStocks)
                {
                    ItemData item = Resources.Load<ItemData>($"Items/{iData.itemID}");
                    if (item != null) stockMap[item] = iData.currentStock;
                }
                shopStockState[shop] = stockMap;
            }
            else
            {
                Debug.LogWarning($"[ShopManager] 读档异常：找不到商店配置文件 Resources/Shops/{sData.shopID}");
            }
        }
        Debug.Log("[ShopManager] 商店库存数据已从存档中恢复。");
    }
}