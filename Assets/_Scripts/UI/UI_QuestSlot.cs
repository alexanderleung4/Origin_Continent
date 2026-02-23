using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UI_QuestSlot : MonoBehaviour
{
    [Header("UI Components")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI typeText;
    public TextMeshProUGUI progressText;
    public TextMeshProUGUI locationText; // 👈 1. 补上这个引用
    
    public Button abandonButton; 
    public Button submitButton; // 👈 2. 新增提交按钮

    private QuestData currentQuest; // 当前显示的数据

    // --- 初始化设置 ---
    public void Setup(QuestData quest)
    {
        currentQuest = quest;

        // 1. 设置基础文本
        if (titleText) titleText.text = quest.title;
        
        // 2. 设置类型和颜色
        if (typeText)
        {
            switch (quest.questType)
            {
                case QuestType.Main: 
                    typeText.text = "[主线]"; 
                    typeText.color = Color.yellow; 
                    break;
                case QuestType.Side: 
                    typeText.text = "[支线]"; 
                    typeText.color = Color.white; 
                    break;
                case QuestType.Daily: 
                    typeText.text = "[每日]"; 
                    typeText.color = Color.cyan; 
                    break;
            }
        }

        // 3. 设置地点文本
        if (locationText) locationText.text = $"地点: {quest.locationName}";

        // 4. 设置进度文本 (保持不变)
        UpdateProgressText();

        // 5. 按钮逻辑 (核心)
        // A. 放弃按钮: 只有未完成且可放弃的任务显示
        if (abandonButton)
        {
            bool canAbandon = quest.isAbandonable && !quest.isCompleted;
            abandonButton.gameObject.SetActive(canAbandon);
            abandonButton.onClick.RemoveAllListeners();
            abandonButton.onClick.AddListener(OnAbandonClicked);
        }

        // B. 提交按钮: 只有 "已完成" 且 "未提交" 时显示
        if (submitButton)
        {
            bool canSubmit = quest.isCompleted && !quest.isSubmitted;
            submitButton.gameObject.SetActive(canSubmit);
            
            submitButton.onClick.RemoveAllListeners();
            submitButton.onClick.AddListener(OnSubmitClicked);
        }
    }
    private void OnSubmitClicked()
    {
        if (currentQuest == null) return;
        
        // 调用管理器提交
        QuestManager.Instance.SubmitQuest(currentQuest.questID);
        
        // 刷新列表 (提交后任务会消失或移入已完成列表)
        if (UI_QuestLog.Instance != null) UI_QuestLog.Instance.RefreshList();
    }

    private void UpdateProgressText()
    {
        if (progressText == null) return;

        if (currentQuest.isCompleted)
        {
            progressText.text = "<color=green>已完成 (请提交)</color>";
        }
        else
        {
            // 显示目标进度 (例如: 0/5)
            string str = "";
            foreach (var obj in currentQuest.objectives)
            {
                // 这里简单显示 targetID，理想情况应该查表转中文名称
                str += $"{obj.targetID}: {obj.currentAmount}/{obj.requiredAmount}\n";
            }
            progressText.text = str;
        }
    }

    // --- 点击放弃 ---
    private void OnAbandonClicked()
    {
        if (currentQuest == null) return;

        // 调用管理器放弃任务
        QuestManager.Instance.AbandonQuest(currentQuest.questID);

        // 刷新整个列表 (比较暴力的做法，但安全)
        // 您需要在 UI_QuestLog 里把 Instance 公开，或者用事件通知
        if (UI_QuestLog.Instance != null)
        {
            UI_QuestLog.Instance.RefreshList();
        }
    }
}