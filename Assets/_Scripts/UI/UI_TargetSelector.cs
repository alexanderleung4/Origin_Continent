using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class UI_TargetSelector : MonoBehaviour
{
    public static UI_TargetSelector Instance { get; private set; }

    [Header("UI 引用")]
    public GameObject panelRoot;
    public Transform gridContainer;
    public Button closeButton;
    public TextMeshProUGUI titleText; 

    [Header("预制体")]
    public GameObject avatarPrefab; // 可以直接复用角色面板侧边栏的头像预制体，或者卡牌预制体

    // 核心：委托回调 (记录玩家选好人之后，接下来要执行什么代码)
    private System.Action<RuntimeCharacter> onTargetSelectedCallback;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        ClosePanel();
        if (closeButton != null) closeButton.onClick.AddListener(ClosePanel);
    }

    /// <summary>
    /// 呼出目标选择器
    /// </summary>
    /// <param name="title">提示文字，例如 "给谁使用？" 或 "谁来穿戴？"</param>
    /// <param name="callback">玩家点击头像后执行的逻辑</param>
    public void OpenSelector(string title, AvatarDisplayMode mode, System.Action<RuntimeCharacter> callback)
    {
        onTargetSelectedCallback = callback;
        if (titleText != null) titleText.text = title;
        
        panelRoot.SetActive(true);
        RefreshList(mode); // 传给刷新列表
    }

    private void RefreshList(AvatarDisplayMode mode)
    {
        foreach (Transform child in gridContainer) Destroy(child.gameObject);

        var party = GameManager.Instance.activeParty;
        if (party == null) return;

        foreach (var member in party)
        {
            if (member == null) continue;

            GameObject go = Instantiate(avatarPrefab, gridContainer);
            
            UI_RosterAvatar avatarUI = go.GetComponent<UI_RosterAvatar>();
            if (avatarUI != null)
            {
                avatarUI.Setup(member, mode, (selectedChar) => 
                {
                    onTargetSelectedCallback?.Invoke(selectedChar);
                    ClosePanel();
                });
            }
            else
            {
                Debug.LogError("[UI_TargetSelector] 你的 avatarPrefab 上没有挂载 UI_RosterAvatar 脚本！");
            }
        }
    }

    public void ClosePanel()
    {
        panelRoot.SetActive(false);
        onTargetSelectedCallback = null; // 清空回调防止内存泄漏
    }
}