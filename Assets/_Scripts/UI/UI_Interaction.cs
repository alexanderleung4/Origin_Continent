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

    private CharacterData currentTarget;

    private void Start()
    {
        if(closeButton) closeButton.onClick.AddListener(CloseMenu);
        if(talkButton) talkButton.onClick.AddListener(OnTalkClicked);
        if(tradeButton) tradeButton.onClick.AddListener(OnTradeClicked);
        if(combatButton) combatButton.onClick.AddListener(OnCombatClicked);
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
            // 情况A: 有剧情且没做过 -> 播放剧情 CSV
            Debug.Log($"[Dialogue] 播放新剧情: {currentTarget.currentStoryCSV}");
            DialogueManager.Instance.StartDialogueCSV(currentTarget.currentStoryCSV);
        }
        else if (currentTarget.defaultDialogue != null)
        {
            // 情况B: 剧情做完了 或 根本没剧情 -> 播放默认闲聊
            if (isStoryFinished) Debug.Log("[Dialogue] 剧情已过，播放闲聊。");
            DialogueManager.Instance.StartDialogue(currentTarget.defaultDialogue);
        }
        else
        {
            Debug.LogWarning("该角色无话可说。");
        }
            
        CloseMenu(); 
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
}