using UnityEngine;
using System.Collections.Generic; // 👈 修复报错的关键：引入 List 和 HashSet
using UnityEngine.SceneManagement;

public enum GameState 
{
    Boot, Exploration, Battle, Dialogue, Menu
}

public class GameManager : MonoBehaviour
{

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        // 👇 新增：绑定时间管理器的跨天事件
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnDayChanged.AddListener(OnDayPassed);
        }
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        // 👇 新增：解绑事件防止内存泄漏
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnDayChanged.RemoveListener(OnDayPassed);
        }
    }

    private void OnDayPassed(int newDay)
    {
        Debug.Log($"[GameManager] 世界迎来了第 {newDay} 天，开始全队状态结算...");
        
        // 通知【所有出战队员】刷新带有期限的特质
        foreach (var member in _activeParty)
        {
            if (member != null) member.TickTraits(1); 
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 排除标题画面
        if (scene.name == "Scene_Title") return;

        // 👇 改动：不要直接刷新，而是启动一个协程“延迟刷新”
        StartCoroutine(DelayedUIRefresh());
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnDayChanged.AddListener(OnDayPassed);
        }
    }

    // 新增：延迟刷新协程
    private System.Collections.IEnumerator DelayedUIRefresh()
    {
        // ⏳ 等待这一帧结束 (让 SaveManager 先把数据灌进去)
        yield return new WaitForEndOfFrame(); 
        
        // 再多等一小会儿 (0.1秒)，确保所有 Awake/Start 都跑完了
        yield return new WaitForSeconds(0.1f);

        if (UIManager.Instance != null)
        {
            // A. 推送背景图
            if (currentLocation != null)
            {
                UIManager.Instance.UpdateLocationUI(currentLocation);
            }

            // B. 推送玩家状态
            if (Player != null)
            {
                // 防线：数据修复 (之前写的)
                if (Player.data == null && playerTemplate != null) Player.data = playerTemplate;

                UIManager.Instance.RefreshPlayerStatus();
                Debug.Log("[GameManager] 延迟刷新 UI 完成 (白头像修复)");
            }
        }
    }
    // ========================================================================
    // 1. 核心架构 (Core Architecture)
    // ========================================================================
    public static GameManager Instance { get; private set; }
    public GameState CurrentState { get; private set; }

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
        InitializeGame();
    }

    private void Update()
    {
        UpdateDebugStats(); // 每帧刷新 Debug 面板 (仅编辑器用)
    }
    //游戏难度设定
    public GameDifficulty currentDifficulty = GameDifficulty.Origin;

    // ========================================================================
    // 2. 小队与名册数据 (Party & Roster Data)
    // ========================================================================
    [Header("Party Settings")]
    [Tooltip("主角模板 (固定 0 号位，绝不可卸下)")]
    public CharacterData playerTemplate; 
    
    [Tooltip("初始自带的其他队友 (用于编辑器测试)")]
    public List<CharacterData> initialTeammates = new List<CharacterData>();

    // 活跃出战列 (打仗、吃经验的人)
    [SerializeField] private List<RuntimeCharacter> _activeParty = new List<RuntimeCharacter>();
    public List<RuntimeCharacter> activeParty => _activeParty;

    // 名册库 (所有已解锁的队友数据引用)
    public List<CharacterData> unlockedCharacters = new List<CharacterData>();

    // [黄金兼容接口]: 引擎里的旧代码读取 Player 时，永远返回主角 (0号位)！
    public RuntimeCharacter Player => (_activeParty != null && _activeParty.Count > 0) ? _activeParty[0] : null;

    // ========================================================================
    // 3. 世界状态 (World State)
    // ========================================================================
    [Header("World Settings")]
    public LocationData startingLocation; // 初始地点
    public LocationData currentLocation;  // 当前地点

    public LocationData currentHomeLocation; // 当前的家园坐标（方便以后做换家系统）

    // ========================================================================
    // 4. 记忆系统 (Memory System)
    // ========================================================================
    // 记录所有已完成的事件ID / 剧情ID
    public HashSet<string> eventMemory = new HashSet<string>();

    /// <summary>
    /// 检查某事是否发生过 (查户口)
    /// </summary>
    public bool HasEvent(string eventID)
    {
        return eventMemory.Contains(eventID);
    }

    /// <summary>
    /// 记录某事发生了 (写入海马体)
    /// </summary>
    public void AddEvent(string eventID)
    {
        if (!string.IsNullOrEmpty(eventID) && !eventMemory.Contains(eventID))
        {
            eventMemory.Add(eventID);
            Debug.Log($"[Memory] 记住了事件: {eventID}");
        }
    }

    // ========================================================================
    // 5. 游戏逻辑 (Game Logic)
    // ========================================================================
    
    private void InitializeGame()
    {
        // ==========================================
        // 👇 新增：游戏启动时，优先读取本地硬盘的全局难度设定
        // ==========================================
        // (int)GameDifficulty.Origin 的值通常是 1
        int savedDiff = PlayerPrefs.GetInt("GlobalDifficulty", (int)GameDifficulty.Origin);
        currentDifficulty = (GameDifficulty)savedDiff;
        Debug.Log($"[System] 全局难度已同步为: {currentDifficulty}");
        // A. 初始化主角与小队
        _activeParty.Clear();
        unlockedCharacters.Clear();

        if (playerTemplate != null)
        {
            // 1. 强制塞入主角 (永远占据 0 号位)
            RuntimeCharacter mc = new RuntimeCharacter(playerTemplate);
            _activeParty.Add(mc);
            unlockedCharacters.Add(playerTemplate);
            Debug.Log($"[GameManager] 主角 {mc.Name} 初始化完成 (0号位)。");

            // 2. 塞入初始测试队友
            foreach (var tm in initialTeammates)
            {
                if (tm != null)
                {
                    _activeParty.Add(new RuntimeCharacter(tm));
                    unlockedCharacters.Add(tm);
                    Debug.Log($"[GameManager] 队友 {tm.characterName} 加入队伍！");
                }
            }
        }
        else
        {
            Debug.LogError("[GameManager] 严重错误：未配置 playerTemplate！");
        }

        // B. 启动状态
        ChangeState(GameState.Exploration);
        Debug.Log("Origin System: Online.");
        
        // C. 进入初始地点
        if (startingLocation != null)
        {
            GoToLocation(startingLocation);
        }
    }

    // --- 👇 新增: 彻底重置游戏状态 (New Game) ---
    public void StartNewGame()
    {
        Debug.Log("--- 🔥 Starting New Game 🔥 ---");

        // 1. 重置记忆
        eventMemory.Clear();

        // 2. 重置小队
        _activeParty.Clear();
        unlockedCharacters.Clear();
        if (playerTemplate != null)
        {
            _activeParty.Add(new RuntimeCharacter(playerTemplate));
            unlockedCharacters.Add(playerTemplate);
            foreach (var tm in initialTeammates)
            {
                if (tm != null)
                {
                    _activeParty.Add(new RuntimeCharacter(tm));
                    unlockedCharacters.Add(tm);
                }
            }
        }

        // 3. 重置背包
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.inventory.Clear();
            // 如果有初始道具，可以在这里添加
        }

        // 4. 重置任务
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.activeQuests.Clear();
            // 重置所有任务数据的运行时状态 (重要!)
            foreach (var q in QuestManager.Instance.allQuests)
            {
                q.isAccepted = false;
                q.isCompleted = false;
                q.isSubmitted = false;
                foreach (var obj in q.objectives) obj.currentAmount = 0;
            }
        }

        // 5. 进入初始场景
        if (startingLocation != null)
        {
            GoToLocation(startingLocation);
        }
    }

    public void GoToLocation(LocationData newLocation)
    {
        if (newLocation == null) return;

        // 👇 使用 SceneFader 包裹逻辑
        if (SceneFader.Instance != null)
        {
            SceneFader.Instance.FadeAndExecute(() => 
            {
                // 这部分代码会在屏幕全黑时执行
                PerformLocationChange(newLocation);
            });
        }
        else
        {
            // 如果没 Fader (比如测试时)，直接切
            PerformLocationChange(newLocation);
        }
    }

    // 将具体的切换逻辑抽离出来
    private void PerformLocationChange(LocationData newLocation)
    {
        currentLocation = newLocation;
        Debug.Log($"[GameManager] 抵达地点: {newLocation.locationName}");

        // 1. 刷新 UI (背景图、NPC列表)
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateLocationUI(newLocation);
        }

        // 2. 👇 切换 BGM (核心!)
        if (AudioManager.Instance != null && newLocation.backgroundMusic != null)
        {
            // 这里我们直接 PlayMusic。
            // 因为 FadeAndExecute 里已经调用了 FadeOutMusic 把声音关小了，
            // PlayMusic 内部逻辑会负责从 0 淡入新音乐。
            AudioManager.Instance.PlayMusic(newLocation.backgroundMusic);
        }
    }

    public void ChangeState(GameState newState)
    {
        CurrentState = newState;
        // 未来可以在这里添加 OnStateChanged 事件广播
    }

    // ========================================================================
    // 6. 调试工具 (Debug Tools)
    // ========================================================================
    [Header("Debug: Final Stats View")]
    [SerializeField] private int _hp;
    [SerializeField] private int _mp;
    [SerializeField] private int _stamina;
    [SerializeField] private int _atk;
    [SerializeField] private int _def;
    [SerializeField] private int _spd;
    [SerializeField] private int _gold; // 新增金币监视

    // 将 Update 里的逻辑抽离出来，保持整洁
    private void UpdateDebugStats()
    {
        // 👇 修改：playerInstance -> Player
        if (Player != null) 
        {
            _hp = Player.MaxHP;
            _mp = Player.MaxMP;
            _stamina = Player.MaxStamina;
            _atk = Player.Attack;
            _def = Player.Defense;
            _spd = Player.Speed;
            _gold = Player.Gold;
        }
    }

    public void TeleportToHome()
    {
        if (currentHomeLocation != null)
        {
            Debug.Log("【死神传送】正在将玩家遣返回家...");
            // Using existing location loading method
            GoToLocation(currentHomeLocation);
        }
        else     {
            Debug.LogError("致命错误：未配置 currentHomeLocation！玩家成了孤魂野鬼！");
        }
    }

    // ========================================================================
    // 7. 经济系统管线 (Economy Pipeline)
    // ========================================================================
    /// <summary>
    /// 获取经过难度乘区计算后的物品真实买入价格
    /// </summary>
    public int GetDynamicBuyPrice(ItemData item)
    {
        if (item == null) return 0;
        
        float priceMultiplier = 1.0f;
        switch (currentDifficulty)
        {
            case GameDifficulty.Story:  priceMultiplier = 0.8f; break; // 叙事模式打 8 折
            case GameDifficulty.Origin: priceMultiplier = 1.0f; break; // 起源模式原价
            case GameDifficulty.Abyss:  priceMultiplier = 1.5f; break; // 深渊模式奸商加价 50%
        }

        return Mathf.RoundToInt(item.buyPrice * priceMultiplier);
    }

    [ContextMenu("Debug: Clear Memory")]
    public void ClearMemory()
    {
        eventMemory.Clear();
        Debug.Log("[Memory] 记忆已清除 (海马体重置)");
    }

    [ContextMenu("Test: Hurt Player")]
    public void TestHurtPlayer()
    {
        // 👇 修改：playerInstance -> Player
        if (Player != null)
        {
            Player.TakeDamage(10);
            if(UIManager.Instance != null) UIManager.Instance.RefreshPlayerStatus();
        }
        // 👇 修改：GameManager.Instance.Player
        if (GameManager.Instance.Player != null)
        {
            GameManager.Instance.Player.AddTrait(Resources.Load<TraitData>("Traits/Trait_BlackCurse"), 1);
        }
    }
}