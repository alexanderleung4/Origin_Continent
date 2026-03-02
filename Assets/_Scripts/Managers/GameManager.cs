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
        foreach (var member in activeFormation)
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
    public CharacterData playerTemplate; 
    public List<CharacterData> initialTeammates = new List<CharacterData>();

    public int maxPartySize = 6; // 👇 彻底修正：6 宫格战术编队！

    // 核心沙盘：0,1,2 是前排 | 3,4,5 是后排。严丝合缝对应战斗系统的 playerSlots
    public RuntimeCharacter[] activeFormation = new RuntimeCharacter[6];
    
    public List<RuntimeCharacter> reserveParty = new List<RuntimeCharacter>(); // 冷板凳
    public List<CharacterData> unlockedCharacters = new List<CharacterData>(); // 名册

    // 🌟 黄金兼容接口：动态提取沙盘上的活人，让之前的旧代码完全不报错！
    public List<RuntimeCharacter> activeParty 
    {
        get 
        {
            List<RuntimeCharacter> list = new List<RuntimeCharacter>();
            for(int i = 0; i < 6; i++) {
                if (activeFormation[i] != null) list.Add(activeFormation[i]);
            }
            return list;
        }
    }

    // 🌟 黄金兼容接口：智能寻找主角 (防报错兜底)
    public RuntimeCharacter Player 
    {
        get 
        {
            if (playerTemplate == null) return activeParty.Count > 0 ? activeParty[0] : null;
            foreach(var c in activeFormation) {
                if (c != null && c.data.characterID == playerTemplate.characterID) return c;
            }
            return activeParty.Count > 0 ? activeParty[0] : null;
        }
    }

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
        // A. 初始化战术沙盘
        activeFormation = new RuntimeCharacter[6];
        reserveParty.Clear();
        unlockedCharacters.Clear();

        if (playerTemplate != null)
        {
            RuntimeCharacter mc = new RuntimeCharacter(playerTemplate);
            activeFormation[1] = mc; // 🎯 强行将主角锁定在 Slot 1 (前排中间)
            unlockedCharacters.Add(playerTemplate);
            Debug.Log($"[GameManager] 主角 {mc.Name} 就位 (阵型 1号位)。");

            foreach (var tm in initialTeammates)
            {
                if (tm != null) RecruitCharacter(tm.characterID); // 直接复用招募方法！
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
        activeFormation = new RuntimeCharacter[6];
        reserveParty.Clear();
        unlockedCharacters.Clear();

        if (playerTemplate != null)
        {
            RuntimeCharacter mc = new RuntimeCharacter(playerTemplate);
            activeFormation[1] = mc; // 🎯 强行将主角锁定在 Slot 1 (前排中间)
            unlockedCharacters.Add(playerTemplate);
            Debug.Log($"[GameManager] 主角 {mc.Name} 就位 (阵型 1号位)。");

            foreach (var tm in initialTeammates)
            {
                if (tm != null) RecruitCharacter(tm.characterID); // 直接复用招募方法！
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

    // ========================================================================
    // 8. 人事流通与编队系统 (Recruitment & Roster)
    // ========================================================================
    
    /// <summary>
    /// 剧情招募：将指定 ID 的角色加入麾下
    /// </summary>
    public void RecruitCharacter(string targetCharacterID)
    {
        
        // 1. 查重防刷：已经在队里或板凳上了，直接忽略
        if (activeParty.Exists(c => c.data.characterID == targetCharacterID) ||
            reserveParty.Exists(c => c.data.characterID == targetCharacterID))
        {
            Debug.LogWarning($"[Recruit] {targetCharacterID} 已经在名册中了！");
            return;
        }

        // 2. 硬盘寻址：去 Resources/Characters 找配置文件
        CharacterData cData = Resources.Load<CharacterData>($"Characters/{targetCharacterID}");
        if (cData == null)
        {
            Debug.LogError($"[Recruit] 严重错误：找不到角色文件 Resources/Characters/{targetCharacterID}");
            return;
        }

        // 3. 赋予肉身
        RuntimeCharacter newMember = new RuntimeCharacter(cData);
        unlockedCharacters.Add(cData);

        // 4. 冷热交替判定：遍历 6 宫格，找个空位塞进去
        bool placed = false;
        for (int i = 0; i < 6; i++)
        {
            if (activeFormation[i] == null)
            {
                activeFormation[i] = newMember;
                placed = true;
                break;
            }
        }

        if (placed)
        {
            if (UI_SystemToast.Instance != null) 
                UI_SystemToast.Instance.Show("Recruit", $"{newMember.Name} 加入了队伍！", 0, cData.portrait);
        }
        else
        {
            reserveParty.Add(newMember);
            if (UI_SystemToast.Instance != null) 
                UI_SystemToast.Instance.Show("Recruit", $"{newMember.Name} 编队已满，进入营地待命！", 0, cData.portrait);
        }
    }

    /// <summary>
    /// 剧情离队：角色叛变或死亡时的防蒸发协议
    /// </summary>
    public void LeaveParty(string targetCharacterID)
    {
        RuntimeCharacter leaver = null;
        int formationIndex = -1;
        bool wasActive = false;
        
        // 1. 先去 6 宫格首发沙盘里找人
        for (int i = 0; i < 6; i++)
        {
            if (activeFormation[i] != null && activeFormation[i].data.characterID == targetCharacterID)
            {
                leaver = activeFormation[i];
                formationIndex = i;
                wasActive = true;
                break;
            }
        }

        // 2. 如果场上没有，去板凳席找
        if (leaver == null) 
        {
            leaver = reserveParty.Find(c => c.data.characterID == targetCharacterID);
            wasActive = false;
        }

        if (leaver == null) return; // 查无此人

        // 🛡️ 绝对底线：判断该角色是不是主角模板！主角绝不可被移出！
        if (playerTemplate != null && leaver.data.characterID == playerTemplate.characterID)
        {
            Debug.LogError($"[LeaveParty] 越权警告：试图将主角 {leaver.Name} 移出队伍，已被系统强制拦截！");
            return;
        }

        // 🛡️ 防蒸发协议：强制没收所有已穿戴的装备，退回公共背包！
        if (InventoryManager.Instance != null)
        {
            List<EquipmentSlot> slotsToStrip = new List<EquipmentSlot>(leaver.equipment.Keys);
            foreach (var slot in slotsToStrip)
            {
                EquipmentData eq = leaver.Unequip(slot);
                // 静默塞回背包，不弹窗轰炸玩家
                if (eq != null) InventoryManager.Instance.AddItem(eq, 1, true); 
            }
        }

        // 3. 彻底除名
        if (wasActive) 
        {
            activeFormation[formationIndex] = null; // 从沙盘中拔除，格子变空
        }
        else 
        {
            reserveParty.Remove(leaver); // 从替补席中剔除
        }
        
        unlockedCharacters.RemoveAll(c => c.characterID == targetCharacterID);

        if (UI_SystemToast.Instance != null) 
            UI_SystemToast.Instance.Show("Leave", $"{leaver.Name} 离开了队伍...", 0, leaver.data.portrait);

        // 如果正开着角色面板，强制关掉防报错
        if (UI_CharacterSheet.Instance != null && UI_CharacterSheet.Instance.panelRoot.activeSelf)
        {
            UI_CharacterSheet.Instance.CloseMenu();
        }
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