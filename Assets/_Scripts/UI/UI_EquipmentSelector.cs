using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UI_EquipmentSelector : MonoBehaviour
{
    public static UI_EquipmentSelector Instance { get; private set; }

    [Header("UI 引用 (UI References)")]
    public GameObject panelRoot;         // 面板的根节点
    public TextMeshProUGUI titleText;    // 标题 (如: "选择武器")
    public Transform gridContainer;      // 挂载 GridLayoutGroup 的列表父节点
    public Button closeButton;           // 关闭按钮
    public GameObject emptyPrompt;       // (可选) 背包里没有该部位装备时的提示文字

    [Header("预制体 (Prefabs)")]
    public GameObject slotPrefab;        // 直接复用您 UI_Inventory 里的格子预制体！

    private EquipmentSlot currentSlot;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        ClosePanel();
        if (closeButton != null) closeButton.onClick.AddListener(ClosePanel);
        
        // 核心联动：只要背包发生了变化（比如我们点击了穿戴），这个筛选器就功成身退自动关闭！
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged.AddListener(OnInventoryChanged);
    }
    
    private void OnDestroy()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged.RemoveListener(OnInventoryChanged);
    }

    private void OnInventoryChanged()
    {
        // 如果侦测到穿戴完成导致背包变动，自动关掉自己，把视线还给角色大面板
        if (panelRoot.activeSelf) ClosePanel();
    }

    // 唤出筛选器
    public void OpenSelector(EquipmentSlot slot)
    {
        currentSlot = slot;
        panelRoot.SetActive(true);
        if (titleText != null) titleText.text = $"选择 {GetSlotName(slot)}";

        RefreshList();
    }

    private void RefreshList()
    {
        // 1. 清空旧格子
        foreach (Transform child in gridContainer) Destroy(child.gameObject);

        bool hasItems = false;

        // 2. 遍历大背包，进行精准过滤！
        foreach (var invSlot in InventoryManager.Instance.inventory)
        {
            // 只要它是装备，且部位对得上
            if (invSlot.itemData is EquipmentData equipData && equipData.slotType == currentSlot)
            {
                hasItems = true;
                GameObject go = Instantiate(slotPrefab, gridContainer);
                
                // 复用旧格子的结构
                Image iconImg = go.transform.Find("Icon")?.GetComponent<Image>();
                TextMeshProUGUI amountText = go.transform.Find("Amount")?.GetComponent<TextMeshProUGUI>();

                if (iconImg != null && equipData.icon != null) { iconImg.sprite = equipData.icon; iconImg.enabled = true; }
                if (amountText != null) amountText.text = invSlot.amount > 1 ? invSlot.amount.ToString() : "";

                Button btn = go.GetComponent<Button>();
                if (btn == null) btn = go.AddComponent<Button>();
                
                // 👇 绝杀逻辑：点击后直接呼出装备详情大面板！伪装成从 Inventory 打开的，以此激活【装备】按钮
                btn.onClick.AddListener(() => OnEquipSelected(equipData));

                // 悬浮提示
                UI_TooltipTrigger tooltip = go.GetComponent<UI_TooltipTrigger>();
                if (tooltip == null) tooltip = go.AddComponent<UI_TooltipTrigger>();
                tooltip.currentItem = equipData;
            }
        }

        if (emptyPrompt != null) emptyPrompt.SetActive(!hasItems);
    }

    private void OnEquipSelected(EquipmentData equipData)
    {
        if (UI_EquipmentDetailPanel.Instance != null)
        {
            // 呼叫详情大面板！
            UI_EquipmentDetailPanel.Instance.OpenPanel(equipData, EquipmentPanelSource.Inventory);
        }
    }

    public void ClosePanel()
    {
        panelRoot.SetActive(false);
    }

    private string GetSlotName(EquipmentSlot slot)
    {
        switch (slot) {
            case EquipmentSlot.Weapon: return "武器";
            case EquipmentSlot.Head: return "头部防具";
            case EquipmentSlot.Body: return "身体防具";
            case EquipmentSlot.Legs: return "腿部防具";
            case EquipmentSlot.Feet: return "足部防具";
            case EquipmentSlot.Neck: return "项链";
            case EquipmentSlot.Hands: return "手套";
            default: return "装备";
        }
    }
}