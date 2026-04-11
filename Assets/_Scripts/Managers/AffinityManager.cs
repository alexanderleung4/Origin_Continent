using System.Collections.Generic;
using UnityEngine;

// 羁绊三大维度
public enum AffinityType { Trust, Intimacy, Dependency }

// 运行时的肉身数据
public class AffinityRuntimeData
{
    public int trust = 0;
    public int intimacy = 0;
    public int dependency = 0;
}

public class AffinityManager : MonoBehaviour
{
    public static AffinityManager Instance { get; private set; }

    [Header("Interaction Settings")]
    public int maxDailyInteractionPoints = 3; // 暂时硬编码为3次/天
    public int currentInteractionPoints;

    // 内存中的羁绊数据字典 (Key: CharacterID)
    private Dictionary<string, AffinityRuntimeData> runtimeAffinity = new Dictionary<string, AffinityRuntimeData>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // 挂载跨天重置事件
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnDayChanged.AddListener(OnDayChanged);
        }
        currentInteractionPoints = maxDailyInteractionPoints;
    }

    // 监听：每日刷新
    private void OnDayChanged(int newDay)
    {
        currentInteractionPoints = maxDailyInteractionPoints;
        if (UI_SystemToast.Instance != null)
            UI_SystemToast.Instance.Show("AP_Reset", "新的一天，互动次数已重置", 0, null);
    }

    // --- 行动点数控制 ---
    public bool HasInteractionPoints() => currentInteractionPoints > 0;

    public bool ConsumeInteractionPoint()
    {
        if (currentInteractionPoints > 0)
        {
            currentInteractionPoints--;
            return true;
        }
        return false;
    }

    // --- 羁绊数值控制 ---
    public void AddAffinity(string characterID, AffinityType type, int amount)
    {
        if (string.IsNullOrEmpty(characterID)) return;
        if (!runtimeAffinity.ContainsKey(characterID))
            runtimeAffinity[characterID] = new AffinityRuntimeData();

        switch (type)
        {
            case AffinityType.Trust: runtimeAffinity[characterID].trust += amount; break;
            case AffinityType.Intimacy: runtimeAffinity[characterID].intimacy += amount; break;
            case AffinityType.Dependency: runtimeAffinity[characterID].dependency += amount; break;
        }
        Debug.Log($"[Affinity] {characterID} 的 {type} 增加了 {amount}");
        // 👇 新增：每次好感增加，立刻检查是否触发里程碑
        CheckAndTriggerMilestones(characterID);
    }

    public int GetAffinity(string characterID, AffinityType type)
    {
        if (!runtimeAffinity.ContainsKey(characterID)) return 0;
        switch (type)
        {
            case AffinityType.Trust: return runtimeAffinity[characterID].trust;
            case AffinityType.Intimacy: return runtimeAffinity[characterID].intimacy;
            case AffinityType.Dependency: return runtimeAffinity[characterID].dependency;
            default: return 0;
        }
    }

    // ==========================================
    // 👇 核心机制：动态里程碑判定与发奖
    // ==========================================
    private void CheckAndTriggerMilestones(string characterID)
    {
        CharacterData cData = Resources.Load<CharacterData>($"Characters/{characterID}");
        if (cData == null || cData.affinityMilestones == null) 
        {
            Debug.LogWarning($"[Affinity-Warning] 找不到 {characterID} 的数据，或里程碑列表为空！");
            return;
        }

        foreach (var milestone in cData.affinityMilestones)
        {
            int currentAffinity = GetAffinity(characterID, milestone.requirementType);
            string memoryKey = $"Milestone_Claimed_{characterID}_{milestone.requirementType}_{milestone.requirementValue}";

            // 探针 1：查看好感度判定
            Debug.Log($"[Affinity-Trace] 检查里程碑: 需要 {milestone.requirementType} >= {milestone.requirementValue}, 当前是 {currentAffinity}");

            // 1. 检查是否已经领过
            if (GameManager.Instance != null && GameManager.Instance.HasEvent(memoryKey)) 
            {
                Debug.Log($"[Affinity-Trace] 拦截: 记忆锚点 {memoryKey} 存在，之前已经领取过了！");
                continue;
            }

            // 2. 检查好感度是否达标
            if (currentAffinity >= milestone.requirementValue)
            {
                bool isRewardSuccess = true;

                // 3. 执行发奖
                if (milestone.rewardType == MilestoneRewardType.GiveItem)
                {
                    ItemData item = Resources.Load<ItemData>($"Items/{milestone.rewardParameter}");
                    
                    if (item == null)
                    {
                        Debug.LogError($"[Affinity-Error] 致命错误！在 Resources/Items/ 目录下找不到名为【{milestone.rewardParameter}】的物品！请检查拼写。");
                        isRewardSuccess = false; 
                    }
                    else if (InventoryManager.Instance != null)
                    {
                        // 🛡️ 架构级修复：如果是装备，必须铸造肉身！
                        if (item is EquipmentData equipBlueprint)
                        {
                            Debug.Log($"[Affinity-Trace] 识别为装备图纸，现场铸造史诗级肉身...");
                            RuntimeEquipment newEquip = new RuntimeEquipment(equipBlueprint, EquipmentRarity.Epic);
                            newEquip.CalculateDynamicStats(); // 刷新白值属性
                            InventoryManager.Instance.AddItem(newEquip, 1, false); // false代表非静默，让UI弹窗
                            Debug.Log($"[Affinity-Success] 专属装备发放成功！");
                        }
                        else
                        {
                            InventoryManager.Instance.AddItem(item, 1, false); // false代表非静默
                            Debug.Log($"[Affinity-Success] 普通物品发放成功！");
                        }
                    }
                }

                // 🛡️ 只有奖励确实发放成功了，才写入防刷锚点（防止因为拼写错误导致玩家永远丢奖励）
                if (isRewardSuccess)
                {
                    if (GameManager.Instance != null) GameManager.Instance.eventMemory.Add(memoryKey);

                    // 4. 华丽的 UI 悬浮窗通报
                    if (UI_SystemToast.Instance != null)
                    {
                        UI_SystemToast.Instance.Show("Milestone", $"【羁绊突破】{milestone.milestoneDescription}!", 0, null);
                    }
                }
            }
        }
    }

    // 提供给交谈按钮的接口：寻找已经解锁，但还没看过的专属对话
    public string GetPendingMilestoneDialogue(CharacterData cData)
    {
        if (cData.affinityMilestones == null) return null;
        
        foreach (var milestone in cData.affinityMilestones)
        {
            // 如果是对话奖励，且好感度达标
            if (milestone.rewardType == MilestoneRewardType.UnlockDialogue && 
                GetAffinity(cData.characterID, milestone.requirementType) >= milestone.requirementValue)
            {
                // 检查这篇剧情是否已经被看过了
                string playedKey = $"PlayedDialogue_{milestone.rewardParameter}";
                if (GameManager.Instance != null && !GameManager.Instance.HasEvent(playedKey))
                {
                    return milestone.rewardParameter; // 返回未看过的 CSV 名字
                }
            }
        }
        return null;
    }

    // ==========================================
    // Save/Load 接口 (供 SaveManager 调用)
    // ==========================================
    public void LoadFrom(SaveData data)
    {
        runtimeAffinity.Clear();
        currentInteractionPoints = data.currentInteractionPoints; // 读取剩余行动点
        
        if (data.affinityStates == null) return;
        foreach (var node in data.affinityStates)
        {
            runtimeAffinity[node.characterID] = new AffinityRuntimeData {
                trust = node.trust,
                intimacy = node.intimacy,
                dependency = node.dependency
            };
        }
    }

    public void SaveTo(SaveData data)
    {
        data.currentInteractionPoints = currentInteractionPoints;
        data.affinityStates = new List<CharacterAffinitySaveNode>();
        foreach (var kvp in runtimeAffinity)
        {
            data.affinityStates.Add(new CharacterAffinitySaveNode {
                characterID = kvp.Key,
                trust = kvp.Value.trust,
                intimacy = kvp.Value.intimacy,
                dependency = kvp.Value.dependency
            });
        }
    }
}