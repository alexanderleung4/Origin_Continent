using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

// 定义一条触摸反馈规则
[System.Serializable]
public struct TouchReaction
{
    public AffinityType requiredAffinity; // 需要检查的羁绊类型
    public int requiredLevel;             // 需要达到的数值
    
    [Header("剧本配置 (Asset优先，没有则读CSV)")]
    public DialogueData dialogueAsset;    // 短对话直接拖 Asset
    public string dialogueCSV;            // 长对话填 CSV 名字
}

// 继承 IPointerClickHandler，完美融入 Unity 原生 UI 点击系统
public class UI_TouchHotspot : MonoBehaviour, IPointerClickHandler
{
    [Header("Hotspot Settings")]
    public string partName = "Head"; 
    
    [Tooltip("摸这里会不会播放专门的音效？没配就不播")]
    public AudioClip touchSound;

    [Header("Reactions (从高到低排列)")]
    [Tooltip("系统会从上往下检查，播放第一个满足条件的对话")]
    public List<TouchReaction> reactions;

    [Header("Default Reaction (兜底)")]
    [Tooltip("如果上面的好感度都没达标，优先播放Asset，其次CSV")]
    public DialogueData defaultDialogueAsset;
    public string defaultDialogueCSV;

    private CharacterData ownerData;

    public void Setup(CharacterData data)
    {
        ownerData = data;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (ownerData == null) return;

        // 1. 播放音效
        if (touchSound != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(touchSound);
        }

        // 2. 查找并播放达标的对话
        PlayValidDialogue();
    }

    private void PlayValidDialogue()
    {
        // 如果系统没准备好，或者压根没配羁绊反应，直接走兜底
        if (AffinityManager.Instance == null || reactions == null || reactions.Count == 0)
        {
            TriggerDialogue(defaultDialogueAsset, defaultDialogueCSV);
            return;
        }

        // 从上往下遍历，找到第一个达标的奖励
        foreach (var rx in reactions)
        {
            int currentAffinity = AffinityManager.Instance.GetAffinity(ownerData.characterID, rx.requiredAffinity);
            if (currentAffinity >= rx.requiredLevel)
            {
                TriggerDialogue(rx.dialogueAsset, rx.dialogueCSV);
                return;
            }
        }

        // 全都不达标，走兜底
        TriggerDialogue(defaultDialogueAsset, defaultDialogueCSV);
    }

    // 🎯 核心双轨路由逻辑
    private void TriggerDialogue(DialogueData asset, string csv)
    {
        if (DialogueManager.Instance == null) return;

        // 💡 提示：开启沉浸模式已在 UI_TouchRoom 统一处理，这里只管播
        if (asset != null)
        {
            Debug.Log($"[Touch System] 摸了 {ownerData.characterName} 的 {partName}，触发 Asset 剧本: {asset.name}");
            DialogueManager.Instance.StartDialogue(asset);
        }
        else if (!string.IsNullOrEmpty(csv))
        {
            Debug.Log($"[Touch System] 摸了 {ownerData.characterName} 的 {partName}，触发 CSV 剧本: {csv}");
            DialogueManager.Instance.StartDialogueCSV(csv);
        }
        else
        {
            Debug.LogWarning($"[Touch System] 摸了 {ownerData.characterName} 的 {partName}，但既没有配置Asset也没有配置CSV！");
        }
    }
}