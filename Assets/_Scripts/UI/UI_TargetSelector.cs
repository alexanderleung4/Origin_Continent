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
    public void OpenSelector(string title, System.Action<RuntimeCharacter> callback)
    {
        onTargetSelectedCallback = callback;
        if (titleText != null) titleText.text = title;
        
        panelRoot.SetActive(true);
        RefreshList();
    }

    private void RefreshList()
    {
        foreach (Transform child in gridContainer) Destroy(child.gameObject);

        var party = GameManager.Instance.activeParty;
        if (party == null) return;

        foreach (var member in party)
        {
            if (member == null) continue;

            GameObject go = Instantiate(avatarPrefab, gridContainer);
            
            // 自动寻找立绘 Image
            Image img = go.GetComponent<Image>();
            if (img == null) img = go.transform.Find("Image_Portrait")?.GetComponent<Image>();
            if (img == null) img = go.transform.Find("Icon")?.GetComponent<Image>();
            if (img != null && member.data.portrait != null) img.sprite = member.data.portrait;

            // 自动寻找名字 Text
            TextMeshProUGUI nameTxt = go.transform.Find("Text_Name")?.GetComponent<TextMeshProUGUI>();
            if (nameTxt != null) nameTxt.text = member.Name;

            // 自动寻找血条显示 (方便吃药时看谁残血)
            TextMeshProUGUI hpTxt = go.transform.Find("Text_HP")?.GetComponent<TextMeshProUGUI>();
            if (hpTxt != null) hpTxt.text = $"HP: {member.CurrentHP}/{member.MaxHP}";

            // 绑定核心点击事件
            Button btn = go.GetComponent<Button>();
            if (btn == null) btn = go.AddComponent<Button>();
            
            btn.onClick.AddListener(() => 
            {
                // 执行之前存好的逻辑 (比如吃药、穿装备)
                onTargetSelectedCallback?.Invoke(member);
                // 完事后自动关闭
                ClosePanel();
            });
        }
    }

    public void ClosePanel()
    {
        panelRoot.SetActive(false);
        onTargetSelectedCallback = null; // 清空回调防止内存泄漏
    }
}