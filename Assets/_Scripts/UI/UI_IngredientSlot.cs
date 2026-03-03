using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UI_IngredientSlot : MonoBehaviour
{
    public Image iconImg;
    public TextMeshProUGUI countTxt;

    public void Setup(ItemData item, int have, int need)
    {
        if (iconImg == null) iconImg = transform.Find("Icon")?.GetComponent<Image>();
        if (countTxt == null) countTxt = transform.Find("Text_Count")?.GetComponent<TextMeshProUGUI>();

        if (iconImg != null) { iconImg.sprite = item.icon; iconImg.enabled = true; }

        if (countTxt != null)
        {
            string color = have >= need ? "#FFFFFF" : "#FF0000";
            countTxt.text = $"<color={color}>{have}/{need}</color>";
        }

        // 添加悬浮提示
        UI_TooltipTrigger tooltip = GetComponent<UI_TooltipTrigger>();
        if (tooltip == null) tooltip = gameObject.AddComponent<UI_TooltipTrigger>();
        tooltip.currentItem = item;
    }
}