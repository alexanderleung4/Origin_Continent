using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Button))]
public class UI_TraitSlot : MonoBehaviour
{
    [Header("UI 绑定")]
    public Image iconImage;
    [Tooltip("显示剩余天数的文本（例如图标角落的小字）")]
    public TextMeshProUGUI durationText; 

    private Button btn;

    private void Awake()
    {
        btn = GetComponent<Button>();
    }

    // 接收底层数据并渲染自己
    public void Setup(RuntimeCharacter.ActiveTrait trait, System.Action<RuntimeCharacter.ActiveTrait> onClickAction)
    {
        // 1. 设置图标
        if (trait.data.icon != null)
        {
            iconImage.sprite = trait.data.icon;
            iconImage.color = Color.white;
        }
        else
        {
            iconImage.color = Color.clear;
        }

        // 2. 设置期限倒计时
        if (durationText != null)
        {
            if (trait.data.isPermanent || trait.remainingDays < 0)
            {
                durationText.text = "∞"; // 永久特质不显示数字
                // 可选：如果您想加一个无限大符号，可以填 "∞"
            }
            else
            {
                durationText.text = trait.remainingDays.ToString();
            }
        }

        // 3. 绑定点击事件 (呼出详情面板)
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => onClickAction?.Invoke(trait));
    }
}