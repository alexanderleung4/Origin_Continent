using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UI_SaveMenu : MonoBehaviour
{
    public static UI_SaveMenu Instance { get; private set; }

    [Header("UI References")]
    public GameObject panelRoot;
    public Transform listContent;
    public GameObject slotPrefab;
    public Button closeButton;

    [Header("Settings")]
    public int maxSaveSlots = 10;
    public string gameSceneName = "MainMenu"; // 👈 确保这里填对名字

    private UI_SaveSlot currentSelectedSlot;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (closeButton) closeButton.onClick.AddListener(CloseMenu);
        CloseMenu(); // 默认关闭
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && panelRoot.activeSelf)
        {
            CloseMenu();
        }
    }

    public void OpenMenu()
    {
        panelRoot.SetActive(true);
        RefreshList();
    }

    public void CloseMenu()
    {
        panelRoot.SetActive(false);
        currentSelectedSlot = null; // 关闭时重置选中状态
    }

    public void RefreshList()
    {
        // 1. 清空列表
        foreach (Transform child in listContent) Destroy(child.gameObject);

        // 2. 生成自动存档
        CreateSlot(-1);

        // 3. 生成手动存档
        for (int i = 0; i < maxSaveSlots; i++)
        {
            CreateSlot(i);
        }
    }

    private void CreateSlot(int id)
    {
        GameObject go = Instantiate(slotPrefab, listContent);
        UI_SaveSlot slot = go.GetComponent<UI_SaveSlot>();
        if (slot != null)
        {
            // 👇 传入 ID 和 游戏场景名
            slot.Setup(id, gameSceneName);
        }
    }

    // --- Slot 点击回调 ---
    public void OnSlotClicked(UI_SaveSlot clickedSlot)
    {
        if (currentSelectedSlot == clickedSlot)
        {
            // 如果点的是同一个，就收起
            clickedSlot.SetExpanded(false);
            currentSelectedSlot = null;
            return;
        }

        if (currentSelectedSlot != null)
        {
            // 收起上一个
            currentSelectedSlot.SetExpanded(false);
        }

        // 展开新的
        clickedSlot.SetExpanded(true);
        currentSelectedSlot = clickedSlot;
    }
}