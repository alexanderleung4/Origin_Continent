using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

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
    public GameObject nameContainer; 
    public TextMeshProUGUI nameText;

    [Header("状态元素 (仅 FullStats 显示)")]
    public GameObject statsRoot;     
    public Slider hpSlider;
    public TextMeshProUGUI hpText;
    public Slider mpSlider;

    private Button btn;

    private void Awake()
    {
        InitializeComponents();
    }

    // 独立出初始化逻辑
    private void InitializeComponents()
    {
        if (btn == null) btn = GetComponent<Button>();
    }

    /// <summary>
    /// 万能初始化接口
    /// </summary>
    public void Setup(RuntimeCharacter character, AvatarDisplayMode mode, Action<RuntimeCharacter> onClick)
    {
        if (character == null) return;
        
        // 🛡️ 防爆盾 1：强制执行初始化，应对 Instantiate 生命周期陷阱
        InitializeComponents();

        // 🛡️ 防爆盾 2：确保角色数据文件未丢失
        if (character.data == null)
        {
            Debug.LogWarning($"[UI_RosterAvatar] 角色 {character.Name} 的配置数据丢失！");
            return;
        }

        // 1. 设置头像
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
            // 防除零错误
            int safeMaxHP = character.MaxHP > 0 ? character.MaxHP : 1;
            int safeMaxMP = character.MaxMP > 0 ? character.MaxMP : 1;

            if (hpSlider != null) hpSlider.value = (float)character.CurrentHP / safeMaxHP;
            if (hpText != null) hpText.text = $"HP: {character.CurrentHP}/{safeMaxHP}";
            if (mpSlider != null) mpSlider.value = (float)character.CurrentMP / safeMaxMP;
        }

        // 4. 绑定点击回调 (增加判空防护)
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => onClick?.Invoke(character));
        }
    }
}