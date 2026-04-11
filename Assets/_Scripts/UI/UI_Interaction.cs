using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UI_Interaction : MonoBehaviour
{
    public GameObject panelRoot;
    public TextMeshProUGUI targetNameText;
    public Image targetPortrait;
    
    [Header("Action Buttons")]
    public Button talkButton;
    public Button giftButton;
    public Button tradeButton;
    public Button combatButton;
    public Button closeButton;

    public Button touchButton; // 新增触摸按钮

    private CharacterData currentTarget;

    private void Start()
    {
        if(closeButton) closeButton.onClick.AddListener(CloseMenu);
        if(talkButton) talkButton.onClick.AddListener(OnTalkClicked);
        if(tradeButton) tradeButton.onClick.AddListener(OnTradeClicked);
        if(combatButton) combatButton.onClick.AddListener(OnCombatClicked);
        if(giftButton) giftButton.onClick.AddListener(OnGiftClicked);
        if(touchButton) touchButton.onClick.AddListener(OnTouchClicked);
        CloseMenu(); 
    }

    public void OpenMenu(CharacterData target)
    {
        
        if (UIManager.Instance != null)
        {
            UIManager.Instance.CloseAllMenus();
            UIManager.Instance.OnAnyMenuOpened();
            
        }
        currentTarget = target;
        panelRoot.SetActive(true);
        
        if(targetNameText) targetNameText.text = target.characterName;
        if(targetPortrait && target.portrait != null) targetPortrait.sprite = target.portrait;

        // 按钮显示逻辑
        if (tradeButton) tradeButton.gameObject.SetActive(target.linkedShop != null);
        if (combatButton) combatButton.gameObject.SetActive(target.team == TeamType.Enemy || target.team == TeamType.Neutral);
        if (talkButton) talkButton.gameObject.SetActive(true);
        if (giftButton) giftButton.gameObject.SetActive(true);

        if (touchButton) 
        {
            // 只有当策划打勾了 canBeTouched 并且配了 Prefab 时，这个按钮才会亮起
            bool canTouch = target.canBeTouched && target.touchInteractionPrefab != null;
            touchButton.gameObject.SetActive(canTouch);
            touchButton.interactable = canTouch;
        }
    }

    public void CloseMenu()
    {
        panelRoot.SetActive(false);
        currentTarget = null;
        // ✅ 换成这行：只乖乖关掉透明遮罩，不去乱指挥 UIManager
        if (UIManager.Instance != null && UIManager.Instance.globalBlocker != null)
        {
            UIManager.Instance.globalBlocker.SetActive(false);
        }
    }

    // --- 核心: 智能对话决策 ---
    private void OnTalkClicked()
    {
        Debug.Log($"[Interaction] 与 {currentTarget.characterName} 交谈");

        // --- 1. 询问 QuestManager: 我和这个 NPC 有什么未了结的任务吗？ ---
        // 我们需要在 QuestManager 里加一个 API: GetQuestStatusForNPC(npcID)
        // 这里假设我们能获取到相关的 quest
        QuestData relatedQuest = QuestManager.Instance.GetQuestByNPC(currentTarget.characterName); // 需实现

        if (relatedQuest != null)
        {
            if (relatedQuest.isCompleted && !relatedQuest.isSubmitted)
            {
                // [阶段: 完结] -> 播放结算对话 (CSV里要写 SubmitQuest)
                Debug.Log("播放任务完成对话");
                DialogueManager.Instance.StartDialogueCSV(relatedQuest.completeDialogueCSV);
                CloseMenu();
                return;
            }
            else if (relatedQuest.isAccepted && !relatedQuest.isCompleted)
            {
                // [阶段: 进行中] -> 播放催促对话 (ScriptableObject)
                Debug.Log("播放任务进行中对话");
                DialogueManager.Instance.StartDialogue(relatedQuest.processingDialogue);
                CloseMenu();
                return;
            }
        }

        // --- 2. 询问 QuestManager: 这个 NPC 有新任务给我吗？ ---
        QuestData newQuest = QuestManager.Instance.GetAvailableQuestForNPC(currentTarget.characterName); // 需实现
        if (newQuest != null)
        {
             // [阶段: 接取] -> 播放接任务对话 (CSV里要写 AcceptQuest)
             Debug.Log("播放接任务对话");
             DialogueManager.Instance.StartDialogueCSV(newQuest.startDialogueCSV);
             CloseMenu();
             return;
        }

        // --- 3. 原有逻辑 (剧情/闲聊) ---

        // 1. 检查是否有剧情配置
        bool hasStory = !string.IsNullOrEmpty(currentTarget.currentStoryCSV);
        
        // 2. 检查剧情是否已完成 (查户口)
        bool isStoryFinished = false;
        if (hasStory && GameManager.Instance != null)
        {
            isStoryFinished = GameManager.Instance.HasEvent(currentTarget.currentStoryCSV);
        }

        // --- 决策树 ---
        if (hasStory && !isStoryFinished)
        {
            // 情况A: 免费的主线/核心剧情
            Debug.Log($"[Dialogue] 播放新剧情: {currentTarget.currentStoryCSV}");
            DialogueManager.Instance.StartDialogueCSV(currentTarget.currentStoryCSV);
        }
        else
        {
            // 👇 情况B: 进入日常互动与羁绊判定 (开始消耗行动点)
            if (AffinityManager.Instance != null && !AffinityManager.Instance.HasInteractionPoints())
            {
                if (UI_SystemToast.Instance != null) 
                    UI_SystemToast.Instance.Show("No_AP", "今日社交互动次数已耗尽。", 0, null);
                CloseMenu();
                return;
            }

            // 1. 询问总控室：有已解锁但还没看过的羁绊专属剧情吗？
            string pendingDialogueCSV = AffinityManager.Instance != null ? AffinityManager.Instance.GetPendingMilestoneDialogue(currentTarget) : null;

            if (!string.IsNullOrEmpty(pendingDialogueCSV))
            {
                // 2. 有！播放专属羁绊剧情（主线级，不需要消耗日常行动点）
                Debug.Log($"[Dialogue] 触发羁绊专属剧情: {pendingDialogueCSV}");
                
                // 写入防重复记忆锚点，表示“这篇我看过了”
                if (GameManager.Instance != null) 
                    GameManager.Instance.eventMemory.Add($"PlayedDialogue_{pendingDialogueCSV}");
                
                DialogueManager.Instance.StartDialogueCSV(pendingDialogueCSV);
            }
            else if (currentTarget.defaultDialogue != null)
            {
                // 3. 兜底逻辑：没有专属剧情，进入日常闲聊，消耗每日次数并涨好感
                if (AffinityManager.Instance != null)
                {
                    AffinityManager.Instance.ConsumeInteractionPoint();
                    AffinityManager.Instance.AddAffinity(currentTarget.characterID, AffinityType.Trust, 1); 
                }
                
                if (isStoryFinished) Debug.Log("[Dialogue] 剧情已过，播放闲聊。");
                DialogueManager.Instance.StartDialogue(currentTarget.defaultDialogue);
            }
            else
            {
                Debug.LogWarning("该角色无话可说。");
            }
        }
            
        CloseMenu();
    }
    
    private void OnGiftClicked()
    {
        // 1. 拦截：检查是否有行动点
        if (AffinityManager.Instance != null && !AffinityManager.Instance.HasInteractionPoints())
        {
            if (UI_SystemToast.Instance != null) 
                UI_SystemToast.Instance.Show("No_AP", "今日精力已耗尽，请明天再来。", 0, null);
            return;
        }

        // 2. 隐藏初始的主交互菜单 (但不清空数据，方便后续返回)
        panelRoot.SetActive(false); 

        // 3. 呼出专门的贴脸赠礼面板
        if (UI_GiftMenu.Instance != null)
        {
            UI_GiftMenu.Instance.OpenMenu(currentTarget);
        }
        else
        {
            Debug.LogError("场景中缺失 UI_GiftMenu 面板！");
        }
    }

    private void OnTradeClicked()
    {
        if (currentTarget.linkedShop != null && UI_Shop.Instance != null)
            UI_Shop.Instance.OpenShop(currentTarget.linkedShop);
        CloseMenu();
    }

    private void OnCombatClicked()
    {
        if (BattleManager.Instance != null) BattleManager.Instance.StartBattle(currentTarget);
        CloseMenu();
    }

    private void OnTouchClicked()
    {
        // 隐藏主菜单
        panelRoot.SetActive(false); 

        // 呼出触摸房间
        if (UI_TouchRoom.Instance != null)
        {
            UI_TouchRoom.Instance.OpenMenu(currentTarget);
        }
        else
        {
            Debug.LogError("场景中缺失 UI_TouchRoom 面板！");
        }
    }
}