using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class UI_CharacterSheet : MonoBehaviour
{
    public static UI_CharacterSheet Instance { get; private set; } 

    [Header("UI References")]
    public GameObject panelRoot;
    public Button closeButton;

    [Header("Drill-down Navigation (大厅与详情页)")]
    public GameObject pageRoster;        
    public GameObject pageDetail;        
    public Button btnBackToRoster;       

    public Transform rosterGridContainer;
    public GameObject rosterCardPrefab;  

    [Header("Party Roster (详情页侧边快速切换栏)")]
    public Transform rosterContainer;        
    public GameObject rosterAvatarPrefab;    
    public RuntimeCharacter CurrentFocusCharacter { get; private set; } 

    [Header("Portrait (公共区域)")]
    public Image portraitImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI levelText; 
    public Slider expSlider;
    public TextMeshProUGUI expText; 

    [Header("Tabs (分页)")]
    public Button btnTabStatus; 
    public Button btnTabSkills; 
    public GameObject pageStatus; 
    public GameObject pageSkills; 

    [Header("Page: Status")]
    public TextMeshProUGUI talentText; 
    public Color normalColor = Color.white;
    public Color highlightColor = Color.yellow;
    
    public TextMeshProUGUI txtHP, txtMP, txtStamina;
    public TextMeshProUGUI txtAtk, txtDef, txtSpd, txtCritRate, txtCritDmg;

    public Button btnPlusHP, btnPlusMP, btnPlusStamina;
    public Button btnPlusAtk, btnPlusDef, btnPlusSpd, btnPlusCritRate, btnPlusCritDmg;

    public List<Button> equipmentSlots; 
    public Sprite defaultSlotSprite; 

    [Header("Traits (被动特质)")]
    public Transform traitListContainer; 
    public GameObject traitSlotPrefab;   

    [Header("Page: Skills")]
    public Transform skillListContainer; 
    public GameObject skillSlotPrefab;   
    
    [Header("Skill Details (详情弹窗/区域)")]
    public GameObject skillDetailPanel;  
    public TextMeshProUGUI detailName;
    public TextMeshProUGUI detailDesc;
    public TextMeshProUGUI detailPower;  

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        CloseMenu();
        if (closeButton != null) closeButton.onClick.AddListener(CloseMenu);
        if (btnBackToRoster != null) btnBackToRoster.onClick.AddListener(OpenRosterHall);

        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged.AddListener(OnInventoryChanged);

        if (btnTabStatus) btnTabStatus.onClick.AddListener(() => SwitchTab(true));
        if (btnTabSkills) btnTabSkills.onClick.AddListener(() => SwitchTab(false));

        BindButton(btnPlusHP, StatType.MaxHP);
        BindButton(btnPlusMP, StatType.MaxMP);
        BindButton(btnPlusStamina, StatType.MaxStamina);
        BindButton(btnPlusAtk, StatType.Attack);
        BindButton(btnPlusDef, StatType.Defense);
        BindButton(btnPlusSpd, StatType.Speed);
        BindButton(btnPlusCritRate, StatType.CritRate); 
        BindButton(btnPlusCritDmg, StatType.CritDamage);

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

    public void OpenMenu()
    {
        if (UIManager.Instance != null) UIManager.Instance.OnOpenPanel(); 
        panelRoot.SetActive(true);
        OpenRosterHall(); 
    }

    public void CloseMenu() { panelRoot.SetActive(false); }
    public void ToggleMenu() { if (panelRoot.activeSelf) CloseMenu(); else OpenMenu(); }

    private void OpenRosterHall()
    {
        if (pageRoster) pageRoster.SetActive(true);
        if (pageDetail) pageDetail.SetActive(false); 
        if (skillDetailPanel) skillDetailPanel.SetActive(false);
        RefreshRosterHall();
    }

    private void RefreshRosterHall()
    {
        if (rosterGridContainer == null || rosterCardPrefab == null) return;
        foreach (Transform child in rosterGridContainer) Destroy(child.gameObject);

        var party = GameManager.Instance.activeParty; 
        if (party == null) return;

        foreach (var member in party)
        {
            if (member == null || member.data == null) continue;

            GameObject card = Instantiate(rosterCardPrefab, rosterGridContainer);
            
            Image img = card.transform.Find("Image_Portrait")?.GetComponent<Image>();
            if (img == null) img = card.GetComponent<Image>(); 
            if (img != null && member.data.portrait != null) img.sprite = member.data.portrait;

            TextMeshProUGUI nameTxt = card.transform.Find("Text_Name")?.GetComponent<TextMeshProUGUI>();
            if (nameTxt != null) nameTxt.text = member.Name;

            TextMeshProUGUI lvTxt = card.transform.Find("Text_Level")?.GetComponent<TextMeshProUGUI>();
            if (lvTxt != null) lvTxt.text = $"Lv.{member.Level}";

            Button btn = card.GetComponent<Button>();
            if (btn != null) btn.onClick.AddListener(() => OpenDetail(member));
        }
    }

    private void OpenDetail(RuntimeCharacter target)
    {
        if (pageRoster) pageRoster.SetActive(false);
        if (pageDetail) pageDetail.SetActive(true);

        SetFocusCharacter(target);
        RefreshSideRosterUI(); 
        SwitchTab(true);       
    }

    public void SetFocusCharacter(RuntimeCharacter target)
    {
        if (target == null) return;
        CurrentFocusCharacter = target;
        
        if (pageStatus != null && pageStatus.activeSelf) RefreshUI();
        else RefreshSkills();
    }

    private void RefreshSideRosterUI()
    {
        if (rosterContainer == null || rosterAvatarPrefab == null) return;
        foreach (Transform child in rosterContainer) Destroy(child.gameObject);
        var party = GameManager.Instance.activeParty;
        if (party == null) return;

        foreach (var member in party)
        {
            if (member == null || member.data == null) continue;
            GameObject go = Instantiate(rosterAvatarPrefab, rosterContainer);
           UI_RosterAvatar avatarUI = go.GetComponent<UI_RosterAvatar>();
            if (avatarUI != null)
            {
                avatarUI.Setup(member, AvatarDisplayMode.Minimal, SetFocusCharacter);
            }
        }
    }

    private void SwitchTab(bool showStatus)
    {
        if (pageStatus) pageStatus.SetActive(showStatus);
        if (pageSkills) pageSkills.SetActive(!showStatus);
        
        if (showStatus) RefreshUI();
        else RefreshSkills();

        if (skillDetailPanel) skillDetailPanel.SetActive(false);
    }

    private void OnInventoryChanged()
    {
        if (panelRoot.activeSelf) 
        {
            if (pageStatus != null && pageStatus.activeSelf) RefreshUI();
            else RefreshSkills();
        }
    }

    public void RefreshUI()
    {
        var player = CurrentFocusCharacter; 
        if (player == null) return;

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

        if (txtHP) txtHP.text = $"{player.CurrentHP} / {player.MaxHP}"; 
        if (txtMP) txtMP.text = $"{player.CurrentMP} / {player.MaxMP}";
        if (txtStamina) txtStamina.text = $"{player.CurrentStamina} / {player.MaxStamina}";
        if (txtAtk) txtAtk.text = player.Attack.ToString();
        if (txtDef) txtDef.text = player.Defense.ToString();
        if (txtSpd) txtSpd.text = player.Speed.ToString();
        if (txtCritRate) txtCritRate.text = $"{player.CritRate * 100:F0}%";
        if (txtCritDmg) txtCritDmg.text = $"{player.CritDamage * 100:F0}%";

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

        foreach (var btn in equipmentSlots) 
        {
            if(btn != null && btn.image != null)
            {
                btn.image.sprite = defaultSlotSprite; 
                btn.image.color = (defaultSlotSprite == null) ? Color.clear : Color.white; 
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
                    // 👇 修复点 3 & 4：提取肉身内的 blueprint 来获取图标
                    btn.image.sprite = kvp.Value.blueprint.icon;
                    btn.image.color = Color.white;
                    UI_TooltipTrigger tooltip = btn.GetComponent<UI_TooltipTrigger>();
                    if (tooltip == null) tooltip = btn.gameObject.AddComponent<UI_TooltipTrigger>();
                    tooltip.currentItem = kvp.Value.blueprint;
                }
            }
        }

        if (traitListContainer != null && traitSlotPrefab != null)
        {
            foreach (Transform child in traitListContainer) Destroy(child.gameObject);

            foreach (var trait in player.traits)
            {
                if (trait.data == null) continue;
                GameObject go = Instantiate(traitSlotPrefab, traitListContainer);
                UI_TraitSlot slot = go.GetComponent<UI_TraitSlot>();
                if (slot != null) slot.Setup(trait, OnTraitSelected);
            }
        }
    }

    public void RefreshSkills()
    {
        if (skillListContainer == null || skillSlotPrefab == null) return;
        foreach (Transform child in skillListContainer) Destroy(child.gameObject);

        var player = CurrentFocusCharacter;
        if (player == null) return;

        foreach (var skill in player.data.startingSkills)
        {
            GameObject go = Instantiate(skillSlotPrefab, skillListContainer);
            UI_SkillSlot slot = go.GetComponent<UI_SkillSlot>();
            if (slot != null) slot.Setup(skill, OnSkillSelected);
        }
    }

    private void OnSkillSelected(SkillData skill)
    {
        if (skillDetailPanel != null)
        {
            skillDetailPanel.SetActive(true);
            if (detailName) detailName.text = skill.skillName;
            if (detailDesc) detailDesc.text = skill.description;

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
                            
                            if (b.type == BuffType.DamageReduction) buffDesc = $"免伤 {b.baseValue}%";
                            else if (b.type == BuffType.Shield) buffDesc = $"护盾值 {b.baseValue}{scalingStr}";
                            else if (b.type == BuffType.StatBoost_Attack) buffDesc = $"提升攻击力 {b.baseValue}{scalingStr}";
                            else if (b.type == BuffType.StatBoost_Defense) buffDesc = $"提升防御力 {b.baseValue}{scalingStr}";

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

    private void OnTraitSelected(RuntimeCharacter.ActiveTrait trait)
    {
        if (skillDetailPanel != null)
        {
            skillDetailPanel.SetActive(true);
            if (detailName) detailName.text = $"{trait.data.traitName} (Lv.{trait.level})";
            
            string desc = trait.data.baseDescription;
            if (!trait.data.isPermanent && trait.remainingDays > 0)
            {
                desc += $"\n<color=#FFAA00>剩余时间: {trait.remainingDays} 天</color>";
            }
            if (detailDesc) detailDesc.text = desc;

            if (detailPower != null)
            {
                if (trait.level > 0 && trait.level <= trait.data.levels.Count)
                {
                    string effectText = trait.data.levels[trait.level - 1].levelDescription;
                    detailPower.text = string.IsNullOrEmpty(effectText) ? "被动效果已生效。" : effectText;
                }
                else
                {
                    detailPower.text = "";
                }
            }
        }
    }

    private string GetStatName(ScalingStat stat)
    {
        switch (stat) { case ScalingStat.Attack: return "攻击力"; case ScalingStat.Defense: return "防御力"; case ScalingStat.MaxHP: return "最大生命"; case ScalingStat.CurrentHP: return "当前生命"; case ScalingStat.MaxMP: return "最大法力"; case ScalingStat.CurrentMP: return "当前法力"; case ScalingStat.Speed: return "速度"; default: return ""; }
    }

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
        var player = CurrentFocusCharacter;
        if (player != null) { player.SpendTalent(type); RefreshUI(); }
    }

    private void OnEquipmentSlotClicked(int index)
    {
        EquipmentSlot slot = GetSlotByIndex(index);
        var player = CurrentFocusCharacter; 
        
        if (player.equipment.ContainsKey(slot)) 
        { 
            // 👇 修复点 5：正确提取实体肉身
            RuntimeEquipment equip = player.equipment[slot];
            if (UI_EquipmentDetailPanel.Instance != null)
            {
                UI_EquipmentDetailPanel.Instance.OpenPanel(equip, EquipmentPanelSource.CharacterSheet);
            }
        }
        else
        {
            if (UI_EquipmentSelector.Instance != null)
            {
                UI_EquipmentSelector.Instance.OpenSelector(slot);
            }
        }
    }

    private void SetBtnActive(Button btn, bool active)
    {
        if (btn != null) btn.gameObject.SetActive(active);
    }

    private int GetSlotIndex(EquipmentSlot slot)
    {
        switch (slot) { case EquipmentSlot.Weapon: return 0; case EquipmentSlot.Head: return 1; case EquipmentSlot.Body: return 2; case EquipmentSlot.Legs: return 3; case EquipmentSlot.Feet: return 4; case EquipmentSlot.Neck: return 5; case EquipmentSlot.Hands: return 6; default: return -1; }
    }
    
    private EquipmentSlot GetSlotByIndex(int index)
    {
        switch (index) { case 0: return EquipmentSlot.Weapon; case 1: return EquipmentSlot.Head; case 2: return EquipmentSlot.Body; case 3: return EquipmentSlot.Legs; case 4: return EquipmentSlot.Feet; case 5: return EquipmentSlot.Neck; case 6: return EquipmentSlot.Hands; default: return EquipmentSlot.Weapon; }
    }
}