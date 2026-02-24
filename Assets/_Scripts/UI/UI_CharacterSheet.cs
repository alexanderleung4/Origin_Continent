using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class UI_CharacterSheet : MonoBehaviour
{
    [Header("UI References")]
    public GameObject panelRoot;
    public Button closeButton;

    [Header("Portrait (公共区域)")]
    public Image portraitImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI levelText; 
    public Slider expSlider;
    public TextMeshProUGUI expText; 

    // --- 👇 新增: 分页系统 ---
    [Header("Tabs (分页)")]
    public Button btnTabStatus; // 属性页按钮
    public Button btnTabSkills; // 技能页按钮
    public GameObject pageStatus; // 属性页内容父节点
    public GameObject pageSkills; // 技能页内容父节点

    // --- 属性页内容 (移入 PageStatus) ---
    [Header("Page: Status")]
    public TextMeshProUGUI talentText; 
    public Color normalColor = Color.white;
    public Color highlightColor = Color.yellow;
    
    // 属性文本
    public TextMeshProUGUI txtHP, txtMP, txtStamina;
    public TextMeshProUGUI txtAtk, txtDef, txtSpd, txtCritRate, txtCritDmg;

    // 加点按钮
    public Button btnPlusHP, btnPlusMP, btnPlusStamina;
    public Button btnPlusAtk, btnPlusDef, btnPlusSpd, btnPlusCritRate, btnPlusCritDmg;

    // 装备槽
    public List<Button> equipmentSlots; 
    public Sprite defaultSlotSprite; 

    // --- 👇 新增: 技能页内容 ---
    [Header("Page: Skills")]
    public Transform skillListContainer; // 技能列表的 Content
    public GameObject skillSlotPrefab;   // 拖入挂了 UI_SkillSlot 的预制体
    
    [Header("Skill Details (详情弹窗/区域)")]
    public GameObject skillDetailPanel;  // 详情面板
    public TextMeshProUGUI detailName;
    public TextMeshProUGUI detailDesc;
    public TextMeshProUGUI detailPower;  // 威力/倍率

    private void Start()
    {
        CloseMenu();
        if (closeButton != null) closeButton.onClick.AddListener(CloseMenu);
        
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged.AddListener(OnInventoryChanged);

        // 绑定分页按钮
        if (btnTabStatus) btnTabStatus.onClick.AddListener(() => SwitchTab(true));
        if (btnTabSkills) btnTabSkills.onClick.AddListener(() => SwitchTab(false));

        // 绑定加点按钮 (保持原样)
        BindButton(btnPlusHP, StatType.MaxHP);
        BindButton(btnPlusMP, StatType.MaxMP);
        BindButton(btnPlusStamina, StatType.MaxStamina);
        BindButton(btnPlusAtk, StatType.Attack);
        BindButton(btnPlusDef, StatType.Defense);
        BindButton(btnPlusSpd, StatType.Speed);
        BindButton(btnPlusCritRate, StatType.CritRate); 
        BindButton(btnPlusCritDmg, StatType.CritDamage);

        // 绑定装备槽
        for (int i = 0; i < equipmentSlots.Count; i++)
        {
            int index = i;
            if (equipmentSlots[i] != null)
            {
                equipmentSlots[i].onClick.RemoveAllListeners();
                equipmentSlots[i].onClick.AddListener(() => OnEquipmentSlotClicked(index));
            }
        }
    }

    private void OnDestroy()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged.RemoveListener(OnInventoryChanged);
    }

    // --- 交互逻辑 ---

    private void SwitchTab(bool showStatus)
    {
        if (pageStatus) pageStatus.SetActive(showStatus);
        if (pageSkills) pageSkills.SetActive(!showStatus);
        
        // 刷新当前页
        if (showStatus) RefreshUI();
        else RefreshSkills();

        // 隐藏技能详情
        if (skillDetailPanel) skillDetailPanel.SetActive(false);
    }

    // --- 显示逻辑 ---

    public void OpenMenu()
    {
        if (UIManager.Instance != null) UIManager.Instance.OnOpenPanel(); // 使用新的互斥+Blocker

        panelRoot.SetActive(true);
        SwitchTab(true); // 默认打开属性页
    }

    public void CloseMenu()
    {
        panelRoot.SetActive(false);
    }

    public void ToggleMenu()
    {
        if (panelRoot.activeSelf) CloseMenu(); else OpenMenu();
    }

    private void OnInventoryChanged()
    {
        if (panelRoot.activeSelf) 
        {
            // 如果开着，就刷新当前页
            if (pageStatus != null && pageStatus.activeSelf) RefreshUI();
            else RefreshSkills();
        }
    }

    // --- Page 1: 属性刷新 (保持原样) ---
    public void RefreshUI()
    {
        var player = GameManager.Instance.Player;
        if (player == null) return;

        // 公共信息
        if (nameText) nameText.text = player.Name;
        if (levelText) levelText.text = $"Lv.{player.Level}";
        
        if (portraitImage != null)
        {
            Sprite spriteToShow = player.data.bodySprite_Normal;
            if (spriteToShow == null) spriteToShow = player.data.portrait; 
            if (spriteToShow != null)
            {
                portraitImage.sprite = spriteToShow;
                portraitImage.preserveAspect = true;
                portraitImage.color = Color.white;
                portraitImage.enabled = true;
            }
            else portraitImage.color = Color.clear;
        }
        
        if (expSlider)
        {
            expSlider.maxValue = player.ExpRequiredForLevelUp;
            expSlider.value = player.CurrentLevelProgress;
            if (expText) expText.text = $"{player.CurrentLevelProgress} / {player.ExpRequiredForLevelUp}";
        }

        // 属性数值
        if (txtHP) txtHP.text = $"{player.CurrentHP} / {player.MaxHP}"; 
        if (txtMP) txtMP.text = $"{player.CurrentMP} / {player.MaxMP}";
        if (txtStamina) txtStamina.text = $"{player.CurrentStamina} / {player.MaxStamina}";
        if (txtAtk) txtAtk.text = player.Attack.ToString();
        if (txtDef) txtDef.text = player.Defense.ToString();
        if (txtSpd) txtSpd.text = player.Speed.ToString();
        if (txtCritRate) txtCritRate.text = $"{player.CritRate * 100:F0}%";
        if (txtCritDmg) txtCritDmg.text = $"{player.CritDamage * 100:F0}%";

        // 天赋
        bool canSpend = player.TalentPoints > 0;
        if (talentText)
        {
            talentText.text = $"天赋点: {player.TalentPoints}";
            talentText.color = canSpend ? highlightColor : normalColor;
        }
        
        SetBtnActive(btnPlusHP, canSpend);
        SetBtnActive(btnPlusMP, canSpend);
        SetBtnActive(btnPlusStamina, canSpend);
        SetBtnActive(btnPlusAtk, canSpend);
        SetBtnActive(btnPlusDef, canSpend);
        SetBtnActive(btnPlusSpd, canSpend);
        SetBtnActive(btnPlusCritRate, canSpend);
        SetBtnActive(btnPlusCritDmg, canSpend);

        // 装备
        foreach (var btn in equipmentSlots) 
        {
            if(btn != null && btn.image != null)
            {
                btn.image.sprite = defaultSlotSprite; 
                btn.image.color = (defaultSlotSprite == null) ? Color.clear : Color.white; 
                // 👇 新增: 清空可能残留的 Tooltip 数据
                UI_TooltipTrigger tooltip = btn.GetComponent<UI_TooltipTrigger>();
                if (tooltip != null) tooltip.currentItem = null;
            }
        }
        foreach (var kvp in player.equipment)
        {
            int index = GetSlotIndex(kvp.Key);
            if (index >= 0 && index < equipmentSlots.Count)
            {
                Button btn = equipmentSlots[index];
                if (btn != null && btn.image != null)
                {
                    btn.image.sprite = kvp.Value.icon;
                    btn.image.color = Color.white;
                    // 👇 新增: 注入真实的装备数据给 Tooltip
                    UI_TooltipTrigger tooltip = btn.GetComponent<UI_TooltipTrigger>();
                    if (tooltip == null) tooltip = btn.gameObject.AddComponent<UI_TooltipTrigger>();
                    tooltip.currentItem = kvp.Value;
                }
            }
        }
    }

    // --- 👇 Page 2: 技能刷新 (新增) ---
    public void RefreshSkills()
    {
        if (skillListContainer == null || skillSlotPrefab == null) return;

        // 1. 清空列表
        foreach (Transform child in skillListContainer) Destroy(child.gameObject);

        var player = GameManager.Instance.Player;
        if (player == null) return;

        // 2. 生成技能 (目前使用 startingSkills，未来可改为 learnedSkills)
        foreach (var skill in player.data.startingSkills)
        {
            GameObject go = Instantiate(skillSlotPrefab, skillListContainer);
            UI_SkillSlot slot = go.GetComponent<UI_SkillSlot>();
            if (slot != null)
            {
                slot.Setup(skill, OnSkillSelected);
            }
        }
    }

    private void OnSkillSelected(SkillData skill)
    {
        if (skillDetailPanel != null)
        {
            skillDetailPanel.SetActive(true);
            if (detailName) detailName.text = skill.skillName;
            if (detailDesc) detailDesc.text = skill.description;

            // 👇 动态解析 Skill Effects 列表，生成描述文本
            if (detailPower != null)
            {
                if (skill.effects == null || skill.effects.Count == 0)
                {
                    detailPower.text = "暂无具体效果数据。";
                    return;
                }

                string powerStr = "";
                for (int i = 0; i < skill.effects.Count; i++)
                {
                    SkillEffect effect = skill.effects[i];
                    string targetStr = effect.effectTarget == EffectTarget.Self ? "自身" : "目标";
                    
                    // 拼接属性加成文本 (例: "100% 攻击力加成")
                    string scalingStr = "";
                    if (effect.scalingStat != ScalingStat.None && effect.scalingMultiplier > 0)
                    {
                        scalingStr = $" + {effect.scalingMultiplier * 100}% {GetStatName(effect.scalingStat)}加成";
                    }

                    if (effect.effectType == EffectType.Damage)
                    {
                        powerStr += $"【效果{i+1}】: 对{targetStr}造成 {effect.baseValue}{scalingStr} 伤害";
                        if (effect.hitCount > 1) powerStr += $" (共 {effect.hitCount} 段)";
                        if (effect.lifestealPercent > 0) powerStr += $"\n ↳ 附带 {effect.lifestealPercent * 100}% 吸血";
                    }
                    else if (effect.effectType == EffectType.Heal)
                    {
                        powerStr += $"【效果{i+1}】: 为{targetStr}恢复 {effect.baseValue}{scalingStr} 生命值";
                    }
                    else if (effect.effectType == EffectType.ApplyBuff)
                    {
                        if (effect.buffToApply != null)
                        {
                            BuffData b = effect.buffToApply;
                            string buffDesc = "";
                            
                            if (b.type == BuffType.DamageReduction) 
                                buffDesc = $"免伤 {b.baseValue}%";
                            else if (b.type == BuffType.Shield) 
                                buffDesc = $"护盾值 {b.baseValue}{scalingStr}";
                            else if (b.type == BuffType.StatBoost_Attack) 
                                buffDesc = $"提升攻击力 {b.baseValue}{scalingStr}";
                            else if (b.type == BuffType.StatBoost_Defense) 
                                buffDesc = $"提升防御力 {b.baseValue}{scalingStr}";

                            powerStr += $"【效果{i+1}】: 为{targetStr}施加 [{b.buffName}] ({buffDesc}，持续 {b.durationTurns} 回合)";
                        }
                        else
                        {
                            powerStr += $"【效果{i+1}】: 为{targetStr}施加状态 (未配置Buff数据)";
                        }
                    }
                    
                    powerStr += "\n";
                }
                detailPower.text = powerStr;
            }
        }
    }

    // 辅助方法：把枚举翻译成中文文本
    private string GetStatName(ScalingStat stat)
    {
        switch (stat)
        {
            case ScalingStat.Attack: return "攻击力";
            case ScalingStat.Defense: return "防御力";
            case ScalingStat.MaxHP: return "最大生命";
            case ScalingStat.CurrentHP: return "当前生命";
            case ScalingStat.MaxMP: return "最大法力";
            case ScalingStat.CurrentMP: return "当前法力";
            case ScalingStat.Speed: return "速度";
            default: return "";
        }
    }

    // --- 辅助方法 ---
    private void BindButton(Button btn, StatType type)
    {
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnPlusClicked(type));
        }
    }

    private void OnPlusClicked(StatType type)
    {
        var player = GameManager.Instance.Player;
        if (player != null) { player.SpendTalent(type); RefreshUI(); }
    }

    private void OnEquipmentSlotClicked(int index)
    {
        EquipmentSlot slot = GetSlotByIndex(index);
        var player = GameManager.Instance.Player;
        if (player.equipment.ContainsKey(slot)) 
        { 
            // 获取角色身上这件真实的装备（带耐久度损耗的）
            EquipmentData equip = player.equipment[slot];
            
            // 劫持路由：不再直接卸下，而是打开详情面板！
            if (UI_EquipmentDetailPanel.Instance != null)
            {
                UI_EquipmentDetailPanel.Instance.OpenPanel(equip, EquipmentPanelSource.CharacterSheet);
            }
        }
    }

    private void SetBtnActive(Button btn, bool active)
    {
        if (btn != null) btn.gameObject.SetActive(active);
    }

    private int GetSlotIndex(EquipmentSlot slot)
    {
        switch (slot)
        {
            case EquipmentSlot.Weapon: return 0;
            case EquipmentSlot.Head: return 1;
            case EquipmentSlot.Body: return 2;
            case EquipmentSlot.Legs: return 3;
            case EquipmentSlot.Feet: return 4;
            case EquipmentSlot.Neck: return 5;
            case EquipmentSlot.Hands: return 6;
            default: return -1;
        }
    }
    
    private EquipmentSlot GetSlotByIndex(int index)
    {
        switch (index)
        {
            case 0: return EquipmentSlot.Weapon;
            case 1: return EquipmentSlot.Head;
            case 2: return EquipmentSlot.Body;
            case 3: return EquipmentSlot.Legs;
            case 4: return EquipmentSlot.Feet;
            case 5: return EquipmentSlot.Neck;
            case 6: return EquipmentSlot.Hands;
            default: return EquipmentSlot.Weapon; 
        }
    }
}