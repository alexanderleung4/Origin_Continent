using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System;

public class UI_ChoicePanel : MonoBehaviour
{
    public static UI_ChoicePanel Instance { get; private set; }

    [Header("UI 容器")]
    public GameObject panelRoot;
    public Transform buttonContainer; 
    public GameObject choiceButtonPrefab; 

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        panelRoot.SetActive(false);
    }

    // 🎯 新增 onLeaveFallback 委托，用于蓝队的防死锁兜底
    public void ShowChoices(List<DialogueChoice> choices, Action<DialogueChoice> onSelected, Action onLeaveFallback = null)
    {
        foreach (Transform child in buttonContainer) Destroy(child.gameObject);

        int unlockedCount = 0; // 记录有多少个按钮是可以点的

        foreach (var choice in choices)
        {
            GameObject btnObj = Instantiate(choiceButtonPrefab, buttonContainer);
            Button btn = btnObj.GetComponent<Button>();
            TextMeshProUGUI txt = btnObj.GetComponentInChildren<TextMeshProUGUI>();

            // ==========================================
            // 🎯 呼叫黑盒进行条件查验
            // ==========================================
            ConditionResult result = ConditionEvaluator.Evaluate(choice.conditionCommand);

            if (result.isMet)
            {
                txt.text = choice.choiceText;
                btn.interactable = true;
                unlockedCount++;

                DialogueChoice capturedChoice = choice; 
                btn.onClick.AddListener(() => 
                {
                    panelRoot.SetActive(false); 
                    onSelected?.Invoke(capturedChoice); 
                });
            }
            else
            {
                // 被锁住了！加上提示文案，并禁用点击
                txt.text = $"{choice.choiceText}{result.lockHint}";
                btn.interactable = false;
            }
        }

        // ==========================================
        // 防死锁协议：如果全部都被锁住了
        // ==========================================
        if (unlockedCount == 0)
        {
            GameObject fallbackBtnObj = Instantiate(choiceButtonPrefab, buttonContainer);
            Button fBtn = fallbackBtnObj.GetComponent<Button>();
            TextMeshProUGUI fTxt = fallbackBtnObj.GetComponentInChildren<TextMeshProUGUI>();

            fTxt.text = "<color=#ff5555>【离开】(前置条件未满足)</color>";
            fBtn.interactable = true;
            fBtn.onClick.AddListener(() =>
            {
                panelRoot.SetActive(false);
                onLeaveFallback?.Invoke(); // 通知 DialogueManager 强制结束对话
            });
        }

        panelRoot.SetActive(true);
    }
}