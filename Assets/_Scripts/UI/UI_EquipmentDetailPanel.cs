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
                // 👇 核心改动：如果是从角色身上点开的，去【当前焦点角色】实例里查真实耐久！
                if (source == EquipmentPanelSource.CharacterSheet)
                {
                    // 优先获取正在查看的队友，如果面板没开就拿主角兜底
                    var targetChar = (UI_CharacterSheet.Instance != null && UI_CharacterSheet.Instance.CurrentFocusCharacter != null) 
                                     ? UI_CharacterSheet.Instance.CurrentFocusCharacter 
                                     : GameManager.Instance.Player;

                    if (targetChar != null && targetChar.equipmentDurability != null && targetChar.equipmentDurability.ContainsKey(equip.slotType))
                    {
                        currentDura = targetChar.equipmentDurability[equip.slotType];
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
            if (currentSource == EquipmentPanelSource.Inventory)
            {
                // 1. 如果是从主界面的大背包点开的，问玩家给谁穿！
                if (UI_TargetSelector.Instance != null)
                {
                    
                    UI_TargetSelector.Instance.OpenSelector($"请选择穿戴者：\n{currentEquip.itemName}", AvatarDisplayMode.NameOnly, (selectedTarget) => 
                    {
                        InventoryManager.Instance.EquipItemLogic(currentEquip, selectedTarget);
                        ClosePanel(); // 穿好后关闭详情面板
                    });
                }
                else
                {
                    // 兜底：直接给主角
                    InventoryManager.Instance.EquipItemLogic(currentEquip, GameManager.Instance.Player);
                    ClosePanel();
                }
            }
            else 
            {
                // 2. 如果是从角色详情面板点开的，直接穿给当前焦点角色！
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
            InventoryManager.Instance.UnequipItem(currentEquip.slotType);
        }
        ClosePanel();
    }
}