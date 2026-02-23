using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems; // 用于处理鼠标悬停等高级交互(可选)

public class UI_SkillSlot : MonoBehaviour
{
    [Header("UI References")]
    public Image iconImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI costText; // 显示 MP: 10 / SP: 20
    public Button btnClick; // 点击按钮

    private SkillData mySkill;

    // --- 初始化 ---
    public void Setup(SkillData skill, System.Action<SkillData> onClickCallback)
    {
        mySkill = skill;

        if (skill == null) return;

        // 1. 图标
        if (iconImage != null)
        {
            iconImage.sprite = skill.icon;
            iconImage.enabled = (skill.icon != null);
        }

        // 2. 名字
        if (nameText != null) nameText.text = skill.skillName;

        // 3. 消耗显示 (根据类型显示 MP 或 SP)
        if (costText != null)
        {
            if (skill.damageType == DamageType.Magical)
                costText.text = $"MP: {skill.mpCost}";
            else
                costText.text = $"SP: {skill.staminaCost}";
        }

        // 4. 绑定点击
        if (btnClick != null)
        {
            btnClick.onClick.RemoveAllListeners();
            btnClick.onClick.AddListener(() => onClickCallback?.Invoke(skill));
        }
    }
}