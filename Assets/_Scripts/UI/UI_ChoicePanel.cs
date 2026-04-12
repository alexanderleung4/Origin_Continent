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
    public Transform buttonContainer; // 挂载 Vertical Layout Group 的节点
    public GameObject choiceButtonPrefab; // 预制体：带 Button 和 TextMeshProUGUI

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        panelRoot.SetActive(false);
    }

    public void ShowChoices(List<DialogueChoice> choices, Action<DialogueChoice> onSelected)
    {
        // 1. 清空旧按钮
        foreach (Transform child in buttonContainer) Destroy(child.gameObject);

        // 2. 生成新按钮
        foreach (var choice in choices)
        {
            GameObject btnObj = Instantiate(choiceButtonPrefab, buttonContainer);
            btnObj.GetComponentInChildren<TextMeshProUGUI>().text = choice.choiceText;
            
            Button btn = btnObj.GetComponent<Button>();
            
            // 捕获局部变量防止闭包陷阱
            DialogueChoice capturedChoice = choice; 
            btn.onClick.AddListener(() => 
            {
                panelRoot.SetActive(false); // 点击瞬间隐藏自身
                onSelected?.Invoke(capturedChoice); // 回调通知 DialogueManager
            });
        }

        panelRoot.SetActive(true);
    }
}