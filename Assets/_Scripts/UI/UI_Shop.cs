using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class UI_Shop : MonoBehaviour
{
    public static UI_Shop Instance { get; private set; }

    [Header("Panel References")]
    public GameObject panelRoot;
    public TextMeshProUGUI shopTitleText;
    public TextMeshProUGUI playerGoldText;
    public Button closeButton;

    [Header("Mode Switching")]
    public Button btnModeBuy; // "买入" 按钮
    public Button btnModeSell; // "卖出" 按钮
    private bool isBuyingMode = true; // 当前是否在买入

    [Header("List Area")]
    public Transform listContainer; // 滚动列表的内容父节点
    public GameObject shopSlotPrefab; // 商品格子预制体

    [Header("Details Area")]
    public GameObject detailsPanel; // 详情面板整体
    public Image detailIcon;
    public TextMeshProUGUI detailName;
    public TextMeshProUGUI detailDesc;
    public TextMeshProUGUI detailPrice;
    public Button actionButton; // "购买" 或 "出售" 按钮
    public TextMeshProUGUI actionBtnText;
    public TextMeshProUGUI detailStock; // 详情页显示库存/持有量

    // --- 运行时数据 ---
    private ShopData currentShop;
    private ItemData selectedItem;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        
        panelRoot.SetActive(false);
        
        // 绑定按钮事件
        if (closeButton) closeButton.onClick.AddListener(CloseShop);
        if (btnModeBuy) btnModeBuy.onClick.AddListener(() => SwitchMode(true));
        if (btnModeSell) btnModeSell.onClick.AddListener(() => SwitchMode(false));
        if (actionButton) actionButton.onClick.AddListener(OnActionClick);
    }

    // --- 核心入口: 打开商店 ---
    public void OpenShop(ShopData shop)
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.CloseAllMenus();
            UIManager.Instance.OnAnyMenuOpened();
        }
        currentShop = shop;
        panelRoot.SetActive(true);
        
        if (shopTitleText) shopTitleText.text = shop.shopName;
        
        // 默认进买入模式
        SwitchMode(true);
    }

    public void CloseShop()
    {
        panelRoot.SetActive(false);
        currentShop = null;
        selectedItem = null;
    }

    // --- 模式切换 ---
    public void SwitchMode(bool buying)
    {
        isBuyingMode = buying;
        selectedItem = null; // 切换模式时清空选择
        detailsPanel.SetActive(false); // 隐藏详情

        // 更新按钮视觉 (可选：高亮当前模式)
        // if (btnModeBuy) btnModeBuy.interactable = !buying;
        // if (btnModeSell) btnModeSell.interactable = buying;

        RefreshList();
        RefreshGold();
    }

    // --- 刷新金币显示 ---
    public void RefreshGold()
    {
        if (GameManager.Instance.Player != null)
        {
            playerGoldText.text = $"Gold: {GameManager.Instance.Player.Gold}";
        }
    }

    // --- 核心: 刷新列表 ---
