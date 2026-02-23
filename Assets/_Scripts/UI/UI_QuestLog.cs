using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UI_QuestLog : MonoBehaviour
{
    public static UI_QuestLog Instance { get; private set; } // 单例，方便 Slot 调用刷新

    [Header("UI References")]
    public GameObject panelRoot;
    public Transform contentRoot; // Scroll View 的 Content
    public GameObject slotPrefab; // 挂载了 UI_QuestSlot 的预制体
    public Button closeButton;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (closeButton) closeButton.onClick.AddListener(ClosePanel);
        ClosePanel();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.J))
        {
            if (panelRoot.activeSelf) ClosePanel();
            else OpenPanel();
        }
    }

    public void OpenPanel()
    {
        panelRoot.SetActive(true);
        RefreshList();
    }

    public void ClosePanel()
    {
        panelRoot.SetActive(false);
    }

    public void RefreshList()
    {
        // 1. 清理旧条目
        foreach (Transform child in contentRoot) Destroy(child.gameObject);

        if (QuestManager.Instance == null) return;

        // 2. 遍历 ActiveQuests 生成条目
        foreach (var quest in QuestManager.Instance.activeQuests)
        {
            GameObject go = Instantiate(slotPrefab, contentRoot);
            
            // 获取 Slot 脚本并初始化
            UI_QuestSlot slot = go.GetComponent<UI_QuestSlot>();
            if (slot != null)
            {
                slot.Setup(quest);
            }
        }
    }
}