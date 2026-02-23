using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UI_SceneNPC : MonoBehaviour
{
    [Header("绑定的角色数据")]
    public CharacterData npcData;

    [Header("UI 节点引用")]
    public Image bodyImage;
    public TextMeshProUGUI nameText;

    private void Start()
    {
        // 游戏开始时，把自己绑定到 UIManager 的交互菜单上
        Button btn = GetComponent<Button>();
        if (btn != null && npcData != null)
        {
            btn.onClick.AddListener(() => 
            {
                if (UIManager.Instance != null && UIManager.Instance.interactionMenu != null)
                {
                    UIManager.Instance.interactionMenu.OpenMenu(npcData);
                }
            });
        }
    }

#if UNITY_EDITOR
    // 💡 魔法代码：在 Unity 编辑器里只要修改数据，立刻刷新外观！
    private void OnValidate()
    {
        if (npcData != null)
        {
            if (bodyImage != null && npcData.bodySprite_Normal != null) 
            {
                bodyImage.sprite = npcData.bodySprite_Normal;
            }
            if (nameText != null) 
            {
                nameText.text = npcData.characterName;
            }
        }
    }
#endif
}