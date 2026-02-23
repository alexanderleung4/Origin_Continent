using UnityEngine;
using System.Collections.Generic;

public enum QuestType
{
    Main,   // 主线 (一次性，记录到 EventMemory)
    Side,   // 支线 (一次性，记录到 EventMemory)
    Daily   // 每日 (可重复，跨天重置)
}

public enum QuestObjectiveType
{
    Kill,       // 杀怪
    Collect,    // 收集物品
    Talk        // 对话/访问
}

[System.Serializable]
public class QuestObjective
{
    public QuestObjectiveType type;
    public string targetID; // 怪物ID / 物品ID / NPC ID
    public int requiredAmount; // 需要多少个
    [HideInInspector] public int currentAmount; // 当前进度 (运行时用)
}

[System.Serializable]
public class QuestReward
{
    public int expReward;
    public int goldReward;
    public ItemData itemReward;
    public int itemAmount = 1;
}

[CreateAssetMenu(fileName = "NewQuest", menuName = "Origin/Quest Data")]
public class QuestData : ScriptableObject
{
    [Header("Info")]
    public string questID;
    public string title;
    [TextArea] public string description;
    public QuestType questType;
    
    [Header("Display Info (UI显示)")]
    public string locationName; // 任务地点 (e.g. "迷雾森林")
    public string npcName;      // 发布人 (e.g. "村长")

    [Header("Objectives")]
    public List<QuestObjective> objectives;

    [Header("Rewards")]
    public QuestReward reward;

    [Header("Quest Chain (任务链)")]
    // 👇 核心: 链表结构。做完这个任务，自动接取/解锁下一个任务
    public QuestData nextQuest; 
    
    // 👇 设定: 是否可放弃? (主线通常为 false, 每日任务为 true)
    public bool isAbandonable = true;

    // --- 运行时状态 (简单起见，暂存于 ScriptableObject，正式版建议分离) ---
    [HideInInspector] public bool isAccepted;
    [HideInInspector] public bool isCompleted;
    [HideInInspector] public bool isSubmitted; // 已提交(拿完奖励)

    [Header("Requirements (接取条件)")]
    public int minLevel = 1; // 最低等级
    public QuestData preRequisiteQuest; // 前置任务

    [Header("Dialogue Config (任务对话配置)")]
    // 1. 接任务时的对话 (对应 AcceptQuest)
    public string startDialogueCSV; 
    
    // 2. 进行中但没做完时的对话 (催促玩家)
    public DialogueData processingDialogue; // 用短对话即可
    
    // 3. 完成任务时的对话 (对应 SubmitQuest)
    public string completeDialogueCSV;
    
    // 检查是否达成目标
    public bool CheckCompletion()
    {
        foreach (var obj in objectives)
        {
            if (obj.currentAmount < obj.requiredAmount) return false;
        }
        return true;
    }
    [ContextMenu("Reset Status (重置状态)")]
    public void ResetStatus()
    {
        isAccepted = false;
        isCompleted = false;
        isSubmitted = false;
        if(objectives != null) foreach(var obj in objectives) obj.currentAmount = 0;
        Debug.Log($"任务 {title} 状态已重置");
    }
}