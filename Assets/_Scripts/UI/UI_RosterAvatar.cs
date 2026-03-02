using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

// 👇 核心机制：定义头像组件的显示模式
public enum AvatarDisplayMode 
{ 
    Minimal,    // 极简模式：只显示头像 (适合侧边栏快速切换)
    NameOnly,   // 选人模式：头像 + 名字 (适合穿戴装备)
    FullStats   // 战术模式：头像 + 名字 + 血条/蓝条 (适合吃药/治疗)
}

[RequireComponent(typeof(Button))]
public class UI_RosterAvatar : MonoBehaviour
{
    [Header("基础元素 (始终显示)")]
    public Image portraitImage;
    
    [Header("动态元素 (根据模式显隐)")]
    public GameObject nameContainer; // 名字的父节点(如果有底板的话)
    public TextMeshProUGUI nameText;

    [Header("状态元素 (仅 FullStats 显示)")]
    public GameObject statsRoot;     // 统管血条、蓝条的父节点
    public Slider hpSlider;
    public TextMeshProUGUI hpText;
    public Slider mpSlider;

    private Button btn;

    private void Awake()
    {
        btn = GetComponent<Button>();
    }

    /// <summary>
    /// 万能初始化接口
    /// </summary>
    public void Setup(RuntimeCharacter character, AvatarDisplayMode mode, Action<RuntimeCharacter> onClick)
    {
        if (character == null) return;

        // 1. 设置头像 (所有模式都显示)
        if (portraitImage != null && character.data.portrait != null)
        {
            portraitImage.sprite = character.data.portrait;
        }

        // 2. 根据模式决定显隐
        bool showName = (mode == AvatarDisplayMode.NameOnly || mode == AvatarDisplayMode.FullStats);
        bool showStats = (mode == AvatarDisplayMode.FullStats);

        if (nameContainer != null) nameContainer.SetActive(showName);
        else if (nameText != null) nameText.gameObject.SetActive(showName);
        
        if (statsRoot != null) statsRoot.SetActive(showStats);

        // 3. 填充具体数据
        if (showName && nameText != null) 
        {
            nameText.text = character.Name;
        }

        if (showStats)
        {
            if (hpSlider != null) hpSlider.value = (float)character.CurrentHP / character.MaxHP;
            if (hpText != null) hpText.text = $"HP: {character.CurrentHP}/{character.MaxHP}";
            if (mpSlider != null) mpSlider.value = (float)character.CurrentMP / character.MaxMP;
        }

        // 4. 绑定点击回调 (极其优雅)
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => onClick?.Invoke(character));
    }
}