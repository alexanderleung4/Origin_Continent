using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("UI References (UI 引用)")]
    public GameObject dialoguePanel;      // 整个对话系统的根节点 (Panel_DialogueRoot)
    public TextMeshProUGUI speakerText;   // 说话人名字
    public TextMeshProUGUI contentText;   // 说话内容
    public Button continueButton;         // 继续/跳过按钮 (建议全屏透明)

    [Header("Portraits (双轨立绘)")]
    public Image portraitLeft;            // 左侧立绘 (专门留给 Player)
    public Image portraitRight;           // 右侧立绘 (专门留给 NPC)
    public Color activeColor = Color.white;                   // 说话时的颜色 (高亮)
    public Color inactiveColor = new Color(0.5f, 0.5f, 0.5f); // 聆听时的颜色 (变暗)

    // --- 状态流转控制 ---
    private Queue<DialogueLine> linesQueue = new Queue<DialogueLine>();
    public bool IsActive { get; private set; }
    
    // --- 打字机系统 ---
    private bool isTyping = false;
    private string currentLineText = "";
    private Coroutine typingCoroutine;
    private float typeSpeed = 0.03f; // 每个字的显示间隔

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // 绑定点击事件：根据当前状态决定是“跳过打字”还是“下一句”
        if (continueButton != null) 
            continueButton.onClick.AddListener(OnContinueClicked);
            
        EndDialogue();
    }

    // ========================================================================
    // 1. 外部调用接口
    // ========================================================================
    public void StartDialogue(DialogueData data)
    {
        if (data == null || data.lines == null || data.lines.Count == 0) return;
        StartDialogueInternal(data.lines);
    }

    public void StartDialogueCSV(string csvFileName)
    {
        List<DialogueLine> loadedLines = CSVLoader.LoadCSV(csvFileName);
        if (loadedLines == null || loadedLines.Count == 0) return;
        StartDialogueInternal(loadedLines);
    }

    private void StartDialogueInternal(List<DialogueLine> lines)
    {
        IsActive = true;
        dialoguePanel.SetActive(true);
        if (GameManager.Instance != null) GameManager.Instance.ChangeState(GameState.Dialogue);

        // 进场时，清空残像
        if (portraitLeft != null) portraitLeft.gameObject.SetActive(false);
        if (portraitRight != null) portraitRight.gameObject.SetActive(false);

        linesQueue.Clear();
        foreach (var line in lines) linesQueue.Enqueue(line);

        DisplayNextLine();
    }

    // ========================================================================
    // 2. 核心演出流转
    // ========================================================================
    
    public void OnContinueClicked()
    {
        if (isTyping)
        {
            // 如果正在打字，点击则瞬间显示全句
            if (typingCoroutine != null) StopCoroutine(typingCoroutine);
            if (contentText != null) contentText.text = currentLineText;
            isTyping = false;
        }
        else
        {
            // 如果打字完毕，点击则播放下一句
            DisplayNextLine();
        }
    }

    private void DisplayNextLine()
    {
        if (linesQueue.Count == 0)
        {
            EndDialogue();
            return;
        }

        DialogueLine line = linesQueue.Dequeue();

        // --- A. 动态文本解析 (处理主角名字) ---
        string displayName = line.speakerName;
        currentLineText = line.content;

        // 判定当前说话人是否为玩家
        bool isPlayer = (line.speakerName == "Player");

        if (GameManager.Instance != null && GameManager.Instance.Player != null)
        {
            string playerName = GameManager.Instance.Player.Name;
            if (isPlayer) displayName = playerName;
            
            // 替换台词内容里的占位符
            currentLineText = currentLineText.Replace("{Player}", playerName);
            currentLineText = currentLineText.Replace("{PlayerName}", playerName);
        }

        if (speakerText != null) speakerText.text = displayName;

        // --- B. 差分立绘寻址 (Speaker + Expression) ---
        Sprite targetSprite = line.portrait; 

        // 拼接您的差分文件名，比如 "Merchant" + "Smile" -> "Merchant_Smile"
        string portraitID = line.speakerName;
        if (!string.IsNullOrEmpty(line.expression))
        {
            portraitID = $"{line.speakerName}_{line.expression}";
        }

        // 如果没有手动填入图片，则去 Resources/Portraits 里面按差分 ID 找
        if (targetSprite == null)
        {
            targetSprite = Resources.Load<Sprite>($"Portraits/{portraitID}");
        }

        // 🛡️ 玩家终极兜底：万一 Resources 里的 "Player_Normal" 或差分图没找到，直接从内存拿肉身立绘！
        if (targetSprite == null && isPlayer && GameManager.Instance != null && GameManager.Instance.Player.data != null)
        {
            targetSprite = GameManager.Instance.Player.data.bodySprite_Normal;
        }

        // --- C. 阵营分发与舞台表现 ---
        if (isPlayer)
        {
            // 玩家说话：左侧高亮
            SetupPortrait(portraitLeft, targetSprite, true);
            // 右侧 NPC 聆听 (维持原图并变暗)
            SetupPortrait(portraitRight, portraitRight != null ? portraitRight.sprite : null, false);
        }
        else
        {
            // NPC说话：右侧高亮
            SetupPortrait(portraitRight, targetSprite, true);
            
            // 👇 绝杀白方块的核心：如果 NPC 先说话，而左侧玩家目前是空的，
            // 我们主动去拿玩家的默认立绘，让他作为“聆听者”完美登场！
            Sprite leftSprite = portraitLeft != null ? portraitLeft.sprite : null;
            if (leftSprite == null && GameManager.Instance != null && GameManager.Instance.Player != null && GameManager.Instance.Player.data != null)
            {
                leftSprite = GameManager.Instance.Player.data.bodySprite_Normal;
            }
            SetupPortrait(portraitLeft, leftSprite, false);
        }

        // --- D. 启动打字机 ---
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        typingCoroutine = StartCoroutine(TypeLine(currentLineText));

        // --- E. 事件触发 ---
        if (!string.IsNullOrEmpty(line.eventCommand))
        {
            HandleEvent(line.eventCommand);
        }
    }

    // 辅助方法：设置单个立绘的状态
    private void SetupPortrait(Image img, Sprite sprite, bool isSpeaking)
    {
        if (img == null) return;
        if (!isSpeaking && !img.gameObject.activeSelf)
        {
            return;
        }

        if (sprite != null)
        {
            img.sprite = sprite;
            img.color = isSpeaking ? activeColor : inactiveColor;
            img.gameObject.SetActive(true);
            
            // 💡 进阶预留：如果想要做微动动画，可以在 isSpeaking == true 时在这里调用
            // if (isSpeaking) PlayBounceAnim(img.transform);
        }
        else
        {
            // 如果既没有配表，Resource 里也找不到，说明这是个没脸的 NPC，直接隐藏
            if (isSpeaking) img.gameObject.SetActive(false); 
        }
    }

    // --- 打字机协程 ---
    private IEnumerator TypeLine(string text)
    {
        isTyping = true;
        if (contentText != null) contentText.text = "";

        foreach (char c in text.ToCharArray())
        {
            if (contentText != null) contentText.text += c;
            
            // 💡 预留音效：如果要加滴滴滴的打字音效，可以在这里播放
            // AudioManager.Instance.PlayTypewriterSound();

            yield return new WaitForSeconds(typeSpeed);
        }
        isTyping = false;
    }

    private void EndDialogue()
    {
        IsActive = false;
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Dialogue) 
        {
            GameManager.Instance.ChangeState(GameState.Exploration);
        }
    }

    // ========================================================================
    // 3. 事件解析引擎 (完全保留您的架构)
    // ========================================================================
    private void HandleEvent(string command)
    {
        Debug.Log($"[Dialogue] 触发事件: {command}");
        string[] parts = command.Split(':');
        if (parts.Length < 1) return;

        string type = parts[0].Trim();
        string value = parts.Length > 1 ? parts[1].Trim() : "";

        switch (type)
        {
            case "Gold":
                if (int.TryParse(value, out int gold)) GameManager.Instance.Player.Gold += gold;
                break;
            case "Exp":
                if (int.TryParse(value, out int exp)) GameManager.Instance.Player.GainExp(exp);
                break;
            case "Stamina":
                if (int.TryParse(value, out int stam)) GameManager.Instance.Player.CurrentStamina += stam; 
                break;
            case "Heal":
                GameManager.Instance.Player.RestoreStats();
                break;
            case "Item":
                ItemData item = Resources.Load<ItemData>($"Items/{value}");
                if (item != null) InventoryManager.Instance.AddItem(item, 1);
                break;
            case "Shop":
                ShopData shop = Resources.Load<ShopData>($"Shops/{value}");
                if (shop != null) { EndDialogue(); UI_Shop.Instance.OpenShop(shop); }
                break;
            case "Battle":
                CharacterData enemy = Resources.Load<CharacterData>($"Enemies/{value}");
                if (enemy != null) { EndDialogue(); BattleManager.Instance.StartBattle(enemy); }
                break;
            // 多敌人阵列关卡入口
            case "Stage":
                StageData stage = Resources.Load<StageData>($"Stages/{value}");
                if (stage != null) { EndDialogue(); BattleManager.Instance.StartBattle(stage); }
                else Debug.LogError($"找不到关卡文件: Resources/Stages/{value}");
                break;
            
            case "Finish": 
                GameManager.Instance.AddEvent(value);
                break;
            case "AcceptQuest": 
                QuestManager.Instance.AcceptQuest(value);
                break;
            case "SubmitQuest": 
                QuestManager.Instance.SubmitQuest(value);
                break;
            case "Report": 
                 QuestManager.Instance.OnNPCInteracted(value);
                break;        
        }

        if (UIManager.Instance != null) UIManager.Instance.RefreshPlayerStatus();
    }
}