public void RefreshList()
    {
        // 1. 清空列表
        foreach (Transform child in listContainer) Destroy(child.gameObject);

        // 2. 根据模式决定数据源
        if (isBuyingMode)
        {
            // --- 买入模式: 显示商店库存 ---
            if (currentShop != null)
            {
                // 这里遍历的是 ShopItemEntry，而不是 ItemData
                foreach (ShopItemEntry entry in currentShop.stockItems)
                {
                    // 我们要把 entry.item 传给创建函数
                    CreateSlot(entry.item, true);
                }
            }
        }
        else
        {
            // --- 卖出模式: 显示玩家背包 (只显示可卖品) ---
            if (InventoryManager.Instance != null)
            {
                foreach (InventorySlot slot in InventoryManager.Instance.inventory)
                {
                    if (slot.itemData.isSellable)
                    {
                        CreateSlot(slot.itemData, false);
                    }
                }
            }
        }
    }

    // --- 创建格子 ---
    private void CreateSlot(ItemData item, bool isBuy)
    {
        GameObject slotObj = Instantiate(shopSlotPrefab, listContainer);
        
        Image icon = slotObj.transform.Find("Icon").GetComponent<Image>();
        TextMeshProUGUI name = slotObj.transform.Find("Name").GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI price = slotObj.transform.Find("Price").GetComponent<TextMeshProUGUI>();
        Button btn = slotObj.GetComponent<Button>();

        // 👇 新增: 获取库存/持有量 Text (假设 Prefab 里加了一个叫 "Stock" 的文本)
        // 如果没有这个 UI 元素，这一步可以跳过，只在详情页显示
        // TextMeshProUGUI stockText = slotObj.transform.Find("Stock").GetComponent<TextMeshProUGUI>();

        icon.sprite = item.icon;
        name.text = item.itemName;
        
        // --- 逻辑分支 ---
        if (isBuy)
        {
            // 买入模式：显示经过难度计算的动态价格
            int dynamicPrice = GameManager.Instance.GetDynamicBuyPrice(item);
            price.text = $"${dynamicPrice}";
            
            // 检查库存是否耗尽
            int stock = ShopManager.Instance.GetStock(currentShop, item);
            if (stock == 0) // 卖光了
            {
                btn.interactable = false; // 变灰
                name.color = Color.gray;
                // if (stockText) stockText.text = "售罄";
            }
            else
            {
                btn.interactable = true;
                name.color = Color.white;
                // if (stockText) stockText.text = (stock == -1) ? "∞" : stock.ToString();
            }
        }
        else
        {
            // 卖出模式：显示收购价
            price.text = $"${item.sellPrice}";
            
            // 检查玩家持有量
            int owned = 0;
            var slot = InventoryManager.Instance.inventory.Find(s => s.itemData == item);
            if (slot != null) owned = slot.amount;
            
            // if (stockText) stockText.text = $"持有: {owned}";
        }

        btn.onClick.AddListener(() => OnItemSelect(item));
    }

    // --- 选中商品 ---
    private void OnItemSelect(ItemData item)
    {
        selectedItem = item;
        detailsPanel.SetActive(true);

        detailIcon.sprite = item.icon;
        detailName.text = item.itemName;
        detailDesc.text = item.description;

        if (isBuyingMode)
        {
            // --- 买入详情 ---
            int stock = ShopManager.Instance.GetStock(currentShop, item);
            string stockStr = (stock == -1) ? "无限" : stock.ToString();
            
            int dynamicPrice = GameManager.Instance.GetDynamicBuyPrice(item);
            detailPrice.text = $"价格: {dynamicPrice}";
            
            // 显示库存
            if (detailStock) detailStock.text = $"库存: {stockStr}";

            // 按钮状态
            if (stock == 0)
            {
                actionButton.interactable = false;
                actionBtnText.text = "已售罄";
            }
            else if (GameManager.Instance.Player.Gold < dynamicPrice) // 👇 修改判断
            {
                actionButton.interactable = false;
                actionBtnText.text = "金币不足";
            }
            else
            {
                actionButton.interactable = true;
                actionBtnText.text = "购买";
            }
        }
        else
        {
            // --- 卖出详情 ---
            int owned = 0;
            var slot = InventoryManager.Instance.inventory.Find(s => s.itemData == item);
            if (slot != null) owned = slot.amount;

            detailPrice.text = $"收购价: {item.sellPrice}";
            
            // 👇 新增: 显示持有量
            if (detailStock) detailStock.text = $"背包持有: {owned}";

            actionButton.interactable = true;
            actionBtnText.text = "出售";
        }
    }

    // --- 点击执行按钮 ---
    private void OnActionClick()
    {
        if (selectedItem == null) return;
        bool success = false;

        if (isBuyingMode)
        {
            // 👇 关键修改: 传入 currentShop
            success = ShopManager.Instance.BuyItem(currentShop, selectedItem, 1);
        }
        else
        {
            success = ShopManager.Instance.SellItem(selectedItem, 1);
        }

        if (success)
        {
            RefreshGold();
            RefreshList(); // 必须刷新列表，因为库存/持有量变了，按钮状态要更新
            OnItemSelect(selectedItem); // 刷新详情页状态
        }
    }
}