using UnityEngine;
using UnityEngine.EventSystems;

public class UI_TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public ItemData currentItem; 

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (currentItem != null && UI_Tooltip.Instance != null)
        {
            UI_Tooltip.Instance.ShowTooltip(currentItem.itemName);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (UI_Tooltip.Instance != null)
        {
            UI_Tooltip.Instance.HideTooltip();
        }
    }
}

