using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class UI_EnhanceTab : MonoBehaviour
{
    [Header("左侧：可强化装备列表")]
    public Transform equipListContainer;
    public GameObject equipSlotPrefab; // 直接复用 UI_Inventory 里的格子预制体

    [Header("右侧上：目标装备展示")]
    public GameObject rightPanelContent; // 用于在没选中装备时隐藏右侧内容
    public Image targetIcon;
    public TextMeshProUGUI targetName;
    public TextMeshProUGUI targetLevel;
    public TextMeshProUGUI statPreview; 
    public Slider expSlider;
    public TextMeshProUGUI expText;

    [Header("右侧下：狗粮与操作")]
    public Transform fodderListContainer;
    public GameObject fodderSlotPrefab; // 复用你的材料格子预制体
    public TextMeshProUGUI costText;
    public Button btnAutoSelect;
    public Button btnConfirmEnhance;

    // 内部状态数据
    private RuntimeEquipment currentTarget;
    private Dictionary<InventorySlot, int> selectedFodders = new Dictionary<InventorySlot, int>();
    private int lastAwakenedAffixIndex = -1;

    private void Start()
    {
        if (btnAutoSelect != null) btnAutoSelect.onClick.AddListener(OnAutoSelectClicked);
        if (btnConfirmEnhance != null) btnConfirmEnhance.onClick.AddListener(OnConfirmEnhanceClicked);
    }

    /// <summary>
    /// 当玩家从铁匠铺切换到“强化”分页时调用
    /// </summary>
    public void OnTabOpened()
    {
        currentTarget = null;
        selectedFodders.Clear();
        lastAwakenedAffixIndex = -1;
        RefreshLeftEquipList();
        RefreshRightPanel();
    }

    // --- 左侧逻辑 ---
    private void RefreshLeftEquipList()
    {
        if (equipListContainer == null || equipSlotPrefab == null) return;
        foreach (Transform child in equipListContainer) Destroy(child.gameObject);

        if (InventoryManager.Instance == null) return;

        foreach (InventorySlot slot in InventoryManager.Instance.inventory)
        {
            // 只显示可以强化的实体肉身装备
            if (slot.equipmentInstance != null)
            {
                GameObject go = Instantiate(equipSlotPrefab, equipListContainer);
                
                // 染色、图标等基础装配 (复用背包的逻辑)
                Image bgImg = go.GetComponent<Image>();
                if (bgImg != null) bgImg.color = GetRarityColor(slot.equipmentInstance.rarity);

                Image iconImg = go.transform.Find("Icon")?.GetComponent<Image>();
                if (iconImg != null && slot.equipmentInstance.blueprint.icon != null)
                {
                    iconImg.sprite = slot.equipmentInstance.blueprint.icon;
                    iconImg.enabled = true;
                }

                TextMeshProUGUI amountText = go.transform.Find("Amount")?.GetComponent<TextMeshProUGUI>();
                if (amountText != null) amountText.text = ""; // 装备数量隐藏
                
                // 点击事件：将该装备设为目标
                Button btn = go.GetComponent<Button>();
                if (btn == null) btn = go.AddComponent<Button>();
                btn.onClick.RemoveAllListeners();
                
                RuntimeEquipment targetEquip = slot.equipmentInstance;
                btn.onClick.AddListener(() => SelectTargetEquip(targetEquip));

                // 悬浮提示
                UI_TooltipTrigger tooltip = go.GetComponent<UI_TooltipTrigger>();
                if (tooltip == null) tooltip = go.AddComponent<UI_TooltipTrigger>();
                tooltip.currentItem = slot.equipmentInstance.blueprint;
            }
        }
    }

    private void SelectTargetEquip(RuntimeEquipment equip)
    {
        currentTarget = equip;
        selectedFodders.Clear(); // 切换武器时，清空已放入的狗粮
        lastAwakenedAffixIndex = -1;
        RefreshRightPanel();
    }

    // --- 右侧逻辑 ---
    private int GetSimulatedExpToNextLevel(EquipmentRarity rarity, int simLevel)
    {
        if (simLevel >= RuntimeEquipment.MAX_LEVEL) return 0;
        float rarityMult = 1.0f;
        switch(rarity) 
        {
            case EquipmentRarity.Rare: rarityMult = 1.5f; break;
            case EquipmentRarity.Epic: rarityMult = 2.0f; break;
            case EquipmentRarity.Legendary: rarityMult = 3.0f; break;
        }
        return Mathf.RoundToInt((simLevel + 1) * 100 * rarityMult);
    }
    private void RefreshRightPanel()
    {
        // 如果没有选中目标，隐藏右侧详情
        if (currentTarget == null)
        {
            if (rightPanelContent != null) rightPanelContent.SetActive(false);
            return;
        }

        if (rightPanelContent != null) rightPanelContent.SetActive(true);

        // 1. 基础信息
        if (targetIcon != null) targetIcon.sprite = currentTarget.blueprint.icon;
        if (targetName != null) targetName.text = $"<color=#{ColorUtility.ToHtmlStringRGB(GetRarityColor(currentTarget.rarity))}>{currentTarget.blueprint.itemName}</color>";
        if (targetLevel != null) targetLevel.text = currentTarget.level >= RuntimeEquipment.MAX_LEVEL ? "MAX LEVEL" : $"Lv.{currentTarget.level} -> Lv.{currentTarget.level + 1}";

        // 2. 结算当前放入的狗粮总经验与金币
        int totalFeedExp = 0;
        foreach (var kvp in selectedFodders)
        {
            if (kvp.Key.equipmentInstance != null) totalFeedExp += kvp.Key.equipmentInstance.GetTotalFeedValue() * kvp.Value;
            else if (kvp.Key.itemData != null) totalFeedExp += kvp.Key.itemData.feedExpValue * kvp.Value;
        }

        int cost = totalFeedExp * EnhanceManager.Instance.goldCostPerExp;
        if (costText != null)
        {
            string costColor = GameManager.Instance.Player.Gold >= cost ? "#FFFFFF" : "#FF0000";
            costText.text = $"手续费: <color={costColor}>{cost}</color> 金币";
        }

        // 3. 经验进度展示 (加上预计获得的经验)
        if (expSlider != null)
        {
            expSlider.maxValue = currentTarget.GetExpToNextLevel();
            // 这里为了直观，我们可以用 Mathf.Clamp 防止经验条视觉上超界
            expSlider.value = Mathf.Clamp(currentTarget.currentExp + totalFeedExp, 0, currentTarget.GetExpToNextLevel());
        }

        if (expText != null)
        {
            if (currentTarget.level >= RuntimeEquipment.MAX_LEVEL)
            {
                expText.text = "MAX";
            }
            else
            {
                string expAddStr = totalFeedExp > 0 ? $"<color=#00FF00> +{totalFeedExp}</color>" : "";
                expText.text = $"{currentTarget.currentExp}{expAddStr} / {currentTarget.GetExpToNextLevel()}";
            }
        }

        // 4. 属性变化预览与词条展示
        if (statPreview != null)
        {
            string statsStr = "";

            // A. 白值预览
            bool hasBaseStats = false;
            
            // 提取通用乘区，避免重复书写冗长的计算公式
            float rarityMult = 1f + ((int)currentTarget.rarity * 0.3f);
            float nextLevelMult = 1f + ((currentTarget.level + 1) * 0.1f);

            if (currentTarget.blueprint.baseDamage > 0)
            {
                int currentDmg = currentTarget.DynamicDamage;
                int nextDmg = Mathf.RoundToInt(currentTarget.blueprint.baseDamage * nextLevelMult * rarityMult);
                statsStr += $"基础攻击力: {currentDmg} <color=#00FF00>-> {nextDmg}</color>\n";
                hasBaseStats = true;
            }
            if (currentTarget.blueprint.baseDefense > 0)
            {
                int currentDef = currentTarget.DynamicDefense;
                int nextDef = Mathf.RoundToInt(currentTarget.blueprint.baseDefense * nextLevelMult * rarityMult);
                statsStr += $"基础防御力: {currentDef} <color=#00FF00>-> {nextDef}</color>\n";
                hasBaseStats = true;
            }
            if (currentTarget.blueprint.baseMaxHP > 0)
            {
                int currentHP = currentTarget.DynamicMaxHP;
                int nextHP = Mathf.RoundToInt(currentTarget.blueprint.baseMaxHP * nextLevelMult * rarityMult);
                statsStr += $"最大生命值: {currentHP} <color=#00FF00>-> {nextHP}</color>\n";
                hasBaseStats = true;
            }
            if (currentTarget.blueprint.baseMaxMP > 0)
            {
                int currentMP = currentTarget.DynamicMaxMP;
                int nextMP = Mathf.RoundToInt(currentTarget.blueprint.baseMaxMP * nextLevelMult * rarityMult);
                statsStr += $"最大法力值: {currentMP} <color=#00FF00>-> {nextMP}</color>\n";
                hasBaseStats = true;
            }

            if (!hasBaseStats)
            {
                statsStr += "该装备无基础白值\n";
            }

            // B. 词条列表展示
            if (currentTarget.affixes.Count > 0)
            {
                statsStr += "\n<color=#AAAAAA>--- 附加词条 ---</color>\n";
                for (int i = 0; i < currentTarget.affixes.Count; i++)
                {
                    var affix = currentTarget.affixes[i];
                    string sign = affix.value >= 0 ? "+" : "";
                    string percent = affix.isPercent ? "%" : "";
                    
                    // 如果这个词条刚刚被觉醒强化过，用醒目的颜色高亮它
                    if (i == lastAwakenedAffixIndex)
                    {
                        statsStr += $"♦ <color=#00FF00>{affix.statType}: {sign}{affix.value}{percent} (觉醒提升!)</color>\n";
                    }
                    else
                    {
                        statsStr += $"♦ {affix.statType}: {sign}{affix.value}{percent}\n";
                    }
                }
            }

            // C. 里程碑预警 (计算如果吃掉当前狗粮，会不会跨越 5 的倍数)
            int expectedLevel = currentTarget.level;
            int simulatedExp = currentTarget.currentExp + totalFeedExp;
            
            while (expectedLevel < RuntimeEquipment.MAX_LEVEL && simulatedExp >= GetSimulatedExpToNextLevel(currentTarget.rarity, expectedLevel))
            {
                simulatedExp -= GetSimulatedExpToNextLevel(currentTarget.rarity, expectedLevel);
                expectedLevel++;
            }

            if (expectedLevel > currentTarget.level)
            {
                int oldMilestone = currentTarget.level / 5;
                int newMilestone = expectedLevel / 5;
                if (newMilestone > oldMilestone)
                {
                    statsStr += $"\n<color=#FFD700>⭐ 升级至 +{expectedLevel} 将觉醒 {newMilestone - oldMilestone} 条附加词条！</color>";
                }
            }

            statPreview.text = statsStr;
        }

        // 5. 刷新狗粮列表 UI
        RefreshFodderGrid();
        

        // 6. 按钮状态控制
        if (btnAutoSelect != null) btnAutoSelect.interactable = currentTarget.level < RuntimeEquipment.MAX_LEVEL;
        if (btnConfirmEnhance != null) btnConfirmEnhance.interactable = selectedFodders.Count > 0 && GameManager.Instance.Player.Gold >= cost;
    }

    private void RefreshFodderGrid()
    {
        if (fodderListContainer == null || fodderSlotPrefab == null) return;
        foreach (Transform child in fodderListContainer) Destroy(child.gameObject);

        foreach (var kvp in selectedFodders)
        {
            InventorySlot slotData = kvp.Key;
            int count = kvp.Value;

            GameObject go = Instantiate(fodderSlotPrefab, fodderListContainer);
            
            Image iconImg = go.transform.Find("Icon")?.GetComponent<Image>();
            if (iconImg != null)
            {
                iconImg.sprite = slotData.equipmentInstance != null ? slotData.equipmentInstance.blueprint.icon : slotData.itemData.icon;
                iconImg.enabled = true;
            }

            TextMeshProUGUI amountText = go.transform.Find("Text_Count")?.GetComponent<TextMeshProUGUI>();
            if (amountText == null) amountText = go.transform.Find("Amount")?.GetComponent<TextMeshProUGUI>();
            if (amountText != null) amountText.text = count > 1 ? count.ToString() : "";

            // ==========================================
            // 👇 新增：给狗粮格子挂载点击撤下功能
            Button btn = go.GetComponent<Button>();
            if (btn == null) btn = go.AddComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => 
            {
                selectedFodders.Remove(slotData);
                RefreshRightPanel(); // 移除后重新计算经验和手续费
            });
            // ==========================================
        }
    }

    // --- 按钮事件 ---
    private void OnAutoSelectClicked()
    {
        if (currentTarget == null) return;
        selectedFodders = EnhanceManager.Instance.AutoSelectMaterials(currentTarget);
        RefreshRightPanel();
    }

    private void OnConfirmEnhanceClicked()
    {
        if (currentTarget == null || selectedFodders.Count == 0) return;

        // 👇 快照：记录升级前的所有词条数值
        float[] oldAffixValues = new float[currentTarget.affixes.Count];
        for (int i = 0; i < currentTarget.affixes.Count; i++)
        {
            oldAffixValues[i] = currentTarget.affixes[i].value;
        }

        bool success = EnhanceManager.Instance.TryEnhance(currentTarget, selectedFodders);
        if (success)
        {
            // 👇 对比：找出哪个词条的数值变大了
            lastAwakenedAffixIndex = -1;
            for (int i = 0; i < currentTarget.affixes.Count; i++)
            {
                if (currentTarget.affixes[i].value > oldAffixValues[i])
                {
                    lastAwakenedAffixIndex = i;
                    break;
                }
            }

            selectedFodders.Clear(); 
            RefreshLeftEquipList(); 
            RefreshRightPanel();
            
            if (UI_Blacksmith.Instance != null)
            {
                UI_Blacksmith.Instance.UpdatePlayerGold();
            }
        }
    }

    private Color GetRarityColor(EquipmentRarity rarity)
    {
        switch (rarity)
        {
            case EquipmentRarity.Common: return Color.white; 
            case EquipmentRarity.Rare: return new Color(0f, 0.63f, 1f); 
            case EquipmentRarity.Epic: return new Color(0.81f, 0.26f, 1f); 
            case EquipmentRarity.Legendary: return new Color(1f, 0.84f, 0f); 
            default: return Color.white;
        }
    }
}