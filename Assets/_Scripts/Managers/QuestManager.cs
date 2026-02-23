using UnityEngine;
using System.Collections.Generic;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    [Header("Database (所有任务索引)")]
    public List<QuestData> allQuests; // 拖入所有做好的任务

    // 运行时列表
    public List<QuestData> activeQuests = new List<QuestData>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // 1. 订阅时间变化 (处理每日任务刷新)
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnDayChanged.AddListener(OnNewDay);
            
        // 2. 初始化任务状态 (比如把所有 Daily 任务重置)
        // 实际开发中这里应该读取存档
    }
    
    private void OnDestroy() 
    {
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnDayChanged.RemoveListener(OnNewDay);
    }

    // --- 核心: 每日刷新逻辑 ---
    private void OnNewDay(int day)
    {
        Debug.Log("[Quest] 新的一天，刷新每日任务...");
        foreach (var quest in allQuests)
        {
            if (quest.questType == QuestType.Daily)
            {
                if (quest.isSubmitted) // 只有昨天做完的才重置
                {
                    ResetQuest(quest);
                }
            }
        }
        // TODO: 通知 UI 刷新
    }

    private void ResetQuest(QuestData quest)
    {
        quest.isAccepted = false;
        quest.isCompleted = false;
        quest.isSubmitted = false;
        foreach (var obj in quest.objectives) obj.currentAmount = 0;
        
        if (activeQuests.Contains(quest)) activeQuests.Remove(quest);
    }

    // --- 核心: 接受任务 ---
    public void AcceptQuest(string questID)
    {
        QuestData q = allQuests.Find(x => x.questID == questID);
        if (q != null && !q.isAccepted && !q.isSubmitted)
        {
            q.isAccepted = true;
            foreach (var obj in q.objectives) obj.currentAmount = 0; // 确保进度清零
            activeQuests.Add(q);
            Debug.Log($"[Quest] 接受任务: {q.title}");
            
            // 立即检查一次背包 (如果是收集任务，可能背包里已经有了)
            CheckInventoryObjectives(q);
        }
    }

    // --- 👇 新增: 放弃任务 ---
    public void AbandonQuest(string questID)
    {
        QuestData q = activeQuests.Find(x => x.questID == questID);
        if (q != null)
        {
            if (!q.isAbandonable)
            {
                Debug.LogWarning("此任务不可放弃 (主线)！");
                return;
            }

            // 1. 重置状态
            q.isAccepted = false;
            foreach (var obj in q.objectives) obj.currentAmount = 0;

            // 2. 移出列表
            activeQuests.Remove(q);
            
            Debug.Log($"[Quest] 已放弃任务: {q.title}");
            
            // TODO: 刷新 UI
            //if (UI_QuestLog.Instance != null) UI_QuestLog.Instance.RefreshList();
        }
    }
    
    // --- 修改: 提交任务 (支持链式触发) ---
    public void SubmitQuest(string questID)
    {
        QuestData q = activeQuests.Find(x => x.questID == questID);
        if (q != null && q.isCompleted && !q.isSubmitted)
        {
            q.isSubmitted = true;
            activeQuests.Remove(q); 
            
            // 1. 发奖励
            GiveReward(q.reward);
            
            // 2. 写入永久记忆 (防止主线重复做)
            if (q.questType != QuestType.Daily)
            {
                GameManager.Instance.AddEvent($"Quest_{questID}_Done");
            }

            Debug.Log($"[Quest] 任务完成: {q.title}");

            // 3. 👇 核心: 自动接取下一环 (链表逻辑)
            if (q.nextQuest != null)
            {
                Debug.Log($"[Quest] 触发后续任务: {q.nextQuest.title}");
                AcceptQuest(q.nextQuest.questID);
                // 也可以不直接 Accept，而是弹窗询问，看设计需求
            }
        }
    }

    // 检查这个 NPC 身上有没有正在进行的任务
    public QuestData GetQuestByNPC(string npcName)
    {
        // 在 activeQuests 里找，且发布人是 npcName
        return activeQuests.Find(q => q.npcName == npcName);
    }

    // 检查这个 NPC 身上有没有我可以接的新任务
    // 🕵️‍♂️ Debug侦探版
    public QuestData GetAvailableQuestForNPC(string npcName)
    {
        Debug.Log($"[Quest-Debug] 正在询问 NPC [{npcName}] 是否有任务...");
        
        if (allQuests == null || allQuests.Count == 0)
        {
            Debug.LogError("[Quest-Debug] 严重警告：All Quests 列表是空的！去 QuestManager Inspector 里拖任务！");
            return null;
        }

        foreach (var q in allQuests)
        {
            if (q == null) continue;

            // 打印检查过程
            string logPrefix = $"   - 检查任务 [{q.title}]: ";

            // 1. 检查名字
            if (q.npcName != npcName)
            {
                // 如果名字不一样，不打印Log以免刷屏，或者用 LogWarning 调试
                // Debug.Log($"{logPrefix} NPC名字不匹配 (任务配的是: '{q.npcName}', 当前是: '{npcName}')"); 
                continue; 
            }

            // 找到了归属这个 NPC 的任务，开始详细检查
            Debug.Log($"{logPrefix} 找到归属任务，开始检查状态...");

            // 2. 检查状态 (最大的嫌疑人!)
            if (q.isAccepted) 
            {
                Debug.LogWarning($"{logPrefix} 失败 -> 任务状态显示【已接取】(isAccepted=true)。请手动在 Inspector 重置！");
                continue;
            }
            if (q.isSubmitted) 
            {
                Debug.LogWarning($"{logPrefix} 失败 -> 任务状态显示【已提交】(isSubmitted=true)。请手动在 Inspector 重置！");
                continue;
            }

            // 3. 检查等级
            int playerLv = GameManager.Instance.Player.Level;
            if (playerLv < q.minLevel) 
            {
                Debug.Log($"{logPrefix} 失败 -> 等级不足 (玩家: {playerLv}, 需求: {q.minLevel})");
                continue;
            }

            // 4. 检查前置
            if (q.preRequisiteQuest != null && !q.preRequisiteQuest.isSubmitted) 
            {
                Debug.Log($"{logPrefix} 失败 -> 前置任务未完成");
                continue;
            }

            // 通过所有检查！
            Debug.Log($"   -> ✅ 成功匹配！准备发布任务。");
            return q;
        }
        
        Debug.Log($"[Quest-Debug] NPC [{npcName}] 身上没有符合条件的任务。");
        return null;
    }

    // --- 监听: 怪物死亡 ---
    // 这个方法需要由 BattleManager 在胜利时调用
    public void OnEnemyKilled(string enemyID)
    {
        foreach (var q in activeQuests)
        {
            if (q.isCompleted) continue;

            bool updated = false;
            foreach (var obj in q.objectives)
            {
                if (obj.type == QuestObjectiveType.Kill && obj.targetID == enemyID)
                {
                    if (obj.currentAmount < obj.requiredAmount)
                    {
                        obj.currentAmount++;
                        updated = true;
                    }
                }
            }
            
            if (updated) CheckQuestStatus(q);
        }
    }
    // --- 监听: NPC 对话 ---
    public void OnNPCInteracted(string targetID)
    {
        foreach (var q in activeQuests)
        {
            if (q.isCompleted) continue;

            bool updated = false;
            foreach (var obj in q.objectives)
            {
                // 检查类型是 Talk 且 目标ID 匹配
                if (obj.type == QuestObjectiveType.Talk && obj.targetID == targetID)
                {
                    if (obj.currentAmount < obj.requiredAmount)
                    {
                        obj.currentAmount++;
                        updated = true;
                    }
                }
            }
            if (updated) CheckQuestStatus(q);
        }
    }

    // --- 辅助: 检查背包 (收集任务) ---
    public void CheckInventoryObjectives(QuestData q)
    {
        // 逻辑稍复杂：需要遍历 objective 检查 InventoryManager
        // 暂略，后续补充
    }

    private void CheckQuestStatus(QuestData q)
    {
        if (q.CheckCompletion())
        {
            q.isCompleted = true;
            Debug.Log($"[Quest] 任务目标达成: {q.title} (请回去提交)");
            // TODO: 飘字提示 "任务完成!"
        }
    }

    private void GiveReward(QuestReward r)
    {
        if (r.expReward > 0) GameManager.Instance.Player.GainExp(r.expReward);
        if (r.goldReward > 0) GameManager.Instance.Player.Gold += r.goldReward;
        if (r.itemReward != null) InventoryManager.Instance.AddItem(r.itemReward, r.itemAmount);
    }
}