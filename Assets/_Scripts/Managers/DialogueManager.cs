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
    public Image cgBackground;            // 全屏 CG 图像组件
    public CanvasGroup uiContentGroup;    // 包含名字、对话框和立绘的父节点 (用于一键隐藏)
    public Button btnRestoreFullUI;       // 全屏透明恢复按钮 (平时隐藏，UI消失时激活)

    [Header("Portraits (双轨立绘)")]
    public Image portraitLeft;            // 左侧立绘 (专门留给 Player)
    public Image portraitRight;           // 右侧立绘 (专门留给 NPC)
    public Color activeColor = Color.white;                   // 说话时的颜色 (高亮)
    public Color inactiveColor = new Color(0.5f, 0.5f, 0.5f); // 聆听时的颜色 (变暗)
    [Header("VFX (演出特效)")]
    public Image flashOverlay;  // 全屏闪白用的纯白Image，平时SetActive(false)

    // --- 状态流转控制 ---
    private Queue<DialogueLine> linesQueue = new Queue<DialogueLine>();
    // 👇 新增：用于内部跨行跳转的完整表单缓存
    private List<DialogueLine> currentDialogueList = new List<DialogueLine>();
    // 👇 新增：缓存当前句子的选项
    private List<DialogueChoice> currentChoices;
    public bool IsActive { get; private set; }
    public bool IsImmersiveMode { get; set; } = false; // 沉浸模式：开启时仅显示对话框，隐藏立绘
    
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
        if (cgBackground != null) cgBackground.gameObject.SetActive(false);
        if (btnRestoreFullUI != null) 
        {
            btnRestoreFullUI.gameObject.SetActive(false);
            btnRestoreFullUI.onClick.AddListener(ShowUI);
        }
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

    public void StartDialogue(string dialogueID)
    {
        if (string.IsNullOrEmpty(dialogueID)) return;
        DialogueData data = Resources.Load<DialogueData>($"Dialogues/{dialogueID}");
        if (data != null) StartDialogue(data);
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

        // 进场时重置全屏 CG 与 UI 显示状态
        if (cgBackground != null) cgBackground.gameObject.SetActive(false);
        if (uiContentGroup != null)
        {
            uiContentGroup.alpha = 1f;
            uiContentGroup.blocksRaycasts = true;
        }

        linesQueue.Clear();
        currentDialogueList.Clear(); // 清空旧表
        foreach (var line in lines) 
        {
            linesQueue.Enqueue(line);
            currentDialogueList.Add(line); // 存入缓存表
        }

        DisplayNextLine();
    }

    // ========================================================================
    // 2. 核心演出流转
    // ========================================================================
    
    public void HideUI()
    {
        if (uiContentGroup != null) 
        {
            uiContentGroup.alpha = 0f;
            uiContentGroup.blocksRaycasts = false;
        }
        if (btnRestoreFullUI != null) btnRestoreFullUI.gameObject.SetActive(true);
    }

    public void ShowUI()
    {
        if (uiContentGroup != null) 
        {
            uiContentGroup.alpha = 1f;
            uiContentGroup.blocksRaycasts = true;
        }
        if (btnRestoreFullUI != null) btnRestoreFullUI.gameObject.SetActive(false);
    }

    public void OnContinueClicked()
    {
        if (isTyping)
        {
            // 如果正在打字，点击则瞬间显示全句
            if (typingCoroutine != null) StopCoroutine(typingCoroutine);
            if (contentText != null) contentText.text = currentLineText;
            isTyping = false;
            // 玩家强行跳过打字后，立刻检查是否需要弹选项
            CheckAndShowChoices();
        }
        else
        {
            // 如果打字完毕，点击则播放下一句
            // (如果有选项，continueButton 已经被 CheckAndShowChoices 关了，玩家点不到这里)
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

        // --- 处理全屏背景 ---
        if (!string.IsNullOrEmpty(line.backgroundID))
        {
            string bgKey = line.backgroundID.ToLower();
            if (bgKey == "none" || bgKey == "clear")
            {
                // 强制关闭全屏 CG，切回普通立绘模式
                if (cgBackground != null) cgBackground.gameObject.SetActive(false);
            }
            else
            {
                Sprite bgSprite = Resources.Load<Sprite>($"Backgrounds/{line.backgroundID}");
                if (bgSprite != null && cgBackground != null)
                {
                    cgBackground.sprite = bgSprite;
                    cgBackground.gameObject.SetActive(true);

                    // 有全屏 CG 时，强制隐藏左右立绘
                    if (portraitLeft != null) portraitLeft.gameObject.SetActive(false);
                    if (portraitRight != null) portraitRight.gameObject.SetActive(false);
                }
            }
        }

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

        // --- C. 阵营分发与舞台表现 (有 CG 时立绘已隐藏，此处仅无 backgroundID 时显示) ---
        if (string.IsNullOrEmpty(line.backgroundID))
        {
            if (isPlayer)
            {
                SetupPortrait(portraitLeft, targetSprite, true);
                SetupPortrait(portraitRight, portraitRight != null ? portraitRight.sprite : null, false);
            }
            else
            {
                SetupPortrait(portraitRight, targetSprite, true);
                Sprite leftSprite = portraitLeft != null ? portraitLeft.sprite : null;
                if (leftSprite == null && GameManager.Instance != null && GameManager.Instance.Player != null && GameManager.Instance.Player.data != null)
                {
                    leftSprite = GameManager.Instance.Player.data.bodySprite_Normal;
                }
                SetupPortrait(portraitLeft, leftSprite, false);
            }
        }

        // --- D. 缓存选项并启动打字机 ---
        currentChoices = line.choices; // 将当前句的选项存入缓存

        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        typingCoroutine = StartCoroutine(TypeLine(currentLineText));

        // --- E. 事件触发 ---
        if (!string.IsNullOrEmpty(line.eventCommand)) HandleEvent(line.eventCommand);
        
        // --- F. Asset配置的VFX触发 ---
        if (line.vfxType != DialogueVFXType.None) HandleVFXType(line.vfxType);

        // 🛡️ 核心 UX 修复：无论有没有选项，打字期间必须保持继续按钮开启！
        // 这样玩家才能点击屏幕跳过漫长的打字过程。选项面板的呼出交由打字结束时处理。
        if (continueButton != null) continueButton.gameObject.SetActive(true);
        
    }

    // 核心枢纽：打字结束或被跳过时，检查并呼出选项
    private void CheckAndShowChoices()
    {
        if (currentChoices != null && currentChoices.Count > 0)
        {
            // 有选项：瞬间关闭继续按钮的拦截护盾，呼出专门的选项面板
            if (continueButton != null) continueButton.gameObject.SetActive(false);
            UI_ChoicePanel.Instance.ShowChoices(currentChoices, OnChoiceSelected, EndDialogue);
        }
        else
        {
            // 没选项：保持继续按钮开启，等待玩家点击进入下一句
            if (continueButton != null) continueButton.gameObject.SetActive(true);
        }
    }
    // 精准的路由与结算枢纽
    private void OnChoiceSelected(DialogueChoice choice)
    {
        // 1. 点击瞬间结算事件 (比如扣除金币、加好感度)
        if (!string.IsNullOrEmpty(choice.eventCommand)) HandleEvent(choice.eventCommand);

        // 2. 路由跳转逻辑
        if (choice.nextAsset != null)
        {
            StartDialogue(choice.nextAsset); // 直接切 Asset
        }
        else if (!string.IsNullOrEmpty(choice.nextID))
        {
            // 尝试在当前 CSV 缓存表中寻找目标 ID
            int jumpIndex = currentDialogueList.FindIndex(l => l.lineID == choice.nextID);
            
            if (jumpIndex != -1)
            {
                // 表内跳转：重构队列，不会丢失当前CSV的上下文
                linesQueue.Clear();
                for (int i = jumpIndex; i < currentDialogueList.Count; i++)
                {
                    linesQueue.Enqueue(currentDialogueList[i]);
                }
                DisplayNextLine();
            }
            else
            {
                // 找不到，说明是跨表路由！加载新 CSV
                StartDialogueCSV(choice.nextID);
            }
        }
        else
        {
            // 没有配置下文，直接关闭对话
            EndDialogue();
        }
    }

    // 辅助方法：设置单个立绘的状态
    private void SetupPortrait(Image img, Sprite sprite, bool isSpeaking)
    {
        if (img == null) return;
        // 👇 核心拦截：如果开启了沉浸模式，强制隐藏所有默认立绘
        if (IsImmersiveMode)
        {
            img.gameObject.SetActive(false);
            return;
        }

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
        // 自然打字结束时，检查是否需要弹选项
        CheckAndShowChoices();
    }

    private void EndDialogue()
    {
        IsActive = false;
        IsImmersiveMode = false; // 退出对话时强制重置，防止污染其他正常对话
        // 离场时重置全屏 CG 与 UI 显示状态，防止残留与假死
        if (cgBackground != null) cgBackground.gameObject.SetActive(false);
        if (uiContentGroup != null)
        {
            uiContentGroup.alpha = 1f;
            uiContentGroup.blocksRaycasts = true;
        }
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Dialogue) 
        {
            GameManager.Instance.ChangeState(GameState.Exploration);
        }
    }

    // ========================================================================
    // 3. 事件解析引擎 (完全保留您的架构)
    // ========================================================================
    private void HandleVFXType(DialogueVFXType vfx)
    {
        switch (vfx)
        {
            case DialogueVFXType.FadeIn:
                if (SceneFader.Instance != null) StartCoroutine(SceneFader.Instance.FadeIn());
                break;
            case DialogueVFXType.FadeOut:
                if (SceneFader.Instance != null) SceneFader.Instance.FadeAndExecute(() => { });
                break;
            case DialogueVFXType.ShakeLight:
                if (VFXManager.Instance != null && uiContentGroup != null)
                    VFXManager.Instance.ShakeUnit(uiContentGroup.gameObject, 0.3f, 5f);
                break;
            case DialogueVFXType.ShakeHeavy:
                if (VFXManager.Instance != null && uiContentGroup != null)
                    VFXManager.Instance.ShakeUnit(uiContentGroup.gameObject, 0.3f, 12f);
                break;
            case DialogueVFXType.FlashWhite:
                StartCoroutine(DialogueVFX.FlashWhite(flashOverlay));
                break;
            case DialogueVFXType.PortraitSlideIn:
                if (portraitLeft != null && portraitLeft.gameObject.activeSelf)
                    StartCoroutine(DialogueVFX.SlideIn(portraitLeft.rectTransform, true));
                if (portraitRight != null && portraitRight.gameObject.activeSelf)
                    StartCoroutine(DialogueVFX.SlideIn(portraitRight.rectTransform, false));
                break;
            case DialogueVFXType.PortraitSlideOut:
                if (portraitLeft != null && portraitLeft.gameObject.activeSelf)
                    StartCoroutine(DialogueVFX.SlideOut(portraitLeft.rectTransform, true));
                if (portraitRight != null && portraitRight.gameObject.activeSelf)
                    StartCoroutine(DialogueVFX.SlideOut(portraitRight.rectTransform, false));
                break;
            case DialogueVFXType.BGFade:
                if (cgBackground != null)
                    StartCoroutine(DialogueVFX.CrossFadeBackground(cgBackground, cgBackground.sprite));
                break;
        }
    }



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
            // ==========================================
            // 人事调动指令 (Recruitment & Roster)
            // ==========================================
            case "JoinParty":
                if (GameManager.Instance != null) 
                {
                    GameManager.Instance.RecruitCharacter(value);
                }
                break;
                
            case "LeaveParty":
                if (GameManager.Instance != null) 
                {
                    GameManager.Instance.LeaveParty(value);
                }
                break;
            case "Fade":
                if (SceneFader.Instance != null)
                {
                    if (value == "in") StartCoroutine(SceneFader.Instance.FadeIn());
                    else if (value == "out") SceneFader.Instance.FadeAndExecute(() => { });
                }
                break;

            case "Shake":
                if (VFXManager.Instance != null)
                {
                    float strength = (value == "heavy") ? 12f : 5f;
                    if (uiContentGroup != null)
                        VFXManager.Instance.ShakeUnit(uiContentGroup.gameObject, 0.3f, strength);
                }
                break;

            case "Flash":
                if (value == "white")
                    StartCoroutine(DialogueVFX.FlashWhite(flashOverlay));
                break;

            case "Portrait":
                if (value == "slidein")
                {
                    if (portraitLeft != null && portraitLeft.gameObject.activeSelf)
                        StartCoroutine(DialogueVFX.SlideIn(portraitLeft.rectTransform, true));
                    if (portraitRight != null && portraitRight.gameObject.activeSelf)
                        StartCoroutine(DialogueVFX.SlideIn(portraitRight.rectTransform, false));
                }
                else if (value == "slideout")
                {
                    if (portraitLeft != null && portraitLeft.gameObject.activeSelf)
                        StartCoroutine(DialogueVFX.SlideOut(portraitLeft.rectTransform, true));
                    if (portraitRight != null && portraitRight.gameObject.activeSelf)
                        StartCoroutine(DialogueVFX.SlideOut(portraitRight.rectTransform, false));
                }
                break;

            case "BG":
                if (value == "fade" && cgBackground != null)
                    StartCoroutine(DialogueVFX.CrossFadeBackground(cgBackground, cgBackground.sprite));
                break;
            // 新增好感度解析指令 -> Affinity:Luna:Intimacy:5
            case "Affinity":
                if (parts.Length >= 4)
                {
                    string charID = parts[1].Trim();
                    if (System.Enum.TryParse(parts[2].Trim(), out AffinityType affType) && 
                        int.TryParse(parts[3].Trim(), out int amount))
                    {
                        if (AffinityManager.Instance != null)
                            AffinityManager.Instance.AddAffinity(charID, affType, amount);
                    }
                }
                break;
                            
            }

        if (UIManager.Instance != null) UIManager.Instance.RefreshPlayerStatus();
    }
}