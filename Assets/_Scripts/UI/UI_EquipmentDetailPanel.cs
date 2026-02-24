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

    [Header("UI References (UI 引用)")]
    public GameObject panelRoot;
    public Image iconImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI typeText;       // 显示 "头部", "身体" 等
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI statsText;      // 动态拼接属性加成
    public TextMeshProUGUI durabilityText; // 耐久度显示

    [Header("Buttons (操作按钮)")]
    public Button btnEquip;
    public Button btnUnequip;
    public Button btnClose;

    // 记录当前正在查看的装备和来源
    private EquipmentData currentEquip;
    private EquipmentPanelSource currentSource;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        
        // 绑定按钮事件
        if (btnEquip != null) btnEquip.onClick.AddListener(OnEquipClicked);
        if (btnUnequip != null) btnUnequip.onClick.AddListener(OnUnequipClicked);
        if (btnClose != null) btnClose.onClick.AddListener(ClosePanel);

        ClosePanel(); // 初始隐藏
    }

    // --- 核心：打开面板并注入数据 ---
    public void OpenPanel(EquipmentData equip, EquipmentPanelSource source)
    {
        if (equip == null) return;

        currentEquip = equip;
        currentSource = source;

        // 1. 基础信息填入
        if (iconImage != null) { iconImage.sprite = equip.icon; iconImage.enabled = equip.icon != null; }
        if (nameText != null) nameText.text = equip.itemName;
        if (typeText != null) typeText.text = $"[ {equip.slotType} ]";
        if (descriptionText != null) descriptionText.text = equip.description;

        // 2. 动态拼接属性词条
        if (statsText != null)
        {
            string statsStr = "";
            if (equip.baseDamage > 0) statsStr += $"基础攻击力: <color=#FF5555>+{equip.baseDamage}</color>\n";
            if (equip.baseDefense > 0) statsStr += $"基础防御力: <color=#55AAFF>+{equip.baseDefense}</color>\n";
            if (equip.baseMaxHP > 0) statsStr += $"最大生命值: <color=#55FF55>+{equip.baseMaxHP}</color>\n";
            if (equip.baseMaxMP > 0) statsStr += $"最大魔法值: <color=#55FFFF>+{equip.baseMaxMP}</color>\n";

            foreach (var mod in equip.modifiers)
            {
                string sign = mod.value >= 0 ? "+" : "";
                string percent = mod.type == ModifierType.Percent ? "%" : "";
                statsStr += $"{mod.statType}: {sign}{mod.value}{percent}\n";
            }
            statsText.text = statsStr == "" ? "无附加属性" : statsStr;
        }

        // 3. 智能耐久度计算
        if (durabilityText != null)
        {
            if (equip.maxDurability <= 0)
            {
                durabilityText.text = "耐久度: <color=#AAAAAA>永不磨损</color>";
            }
            else
            {
                int currentDura = equip.maxDurability; // 默认满耐久
                // 如果是从角色身上点开的，去肉身实例里查真实耐久
                if (source == EquipmentPanelSource.CharacterSheet && GameManager.Instance != null && GameManager.Instance.Player != null)
                {
                    var player = GameManager.Instance.Player;
                    if (player.equipmentDurability != null && player.equipmentDurability.ContainsKey(equip.slotType))
                    {
                        currentDura = player.equipmentDurability[equip.slotType];
                    }
                }
                
                // 变红警告逻辑
                string colorHex = (currentDura == 0) ? "#FF0000" : (currentDura < equip.maxDurability * 0.3f ? "#FF8800" : "#FFFFFF");
                durabilityText.text = $"耐久度: <color={colorHex}>{currentDura}/{equip.maxDurability}</color>";
            }
        }

        // 4. 动态按钮互斥逻辑
        if (btnEquip != null) btnEquip.gameObject.SetActive(source == EquipmentPanelSource.Inventory);
        if (btnUnequip != null) btnUnequip.gameObject.SetActive(source == EquipmentPanelSource.CharacterSheet);

        // 开启面板
        if (panelRoot != null) panelRoot.SetActive(true);
    }

    public void ClosePanel()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        currentEquip = null;
    }

    // --- 按钮实际执行逻辑 ---
    private void OnEquipClicked()
    {
        if (currentEquip != null && InventoryManager.Instance != null)
        {
            // 绝杀 Bug：直接调用穿戴逻辑，不再经过 UseItem 的路由拦截！
            InventoryManager.Instance.EquipItemLogic(currentEquip); 
        }
        ClosePanel();
    }

    private void OnUnequipClicked()
    {
        if (currentEquip != null && InventoryManager.Instance != null)
        {
            InventoryManager.Instance.UnequipItem(currentEquip.slotType);
        }
        ClosePanel();
    }
}