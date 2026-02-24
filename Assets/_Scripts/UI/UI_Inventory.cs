using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UI_Inventory : MonoBehaviour
{
    [Header("UI References")]
    public GameObject panelRoot;
    public Transform gridContainer;
    public Button closeButton;
    
    [Header("Prefabs")]
    public GameObject slotPrefab;

    private void Start()
    {
        // 👇 新增: 主动向 UIManager 报到 (自动注册)
        if (UIManager.Instance != null)
        {
            UIManager.Instance.inventoryUI = this; 
        }
        // 初始关闭
        CloseMenu();
        
        if (closeButton != null)
            closeButton.onClick.AddListener(CloseMenu);

        // 订阅事件：一旦背包数据变了，我就重画
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged.AddListener(RefreshUI);
        }
    }

    // --- 开关逻辑 ---
    public void ToggleMenu()
    {
        if (panelRoot.activeSelf) CloseMenu();
        else OpenMenu();
    }

    public void OpenMenu()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.CloseAllMenus();
            UIManager.Instance.OnAnyMenuOpened();
        }
        panelRoot.SetActive(true);
        RefreshUI(); // 打开时刷新一次
    }

    public void CloseMenu()
    {
        panelRoot.SetActive(false);
    }

    // --- 核心：重画格子 ---
    public void RefreshUI()
    {
        foreach (Transform child in gridContainer) Destroy(child.gameObject);

        foreach (InventorySlot slot in InventoryManager.Instance.inventory)
        {
            GameObject newSlot = Instantiate(slotPrefab, gridContainer);
            
            Image iconImg = newSlot.transform.Find("Icon").GetComponent<Image>();
            TextMeshProUGUI amountText = newSlot.transform.Find("Amount").GetComponent<TextMeshProUGUI>();

            if (slot.itemData.icon != null)
            {
                iconImg.sprite = slot.itemData.icon;
                iconImg.enabled = true;
            }
            else iconImg.enabled = false;

            if (slot.amount > 1) amountText.text = slot.amount.ToString();
            else amountText.text = "";

            // --- 按钮事件注入 ---
            Button btn = newSlot.GetComponent<Button>();
            if (btn == null) btn = newSlot.AddComponent<Button>(); // 自动添加保险

            btn.onClick.AddListener(() => OnSlotClicked(slot));

            // --- 👇 新增: Tooltip 动态数据注入 ---
            UI_TooltipTrigger tooltipTrigger = newSlot.GetComponent<UI_TooltipTrigger>();
            // 如果预制体上忘了挂载，系统自动帮您补上，防止报错
            if (tooltipTrigger == null) tooltipTrigger = newSlot.AddComponent<UI_TooltipTrigger>(); 
            
            // 将当前格子的物品数据赋予触发器
            tooltipTrigger.currentItem = slot.itemData;
        }
    }

    private void OnSlotClicked(InventorySlot slot)
    {
        // 调用管理器的使用逻辑
        InventoryManager.Instance.UseItem(slot.itemData);
        
        // 如果是在战斗中，使用后通常需要关闭背包界面
        if (GameManager.Instance.CurrentState == GameState.Battle)
        {
            CloseMenu();
        }
    }
}