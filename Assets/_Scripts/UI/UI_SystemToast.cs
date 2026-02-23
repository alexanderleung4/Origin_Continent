using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class ToastMessage
{
    public string mergeID;    // 聚合码 (如: "Gold", "Item_1")
    public string prefixText; // 文本 (如: "获得金币:", "升到了 5 级！")
    public int amount;        // 数量 (大于0时才会拼接 "+x"，否则只显示文本)
    public Sprite icon;       // 图标
}

public class UI_SystemToast : MonoBehaviour
{
    public static UI_SystemToast Instance { get; private set; }

    [Header("UI References")]
    public CanvasGroup canvasGroup;
    public RectTransform panelRect;
    public Image iconImage;
    public TextMeshProUGUI toastText;

    [Header("Animation Settings")]
    public float displayDuration = 2.0f; // 悬停时间
    public float fadeDuration = 0.3f;    // 淡入淡出时间
    public float slideOffset = 50f;      // 从上方多少像素滑下来

    private List<ToastMessage> messageQueue = new List<ToastMessage>();
    private ToastMessage currentMessage = null;
    private float currentTimer = 0f;
    private bool isPlaying = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        
        if (canvasGroup != null) canvasGroup.alpha = 0;
        gameObject.SetActive(false);
    }

    // --- 全局静态调用接口 ---
    public void Show(string mergeID, string prefix, int amount = 0, Sprite icon = null)
    {
        // 1. 如果正在播报的就是这个类型，直接聚合数值！
        if (isPlaying && currentMessage != null && currentMessage.mergeID == mergeID && !string.IsNullOrEmpty(mergeID))
        {
            currentMessage.amount += amount;
            UpdateUI(currentMessage);
            currentTimer = displayDuration; // 重置停留时间
            return;
        }

        // 2. 检查队列里有没有还没播出来的同类项
        ToastMessage queuedMsg = messageQueue.Find(m => m.mergeID == mergeID && !string.IsNullOrEmpty(mergeID));
        if (queuedMsg != null)
        {
            queuedMsg.amount += amount;
            return;
        }

        // 3. 作为全新的播报加入队列
        ToastMessage newMsg = new ToastMessage { mergeID = mergeID, prefixText = prefix, amount = amount, icon = icon };
        messageQueue.Add(newMsg);

        if (!isPlaying)
        {
            gameObject.SetActive(true); // 必须在启动协程前激活物体
            StartCoroutine(PlayQueue());
        }
    }

    private void UpdateUI(ToastMessage msg)
    {
        if (iconImage != null)
        {
            if (msg.icon != null)
            {
                iconImage.sprite = msg.icon;
                iconImage.gameObject.SetActive(true);
            }
            else
            {
                iconImage.gameObject.SetActive(false); // 没有图标自动隐藏，LayoutGroup会自动排版
            }
        }

        // 文本拼装：如果有数量，就拼接 "+数量"，否则只显示文本
        if (msg.amount > 0)
            toastText.text = $"{msg.prefixText} +{msg.amount}";
        else
            toastText.text = msg.prefixText;
    }

    private IEnumerator PlayQueue()
    {
        isPlaying = true;
        gameObject.SetActive(true);

        while (messageQueue.Count > 0)
        {
            currentMessage = messageQueue[0];
            messageQueue.RemoveAt(0);

            UpdateUI(currentMessage);

            // --- 阶段1：滑入 (Fade In & Slide Down) ---
            float t = 0;
            Vector2 startPos = new Vector2(panelRect.anchoredPosition.x, slideOffset);
            Vector2 endPos = new Vector2(panelRect.anchoredPosition.x, 0);

            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                if (canvasGroup != null) canvasGroup.alpha = t / fadeDuration;
                panelRect.anchoredPosition = Vector2.Lerp(startPos, endPos, t / fadeDuration);
                yield return null;
            }
            if (canvasGroup != null) canvasGroup.alpha = 1;
            panelRect.anchoredPosition = endPos;

            // --- 阶段2：悬停读取 (Hold) ---
            currentTimer = displayDuration;
            while (currentTimer > 0)
            {
                currentTimer -= Time.deltaTime; // 期间如果外部调了 Show() 并触发聚合，currentTimer 会被重置
                yield return null;
            }

            // --- 阶段3：淡出上滑 (Fade Out & Slide Up) ---
            t = 0;
            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                if (canvasGroup != null) canvasGroup.alpha = 1 - (t / fadeDuration);
                panelRect.anchoredPosition = Vector2.Lerp(endPos, startPos, t / fadeDuration);
                yield return null;
            }
            if (canvasGroup != null) canvasGroup.alpha = 0;
        }

        isPlaying = false;
        currentMessage = null;
        gameObject.SetActive(false);
    }

    // --- 调试用 (右键组件菜单可调用) ---
    [ContextMenu("Test: 拾取 100 金币 (测试聚合)")]
    public void TestAddGold()
    {
        Show("Gold", "获得金币:", 100, null);
    }

    [ContextMenu("Test: 获得 1 把生锈的铁剑 (测试排队)")]
    public void TestAddItem()
    {
        Show("Weapon_RustSword", "获得物品: 生锈的铁剑", 1, null);
    }
}
