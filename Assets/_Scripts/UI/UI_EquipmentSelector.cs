using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UI_EquipmentSelector : MonoBehaviour
{
    public static UI_EquipmentSelector Instance { get; private set; }

    [Header("UI 引用 (UI References)")]
    public GameObject panelRoot;         
    public TextMeshProUGUI titleText;    
    public Transform gridContainer;      
    public Button closeButton;           
    public GameObject emptyPrompt;       

    [Header("预制体 (Prefabs)")]
    public GameObject slotPrefab;        

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
        if (panelRoot.activeSelf) ClosePanel();
    }

    public void OpenSelector(EquipmentSlot slot)
    {
        currentSlot = slot;
        panelRoot.SetActive(true);
        if (titleText != null) titleText.text = $"选择 {GetSlotName(slot)}";

        RefreshList();
    }

    private void RefreshList()
    {
        foreach (Transform child in gridContainer) Destroy(child.gameObject);
        bool hasItems = false;

        foreach (var invSlot in InventoryManager.Instance.inventory)
        {
            // 如果这个格子装的是实体装备，且部位对得上
            if (invSlot.equipmentInstance != null && invSlot.equipmentInstance.blueprint.slotType == currentSlot)
            {
                hasItems = true;
                GameObject go = Instantiate(slotPrefab, gridContainer);
                
                Image iconImg = go.transform.Find("Icon")?.GetComponent<Image>();
                TextMeshProUGUI amountText = go.transform.Find("Amount")?.GetComponent<TextMeshProUGUI>();

                Image bgImg = go.GetComponent<Image>();
                if (bgImg != null)
                {
                    bgImg.color = GetRarityColor(invSlot.equipmentInstance.rarity);
                }

                if (iconImg != null && invSlot.equipmentInstance.blueprint.icon != null) 
                { 
                    iconImg.sprite = invSlot.equipmentInstance.blueprint.icon; 
                    iconImg.enabled = true; 
                }
                if (amountText != null) amountText.text = ""; // 肉身必定是1个，不显示数量

                Button btn = go.GetComponent<Button>();
                if (btn == null) btn = go.AddComponent<Button>();
                
                // 👇 修复点 6：点击时，准确传递格子里的装备实例！
                btn.onClick.AddListener(() => OnEquipSelected(invSlot.equipmentInstance));

                UI_TooltipTrigger tooltip = go.GetComponent<UI_TooltipTrigger>();
                if (tooltip == null) tooltip = go.AddComponent<UI_TooltipTrigger>();
                tooltip.currentItem = invSlot.equipmentInstance.blueprint;
            }
        }

        if (emptyPrompt != null) emptyPrompt.SetActive(!hasItems);
    }

    // 👇 修复点 7：接收 RuntimeEquipment
    private void OnEquipSelected(RuntimeEquipment equipInstance)
    {
        if (UI_EquipmentDetailPanel.Instance != null)
        {
            UI_EquipmentDetailPanel.Instance.OpenPanel(equipInstance, EquipmentPanelSource.Inventory);
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
    private Color GetRarityColor(EquipmentRarity rarity)
    {
        switch (rarity)
        {
            case EquipmentRarity.Common: return Color.white; // 或者用 new Color(0.8f, 0.8f, 0.8f) 浅灰
            case EquipmentRarity.Rare: return new Color(0f, 0.63f, 1f); // 蓝色 #00A2FF
            case EquipmentRarity.Epic: return new Color(0.81f, 0.26f, 1f); // 紫色 #D042FF
            case EquipmentRarity.Legendary: return new Color(1f, 0.84f, 0f); // 金色 #FFD700
            default: return Color.white;
        }
    }
}