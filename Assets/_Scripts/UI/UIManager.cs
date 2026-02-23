using UnityEngine;
using TMPro; 
using UnityEngine.UI;
using System.Collections.Generic; // 确保引入 List 和 Dictionary

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("System Panels (核心界面引用)")]
    public GameObject mainHUD_Ref;   // 👈 新增：用来存 MainHUD
    public GameObject battleHUD_Ref; // 👈 新增：用来存 BattleHUD
    public UI_Battle battleUI;

    [Header("UI Elements (UI组件绑定)")]
    public TextMeshProUGUI timeText; // 顶部栏的时间显示
    public TextMeshProUGUI locationNameText; // 新增：显示地点名字
    public UI_Inventory inventoryUI;
    [Header("Background (新增)")]
    public Image backgroundImage; // 背景大图
    [Header("Right Panel")] 
    public Slider playerHPSlider; // 玩家血条
    public Slider playerExpSlider;//经验值
    public TextMeshProUGUI playerNameText; // 玩家名字
    public Image playerAvatarImage;//玩家头像
     
    [Header("Scene View (新增)")]
    public Transform sceneContainer;    // SceneContainer
    public GameObject npcButtonPrefab;  // Btn_NPC_Template
    [Header("Interaction")] 
    public UI_Interaction interactionMenu;
    [Header("Debug / Test")]
    public CharacterData debugEnemy;

    [Header("UI Panels (面板管理)")]
    // 👇 请把所有主要面板的根节点 (GameObject) 都拖进这个列表
    // 例如: Panel_Character, Panel_Inventory, Panel_Interaction, Panel_Settings, Panel_Shop
    public List<GameObject> allMenus = new List<GameObject>();

    [Header("Blocker")]
    public GameObject globalBlocker; // 拖进去

    // 👇 新增: 记录当前场景里的 NPC 按钮和数据的对应关系
    // Key: 按钮物体, Value: 对应的角色数据
    private Dictionary<GameObject, CharacterData> activeNPCButtons = new Dictionary<GameObject, CharacterData>();

    // ✅ 保留 Awake 用于单例初始化 (保持原来的防僵尸逻辑)
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (Instance.gameObject != null) Destroy(Instance.gameObject);
        }
        Instance = this;
    }
    // ✅ 恢复听力：监听时间变化
    private void Start()
    {
        // 1. 订阅时间系统
        if (TimeManager.Instance != null)
        {
            // 先移除防止重复
            TimeManager.Instance.OnTimeChanged.RemoveListener(UpdateTimeUI);
            TimeManager.Instance.OnTimeChanged.AddListener(UpdateTimeUI);

            // 2. 马上刷新一次 (注意：这里要用小写 c !)
            UpdateTimeUI(
                TimeManager.Instance.currentDay,    
                TimeManager.Instance.currentHour,   
                TimeManager.Instance.currentMinute  
            );
        }
    }

    // ✅ 记得在销毁时取消订阅 
    private void OnDestroy()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnTimeChanged.RemoveListener(UpdateTimeUI);
        }
        
        // 别忘了把监听 NPC 按钮的列表也清空
        activeNPCButtons.Clear();
    }

    // --- 新增：更新场景UI ---
    public void UpdateLocationUI(LocationData locData)
    {
        if (locData == null) return;

        // 1. 刷新标题
        if (locationNameText != null) locationNameText.text = locData.locationName;

        // 2. 清理旧场景的所有残骸
        ClearCurrentScene();

        // 3. 智能路由：优先加载独立场景预制体
        if (locData.mapPrefab != null)
        {
            // 隐藏基础的 UI 背景大图
            if (backgroundImage != null) backgroundImage.gameObject.SetActive(false);
            
            // 实例化整个场景控制台
            Instantiate(locData.mapPrefab, sceneContainer);
            Debug.Log($"[UI] 已加载独立场景预制体: {locData.mapPrefab.name}");
        }
        else
        {
            // 4. 向下兼容：如果没做 Prefab，就用老方法显示背景图和随机NPC
            if (backgroundImage != null) 
            {
                backgroundImage.gameObject.SetActive(true);
                if (locData.backgroundImage != null) backgroundImage.sprite = locData.backgroundImage;
            }
            SpawnNPCs(locData);
        }
    }
    // 独立出来的清理方法
    private void ClearCurrentScene()
    {
        for (int i = sceneContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(sceneContainer.GetChild(i).gameObject);
        }
        activeNPCButtons.Clear();
    }
    private void SpawnNPCs(LocationData loc)
    {
        // 只有旧模式才走这里，安全清理已经在 ClearCurrentScene 做过了
        // 只有旧模式才走这里，安全清理已经在 ClearCurrentScene 做过了
        if (loc.staticNPCs != null)
        {
            foreach (CharacterData npc in loc.staticNPCs)
            {
                GameObject btnObj = Instantiate(npcButtonPrefab, sceneContainer);
                
                float randomX = Random.Range(-400f, 400f);
                float randomY = Random.Range(-200f, 0f);
                btnObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(randomX, randomY);
                
                // 填充数据：调用真实肉身立绘
                if (npc.bodySprite_Normal != null) btnObj.GetComponent<Image>().sprite = npc.bodySprite_Normal;
                
                TextMeshProUGUI nameText = btnObj.GetComponentInChildren<TextMeshProUGUI>();
                if (nameText != null) nameText.text = npc.characterName;

                // 绑定事件
                Button btn = btnObj.GetComponent<Button>();
                btn.onClick.AddListener(() => OnNPCClicked(npc));

                activeNPCButtons.Add(btnObj, npc);
            }
        }
    }

    // --- 👇 新增: 移除特定 NPC ---
    // 供 BattleManager 在胜利后调用
    public void RemoveNPC(CharacterData targetData)
    {
        GameObject targetBtn = null;

        // 遍历字典，找到第一个匹配该数据的按钮
        foreach (var pair in activeNPCButtons)
        {
            if (pair.Value == targetData)
            {
                targetBtn = pair.Key;
                break; // 找到一个就够了 (防止一次删掉所有同名怪)
            }
        }

        if (targetBtn != null)
        {
            // 从字典移除
            activeNPCButtons.Remove(targetBtn);
            // 销毁物体
            Destroy(targetBtn);
            Debug.Log($"[UI] 已移除战败单位: {targetData.characterName}");
        }
    }

    private void OnNPCClicked(CharacterData npc)
    {
        // 修正逻辑：先检查引用是否存在
        if (interactionMenu != null)
        {
            interactionMenu.OpenMenu(npc);
        }
        else
        {
            Debug.LogError("UIManager 居然没有绑定 interactionMenu！快去 Inspector 里拖进去！");
        }
    }

    // --- 更新逻辑 ---
    private void UpdateTimeUI(int day, int hour, int minute)
    {
        // 格式化字符串: "Day 1 | 08:00"
        // D2 表示数字不足两位补0 (比如 8 变成 08)
        timeText.text = $"Day {day} | {hour:D2}:{minute:D2}";
    }
    // --- 新增：刷新状态逻辑 ---
    // ✅ 改造刷新方法：绝对防御
    public void RefreshPlayerStatus()
    {
        // 1. 此时 GameManager 可能还没准备好 Player，直接返回，不报错
        if (GameManager.Instance == null || GameManager.Instance.Player == null) 
        {
            return; 
        }

        // 2. 检查 UI 组件是否都在 (防止你拖进场景后 Inspector 里的引用掉了)
        if (playerHPSlider == null || playerNameText == null) 
        {
            // Debug.LogWarning("UI组件缺失，跳过刷新");
            return; 
        }

        // 3. 安全赋值
        var player = GameManager.Instance.Player;
        
        if (playerNameText != null) playerNameText.text = player.Name;
        
        // 加上分母为0的保护
        if (playerHPSlider != null)
        {
            float maxHP = player.MaxHP > 0 ? player.MaxHP : 1; 
            playerHPSlider.value = (float)player.CurrentHP / maxHP;
        }

        if (playerExpSlider != null)
        {
            float reqExp = player.ExpRequiredForLevelUp > 0 ? player.ExpRequiredForLevelUp : 1;
            playerExpSlider.value = (float)player.CurrentLevelProgress / reqExp;
        }

        if (playerAvatarImage != null)
        {
            // 只有当 player.data 存在，且 portrait 不为空时，才显示图片
            if (player.data != null && player.data.portrait != null)
            {
                playerAvatarImage.gameObject.SetActive(true);
                playerAvatarImage.sprite = player.data.portrait;
            }
            else
            {
                // 如果没有图片，就隐藏 Image 组件，防止出现白方块
                playerAvatarImage.gameObject.SetActive(false);
            }
        }
    }

    // 🌟 核心协议: 关闭所有已注册的菜单
    public void CloseAllMenus()
    {
        // 🛡️ 防线 A: 如果列表根本没初始化，直接跳过
        if (allMenus == null) return;
        foreach (var panel in allMenus)
        {
            if (panel != null && panel.activeSelf)
            {
                panel.SetActive(false);
            }
        }
        
        if (interactionMenu != null) interactionMenu.gameObject.SetActive(false);
        Debug.Log("[UI] 已执行互斥操作：关闭所有面板。");
        if (globalBlocker != null) globalBlocker.SetActive(false);
    }

    // 统一的面板打开入口
    public void OnOpenPanel()
    {
        // 1. 先关闭其他面板
        CloseAllMenus(); 
        
        // 2. 开启防穿透遮罩 (如果您之前没有定义 globalBlocker 变量，这行可以注释掉)
        if (globalBlocker != null) globalBlocker.SetActive(true); 
    }
    public void OnAnyMenuOpened()
    {
        if (globalBlocker != null) globalBlocker.SetActive(true);
    }
    [ContextMenu("Test Battle")]
    public void TestStartBattle()
    {
        // 如果有配置好的敌人，就用配置的；否则还是用玩家分身(防崩)
        CharacterData target = debugEnemy != null ? debugEnemy : GameManager.Instance.Player.data;
        
        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.StartBattle(target);
        }
    }
}