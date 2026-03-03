using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum EquipmentPanelSource
{
    Inventory,      // 从背包打开
    CharacterSheet  // 从角色面板打开
}

public class UI_EquipmentDetailPanel : MonoBehaviour
{
    public static UI_EquipmentDetailPanel Instance { get; private set; }

    [Header("UI References")]
    public GameObject panelRoot;
    public Image iconImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI typeText;       
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI statsText;      
    public TextMeshProUGUI durabilityText; 

    [Header("Buttons")]
    public Button btnEquip;
    public Button btnUnequip;
    public Button btnSalvage;
    public Button btnClose;

    // 👇 核心脱壳：UI现在全权读取肉身对象
    private RuntimeEquipment currentEquip;
    private EquipmentPanelSource currentSource;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        
        if (btnEquip != null) btnEquip.onClick.AddListener(OnEquipClicked);
        if (btnUnequip != null) btnUnequip.onClick.AddListener(OnUnequipClicked);
        if (btnClose != null) btnClose.onClick.AddListener(ClosePanel);
        if (btnSalvage != null) btnSalvage.onClick.AddListener(OnSalvageClicked);

        ClosePanel(); 
    }

    // 辅助颜色转换
    private string GetRarityColor(EquipmentRarity rarity)
    {
        switch (rarity)
        {
            case EquipmentRarity.Common: return "#FFFFFF"; // 白
            case EquipmentRarity.Rare: return "#00A2FF";   // 蓝
            case EquipmentRarity.Epic: return "#D042FF";   // 紫
            case EquipmentRarity.Legendary: return "#FFD700"; // 金
            default: return "#FFFFFF";
        }
    }

    private string GetRarityName(EquipmentRarity rarity)
    {
        switch (rarity)
        {
            case EquipmentRarity.Common: return "普通";
            case EquipmentRarity.Rare: return "稀有";
            case EquipmentRarity.Epic: return "史诗";
            case EquipmentRarity.Legendary: return "传说";
            default: return "普通";
        }
    }

    // --- 核心：打开面板并注入肉身数据 ---
    public void OpenPanel(RuntimeEquipment equip, EquipmentPanelSource source)
    {
        if (equip == null) return;

        currentEquip = equip;
        currentSource = source;

        // 1. 基础信息填入 (带有品质颜色和强化等级)
        if (iconImage != null) { iconImage.sprite = equip.blueprint.icon; iconImage.enabled = equip.blueprint.icon != null; }
        
        string rarityColor = GetRarityColor(equip.rarity);
        string levelStr = equip.level > 0 ? $" +{equip.level}" : "";
        if (nameText != null) nameText.text = $"<color={rarityColor}>{equip.blueprint.itemName}{levelStr}</color>";
        
        if (typeText != null) typeText.text = $"[ {equip.blueprint.slotType} ] - {GetRarityName(equip.rarity)}";
        if (descriptionText != null) descriptionText.text = equip.blueprint.description;

        // 2. 动态拼接属性词条 (读取实例化过后的 Dynamic 属性)
        if (statsText != null)
        {
            string statsStr = "";
            if (equip.DynamicDamage > 0) statsStr += $"攻击力: <color=#FF5555>+{equip.DynamicDamage}</color>\n";
            if (equip.DynamicDefense > 0) statsStr += $"防御力: <color=#55AAFF>+{equip.DynamicDefense}</color>\n";
            if (equip.DynamicMaxHP > 0) statsStr += $"最大生命值: <color=#55FF55>+{equip.DynamicMaxHP}</color>\n";
            if (equip.DynamicMaxMP > 0) statsStr += $"最大魔法值: <color=#55FFFF>+{equip.DynamicMaxMP}</color>\n";

            // 拼接随机词条 Affixes
            if (equip.affixes.Count > 0)
            {
                statsStr += "\n<color=#FFD700>--- 附加词条 ---</color>\n";
                foreach (var affix in equip.affixes)
                {
                    string sign = affix.value >= 0 ? "+" : "";
                    string percent = affix.isPercent ? "%" : "";
                    statsStr += $"♦ {affix.statType}: {sign}{affix.value}{percent}\n";
                }
            }

            statsText.text = statsStr == "" ? "无附加属性" : statsStr;
        }

        // 3. 智能耐久度计算 (直接读肉身)
        if (durabilityText != null)
        {
            if (equip.blueprint.maxDurability <= 0)
            {
                durabilityText.text = "耐久度: <color=#AAAAAA>永不磨损</color>";
            }
            else
            {
                int currentDura = equip.currentDurability;
                string colorHex = (currentDura == 0) ? "#FF0000" : (currentDura < equip.blueprint.maxDurability * 0.3f ? "#FF8800" : "#FFFFFF");
                durabilityText.text = $"耐久度: <color={colorHex}>{currentDura}/{equip.blueprint.maxDurability}</color>";
            }
        }

        // 4. 动态按钮互斥逻辑
        if (btnEquip != null) btnEquip.gameObject.SetActive(source == EquipmentPanelSource.Inventory);
        if (btnUnequip != null) btnUnequip.gameObject.SetActive(source == EquipmentPanelSource.CharacterSheet);
        // 👇 新增：只有从背包点开时，才允许显示拆解按钮 (身上穿的不能直接拆)
        if (btnSalvage != null) btnSalvage.gameObject.SetActive(source == EquipmentPanelSource.Inventory);

        if (panelRoot != null) panelRoot.SetActive(true);
    }

    public void ClosePanel()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        currentEquip = null;
    }

    private void OnEquipClicked()
    {
        if (currentEquip != null && InventoryManager.Instance != null)
        {
            if (currentSource == EquipmentPanelSource.Inventory)
            {
                if (UI_TargetSelector.Instance != null)
                {
                    UI_TargetSelector.Instance.OpenSelector($"请选择穿戴者：\n{currentEquip.blueprint.itemName}", AvatarDisplayMode.NameOnly, (selectedTarget) => 
                    {
                        InventoryManager.Instance.EquipItemLogic(currentEquip, selectedTarget);
                        ClosePanel(); 
                    });
                }
                else
                {
                    InventoryManager.Instance.EquipItemLogic(currentEquip, GameManager.Instance.Player);
                    ClosePanel();
                }
            }
            else 
            {
                var targetChar = UI_CharacterSheet.Instance.CurrentFocusCharacter;
                InventoryManager.Instance.EquipItemLogic(currentEquip, targetChar);
                ClosePanel();
            }
        }
    }

    private void OnUnequipClicked()
    {
        if (currentEquip != null && InventoryManager.Instance != null)
        {
            InventoryManager.Instance.UnequipItem(currentEquip.blueprint.slotType);
        }
        ClosePanel();
    }

    // 👇 新增：执行拆解！
    private void OnSalvageClicked()
    {
        if (currentEquip != null && ForgeManager.Instance != null)
        {
            bool success = ForgeManager.Instance.SalvageEquipment(currentEquip);
            if (success)
            {
                ClosePanel(); // 拆解成功，装备灰飞烟灭，关闭面板
            }
        }
    }
}