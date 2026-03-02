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
    public float displayDuration = 2.0f; 
    public float fadeDuration = 0.3f;    
    public float slideOffset = 50f;      

    private List<ToastMessage> messageQueue = new List<ToastMessage>();
    private ToastMessage currentMessage = null;
    private float currentTimer = 0f;
    private bool isPlaying = false;
    
    // 👇 新增：跳过标记
    private bool skipRequested = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        
        if (canvasGroup != null) canvasGroup.alpha = 0;
        gameObject.SetActive(false);
    }

    // 👇 新增：全局监听玩家点击
    private void Update()
    {
        // 如果正在播报，且玩家点击了鼠标左键（或者按了任意键），立刻请求跳过当前播报！
        if (isPlaying && (Input.GetMouseButtonDown(0) || Input.anyKeyDown))
        {
            skipRequested = true;
        }
    }

    public void Show(string mergeID, string prefix, int amount = 0, Sprite icon = null)
    {
        if (isPlaying && currentMessage != null && currentMessage.mergeID == mergeID && !string.IsNullOrEmpty(mergeID))
        {
            currentMessage.amount += amount;
            UpdateUI(currentMessage);
            currentTimer = displayDuration; 
            return;
        }

        ToastMessage queuedMsg = messageQueue.Find(m => m.mergeID == mergeID && !string.IsNullOrEmpty(mergeID));
        if (queuedMsg != null)
        {
            queuedMsg.amount += amount;
            return;
        }

        ToastMessage newMsg = new ToastMessage { mergeID = mergeID, prefixText = prefix, amount = amount, icon = icon };
        messageQueue.Add(newMsg);

        if (!isPlaying)
        {
            gameObject.SetActive(true); 
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
                iconImage.gameObject.SetActive(false); 
            }
        }

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
            skipRequested = false; // 每次拿新消息时重置跳过标记

            UpdateUI(currentMessage);

            // --- 阶段1：滑入 (Fade In & Slide Down) ---
            float t = 0;
            Vector2 startPos = new Vector2(panelRect.anchoredPosition.x, slideOffset);
            Vector2 endPos = new Vector2(panelRect.anchoredPosition.x, 0);

            while (t < fadeDuration)
            {
                // 👇 核心打断：如果玩家点击了，瞬间完成滑入！
                if (skipRequested) break; 

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
                // 👇 核心打断：如果玩家点击了，瞬间结束悬停！
                if (skipRequested) break;

                currentTimer -= Time.deltaTime; 
                yield return null;
            }

            // --- 阶段3：淡出上滑 (Fade Out & Slide Up) ---
            // 淡出时不建议打断，不然画面会突兀闪烁，保留原本的 0.3 秒顺滑离场
            skipRequested = false; 
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

    [ContextMenu("Test: 拾取 100 金币 (测试聚合)")]
    public void TestAddGold() { Show("Gold", "获得金币:", 100, null); }

    [ContextMenu("Test: 获得 1 把生锈的铁剑 (测试排队)")]
    public void TestAddItem() { Show("Weapon_RustSword", "获得物品: 生锈的铁剑", 1, null); }
}