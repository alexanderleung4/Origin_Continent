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
    public Button btnModeBuy; 
    public Button btnModeSell; 
    private bool isBuyingMode = true; 

    [Header("List Area")]
    public Transform listContainer; 
    public GameObject shopSlotPrefab; 

    [Header("Details Area")]
    public GameObject detailsPanel; 
    public Image detailIcon;
    public TextMeshProUGUI detailName;
    public TextMeshProUGUI detailDesc;
    public TextMeshProUGUI detailPrice;
    public Button actionButton; 
    public TextMeshProUGUI actionBtnText;
    public TextMeshProUGUI detailStock; 

    // --- 运行时数据 ---
    private ShopData currentShop;
    
    // 👇 修改：选中项不再是单纯的 ItemData，而是整个商品条目，这样才能知道它的定制品质
    private ShopItemEntry selectedEntry; 
    // 卖出模式下，我们只关心 ItemData，可以用一个临时的 Entry 包裹它
    private ItemData selectedSellItem;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        
        panelRoot.SetActive(false);
        
        if (closeButton) closeButton.onClick.AddListener(CloseShop);
        if (btnModeBuy) btnModeBuy.onClick.AddListener(() => SwitchMode(true));
        if (btnModeSell) btnModeSell.onClick.AddListener(() => SwitchMode(false));
        if (actionButton) actionButton.onClick.AddListener(OnActionClick);
    }

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
        
        SwitchMode(true);
    }

    public void CloseShop()
    {
        panelRoot.SetActive(false);
        currentShop = null;
        selectedEntry = default;
        selectedSellItem = null;
    }

    public void SwitchMode(bool buying)
    {
        isBuyingMode = buying;
        selectedEntry = default; 
        selectedSellItem = null;
        detailsPanel.SetActive(false); 

        RefreshList();
        RefreshGold();
    }

    public void RefreshGold()
    {
        if (GameManager.Instance.Player != null)
        {
            playerGoldText.text = $"Gold: {GameManager.Instance.Player.Gold}";
        }
    }

    public void RefreshList()
    {
        foreach (Transform child in listContainer) Destroy(child.gameObject);

        if (isBuyingMode)
        {
            if (currentShop != null)
            {
                foreach (ShopItemEntry entry in currentShop.stockItems)
                {
                    // 👇 传入完整的 entry
                    CreateSlot(entry, true);
                }
            }
        }
        else
        {
            if (InventoryManager.Instance != null)
            {
                // 用 HashSet 防重复显示同名物品
                HashSet<ItemData> processedItems = new HashSet<ItemData>();
                foreach (InventorySlot slot in InventoryManager.Instance.inventory)
                {
                    if (slot.itemData.isSellable && !processedItems.Contains(slot.itemData))
                    {
                        processedItems.Add(slot.itemData);
                        ShopItemEntry dummyEntry = new ShopItemEntry { item = slot.itemData };
                        CreateSlot(dummyEntry, false);
                    }
                }
            }
        }
    }

    private void CreateSlot(ShopItemEntry entry, bool isBuy)
    {
        ItemData item = entry.item;
        if (item == null) return;

        GameObject slotObj = Instantiate(shopSlotPrefab, listContainer);
        
        Image icon = slotObj.transform.Find("Icon").GetComponent<Image>();
        TextMeshProUGUI name = slotObj.transform.Find("Name").GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI price = slotObj.transform.Find("Price").GetComponent<TextMeshProUGUI>();
        Button btn = slotObj.GetComponent<Button>();

        icon.sprite = item.icon;
        
        // 👇 核心视觉升维：根据定制品质上色！
        string displayName = item.itemName;
        Color nameColor = Color.white;

        if (isBuy && entry.overrideEquipment && item is EquipmentData)
        {
            switch (entry.targetRarity)
            {
                case EquipmentRarity.Rare: displayName = $"<color=#4A90E2>【稀有】{item.itemName}</color>"; nameColor = new Color(0.3f, 0.6f, 1f); break;
                case EquipmentRarity.Epic: displayName = $"<color=#9013FE>【史诗】{item.itemName}</color>"; nameColor = new Color(0.6f, 0.1f, 1f); break;
                case EquipmentRarity.Legendary: displayName = $"<color=#F5A623>【传说】{item.itemName}</color>"; nameColor = new Color(1f, 0.84f, 0f); break; // 经典的暗黑暗金
            }
        }

        name.text = displayName;
        
        if (isBuy)
        {
            int dynamicPrice = GameManager.Instance.GetDynamicBuyPrice(item);
            price.text = $"${dynamicPrice}";
            
            int stock = ShopManager.Instance.GetStock(currentShop, item);
            if (stock == 0) 
            {
                btn.interactable = false; 
                name.color = Color.gray; // 售罄变灰
            }
            else
            {
                btn.interactable = true;
                // 如果没有售罄，且没有被上面上色，就保持白色
                if (!entry.overrideEquipment) name.color = Color.white; 
            }

            btn.onClick.AddListener(() => OnItemSelect_Buy(entry, displayName));
        }
        else
        {
            price.text = $"${item.sellPrice}";
            btn.onClick.AddListener(() => OnItemSelect_Sell(item));
        }
    }

    private void OnItemSelect_Buy(ShopItemEntry entry, string richName)
    {
        selectedEntry = entry;
        ItemData item = entry.item;
        detailsPanel.SetActive(true);

        detailIcon.sprite = item.icon;
        detailName.text = richName;
        
        // 追加神装描述
        if (entry.overrideEquipment)
            detailDesc.text = $"<color=#F5A623>[购买时随机生成 {entry.targetRarity} 级专属词条]</color>\n\n" + item.description;
        else
            detailDesc.text = item.description;

        int stock = ShopManager.Instance.GetStock(currentShop, item);
        string stockStr = (stock == -1) ? "无限" : stock.ToString();
        
        int dynamicPrice = GameManager.Instance.GetDynamicBuyPrice(item);
        detailPrice.text = $"价格: {dynamicPrice}";
        
        if (detailStock) detailStock.text = $"库存: {stockStr}";

        if (stock == 0)
        {
            actionButton.interactable = false;
            actionBtnText.text = "已售罄";
        }
        else if (GameManager.Instance.Player.Gold < dynamicPrice) 
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

    private void OnItemSelect_Sell(ItemData item)
    {
        selectedSellItem = item;
        detailsPanel.SetActive(true);

        detailIcon.sprite = item.icon;
        detailName.text = item.itemName;
        detailDesc.text = item.description;

        int owned = 0;
        var slot = InventoryManager.Instance.inventory.Find(s => s.itemData == item);
        if (slot != null) owned = slot.amount;

        detailPrice.text = $"收购价: {item.sellPrice}";
        if (detailStock) detailStock.text = $"背包持有: {owned}";

        actionButton.interactable = true;
        actionBtnText.text = "出售";
    }

    private void OnActionClick()
    {
        bool success = false;

        if (isBuyingMode)
        {
            if (selectedEntry.item == null) return;
            success = ShopManager.Instance.BuyItem(currentShop, selectedEntry.item, 1);
            if (success)
            {
                RefreshGold();
                RefreshList(); 
                OnItemSelect_Buy(selectedEntry, detailName.text); // 刷新详情页状态
            }
        }
        else
        {
            if (selectedSellItem == null) return;
            success = ShopManager.Instance.SellItem(selectedSellItem, 1);
            if (success)
            {
                RefreshGold();
                RefreshList(); 
                OnItemSelect_Sell(selectedSellItem); 
            }
        }
    }
}