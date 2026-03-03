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
        if (UIManager.Instance != null)
        {
            UIManager.Instance.inventoryUI = this; 
        }
        CloseMenu();
        
        if (closeButton != null)
            closeButton.onClick.AddListener(CloseMenu);

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged.AddListener(RefreshUI);
        }
    }

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
        RefreshUI(); 
    }

    public void CloseMenu()
    {
        panelRoot.SetActive(false);
    }

    public void RefreshUI()
    {
        foreach (Transform child in gridContainer) Destroy(child.gameObject);

        foreach (InventorySlot slot in InventoryManager.Instance.inventory)
        {
            GameObject newSlot = Instantiate(slotPrefab, gridContainer);
            
            Image iconImg = newSlot.transform.Find("Icon").GetComponent<Image>();
            TextMeshProUGUI amountText = newSlot.transform.Find("Amount").GetComponent<TextMeshProUGUI>();

            // ==========================================
            // 👇 核心改动：获取格子自身的背景图，进行神锻品质染色！
            // ==========================================
            Image bgImg = newSlot.GetComponent<Image>();
            if (bgImg != null)
            {
                // 如果这个格子里装的是“实体肉身装备”，则根据品质上色
                if (slot.equipmentInstance != null)
                {
                    bgImg.color = GetRarityColor(slot.equipmentInstance.rarity);
                }
                else
                {
                    // 普通材料或药水，保持默认底色（白色）
                    bgImg.color = Color.white; 
                }
            }
            // ==========================================

            if (slot.itemData.icon != null)
            {
                iconImg.sprite = slot.itemData.icon;
                iconImg.enabled = true;
            }
            else iconImg.enabled = false;

            // 如果是装备(肉身)，通常 amount 是 1，这里会自动隐藏数字
            if (slot.amount > 1) amountText.text = slot.amount.ToString();
            else amountText.text = "";

            Button btn = newSlot.GetComponent<Button>();
            if (btn == null) btn = newSlot.AddComponent<Button>(); 

            btn.onClick.AddListener(() => OnSlotClicked(slot));

            UI_TooltipTrigger tooltipTrigger = newSlot.GetComponent<UI_TooltipTrigger>();
            if (tooltipTrigger == null) tooltipTrigger = newSlot.AddComponent<UI_TooltipTrigger>(); 
            
            // 💡 顺带一提：如果是装备，把蓝图传给 Tooltip，如果是材料就传 itemData
            tooltipTrigger.currentItem = slot.equipmentInstance != null ? slot.equipmentInstance.blueprint : slot.itemData;
        }
    }

    private void OnSlotClicked(InventorySlot slot)
    {
        InventoryManager.Instance.UseSlot(slot);
        
        if (GameManager.Instance.CurrentState == GameState.Battle)
        {
            CloseMenu();
        }
    }

    // 👇 新增：将枚举转换为真实的 Unity Color (经典刷子游戏配色)
    private Color GetRarityColor(EquipmentRarity rarity)
    {
        switch (rarity)
        {
            case EquipmentRarity.Common: return Color.white; 
            case EquipmentRarity.Rare: return new Color(0f, 0.63f, 1f); // 稀有 - 亮蓝
            case EquipmentRarity.Epic: return new Color(0.81f, 0.26f, 1f); // 史诗 - 紫色
            case EquipmentRarity.Legendary: return new Color(1f, 0.84f, 0f); // 传说 - 金色
            default: return Color.white;
        }
    }
}