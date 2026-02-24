using UnityEngine;
using TMPro;

public class UI_Tooltip : MonoBehaviour
{
    public static UI_Tooltip Instance { get; private set; }
    
    [Header("References")]
    public TextMeshProUGUI nameText;
    
    private RectTransform rectTransform;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        rectTransform = GetComponent<RectTransform>();
        HideTooltip();
    }

    private void Update()
    {
        if (gameObject.activeSelf)
        {
            Vector2 mousePos = Input.mousePosition;
            transform.position = mousePos + new Vector2(15f, -15f); 
        }
    }

    public void ShowTooltip(string itemName)
    {
        if (nameText != null) nameText.text = itemName;
        gameObject.SetActive(true);
    }

    public void HideTooltip()
    {
        gameObject.SetActive(false);
        if (nameText != null) nameText.text = "";
    }
}

