using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class UI_Navigation : MonoBehaviour
{
    [Header("UI References")]
    public GameObject panelRoot;        // 整个弹窗 (Panel_Navigation)
    public Transform contentRoot;       // 按钮容器 (Content)
    public Button closeButton;          // 关闭按钮 (Btn_Close)
    
    [Header("Prefab")]
    public GameObject locationButtonPrefab; // 那个模版按钮

    private void Start()
    {
        // 绑定关闭按钮
        if (closeButton != null)
            closeButton.onClick.AddListener(ClosePanel);
            
        // 初始关闭
        ClosePanel();
    }

    // --- 打开面板逻辑 ---
    public void OpenPanel()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.CloseAllMenus();
            UIManager.Instance.OnAnyMenuOpened();
        }
        panelRoot.SetActive(true);
        RefreshLocationList();
    }

    public void ClosePanel()
    {
        panelRoot.SetActive(false);
    }

    // --- 核心：刷新列表 ---
    private void RefreshLocationList()
    {
        // 1. 清理旧按钮 (防止重复)
        foreach (Transform child in contentRoot)
        {
            Destroy(child.gameObject);
        }

        // 2. 获取当前地点的数据
        LocationData currentLoc = GameManager.Instance.currentLocation;
        if (currentLoc == null) return;

        // 3. 遍历连接的地点，生成按钮
        foreach (LocationData dest in currentLoc.connectedLocations)
        {
            // 生成按钮
            GameObject btnObj = Instantiate(locationButtonPrefab, contentRoot);
            
            // 改文字
            TextMeshProUGUI btnText = btnObj.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null) btnText.text = dest.locationName;

            // 绑定点击事件 (这是最骚的一步：闭包)
            Button btn = btnObj.GetComponent<Button>();
            btn.onClick.AddListener(() => OnLocationSelected(dest));
        }
    }

    // --- 点击地点后的逻辑 ---
    private void OnLocationSelected(LocationData target)
    {
        // 1. 检查消耗 (30分钟 + 5精力)
        var player = GameManager.Instance.Player;
        if (player.CurrentStamina < 5)
        {
            Debug.Log("精力不足，无法移动！");
            return;
        }

        // 2. 扣除消耗
        player.ConsumeStamina(5); // 需在 RuntimeCharacter 中补充此方法
        TimeManager.Instance.AdvanceTime(30);

        // 3. 执行移动
        GameManager.Instance.GoToLocation(target);

        // 4. 关闭面板
        ClosePanel();
    }
}