using UnityEngine;
using UnityEngine.Events; // 引入事件系统，用于通知UI刷新

public enum DayNightPhase
{
    Day,    // 白天 (06:00 - 18:00)
    Night   // 夜晚 (18:00 - 06:00)
}

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }

    [Header("Time Settings (时间设置)")]
    public int currentDay = 1;
    [Range(0, 23)] public int currentHour = 8; // 默认早上8点开局
    [Range(0, 59)] public int currentMinute = 0;

    [Header("State (当前状态)")]
    public DayNightPhase currentPhase;

    // --- 事件广播 (Events) ---
    // 任何订阅了这个事件的脚本（比如UI），在时间变化时都会收到通知
    public UnityEvent<int, int, int> OnTimeChanged; // 参数: Day, Hour, Minute
    public UnityEvent<DayNightPhase> OnPhaseChanged; // 参数: Phase
    // 每日变更事件 (参数: 新的天数)
    public UnityEvent<int> OnDayChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        UpdatePhase(); // 初始化时检查一次白天黑夜
        NotifyTimeChange();
    }

    // --- 核心功能: 时间推进 ---
    //[cite_start]// 对应白皮书 V1.3: 移动消耗30分，战斗消耗1小时等 [cite: 47]
    public void AdvanceTime(int minutesToAdd)
    {
        currentMinute += minutesToAdd;
        bool dayChanged = false; // 标记是否跨天

        // 分钟进位逻辑
        while (currentMinute >= 60)
        {
            currentMinute -= 60;
            currentHour++;
        }

        // 小时进位逻辑 (跨天)
        while (currentHour >= 24)
        {
            currentHour -= 24;
            currentDay++;
            dayChanged = true; // 标记
            Debug.Log($"[TimeManager] 新的一天开始了: 第 {currentDay} 天");
            if (UI_SystemToast.Instance != null)
            {
                UI_SystemToast.Instance.Show("NewDay", $"新的一天开始了：第 {currentDay} 天", 0, null);
            }
            // 这里以后可以触发 "每日结算" 或 "寿命扣除"
        }

        UpdatePhase();
        NotifyTimeChange();
        // 如果跨天了，通知所有人
        if (dayChanged)
        {
            OnDayChanged?.Invoke(currentDay);
        }
    }

    // --- 核心功能: 休息/跳过时间 ---
    //[cite_start]// 对应白皮书 V1.3: 强制跳过至次日 08:00 AM [cite: 47]
    [ContextMenu("Test: Skip to Next Day (一键跨天)")]
    public void RestToNextDay()
    {
        currentDay++;
        currentHour = 8;
        currentMinute = 0;

        Debug.Log("[TimeManager] 休息完毕，精力已恢复 (逻辑待连接)");
        UpdatePhase();
        NotifyTimeChange();
        // 休息肯定会跨天，触发事件
        OnDayChanged?.Invoke(currentDay);
        if (UI_SystemToast.Instance != null)
        {
            UI_SystemToast.Instance.Show("NewDay", $"休息完毕：第 {currentDay} 天", 0, null);
        }
    }

    // --- 内部逻辑: 检查昼夜更替 ---
    private void UpdatePhase()
    {
        DayNightPhase lastPhase = currentPhase;

        //[cite_start]// 定义: 6点到18点是白天 [cite: 54]
        if (currentHour >= 6 && currentHour < 18)
        {
            currentPhase = DayNightPhase.Day;
        }
        else
        {
            currentPhase = DayNightPhase.Night;
        }

        // 如果状态变了（比如黄昏变夜晚），通知大家
        if (lastPhase != currentPhase)
        {
            OnPhaseChanged?.Invoke(currentPhase);
            Debug.Log($"[TimeManager] 天色变了: {currentPhase}");
        }
    }

    private void NotifyTimeChange()
    {
        // 广播当前时间
        OnTimeChanged?.Invoke(currentDay, currentHour, currentMinute);
        // Debug.Log($"当前时间: Day {currentDay} - {currentHour:D2}:{currentMinute:D2}");
    }

    // --- 调试用 (右键组件菜单可调用) ---
    [ContextMenu("Test: Add 30 Minutes (Move)")]
    public void TestAdd30Min()
    {
        AdvanceTime(30);
    }

    [ContextMenu("Test: Add 1 Hour (Battle)")]
    public void TestAdd1Hour()
    {
        AdvanceTime(60);
    }
}