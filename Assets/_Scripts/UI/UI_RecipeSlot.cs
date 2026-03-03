using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UI_RecipeSlot : MonoBehaviour
{
    public Image iconImg;
    public TextMeshProUGUI nameTxt;
    private Button btn;

    public void Setup(RecipeData recipe, System.Action<RecipeData> onClick)
    {
        if (iconImg == null) iconImg = transform.Find("Icon")?.GetComponent<Image>();
        if (nameTxt == null) nameTxt = transform.Find("Text_Name")?.GetComponent<TextMeshProUGUI>();
        if (btn == null) btn = GetComponent<Button>();

        if (iconImg != null) { iconImg.sprite = recipe.outputEquipment.icon; iconImg.enabled = true; }
        if (nameTxt != null) nameTxt.text = recipe.outputEquipment.itemName;

        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => onClick?.Invoke(recipe));
        }
    }
}