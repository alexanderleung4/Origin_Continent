using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public enum BattleState { Start, PlayerTurn, PlayerMenu, SelectTarget, PlayerAction, EnemyTurn, Won, Lost }

// 👇 新增：用来把数据和预制体绑在一起的容器
[System.Serializable]
public class BattleEntity
{
    public RuntimeCharacter runtime;
    public UI_BattleEntity uiEntity;
    public bool isPlayerSide;
    public bool isDead => runtime.CurrentHP <= 0;
}

public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }
    public BattleState state;

    [Header("Audio")]
    public AudioClip defaultBattleBGM; 

    [Header("Prefabs")]
    public GameObject skillButtonPrefab;

    // 👇 修改：从单体变成了阵列列表
    public List<BattleEntity> allEntities = new List<BattleEntity>();
    private BattleEntity currentActor;   // 当前行动者
    private BattleEntity currentTarget;  // 当前选中的目标

    private SkillData pendingSkill;

    private struct PendingCG
    {
        public string eventID;
        public BattleEntity victim;
    }

    private List<PendingCG> pendingCGs = new List<PendingCG>();

    private UI_Battle ui 
    {
        get 
        {
            if (UIManager.Instance == null) return null;
            return UIManager.Instance.battleUI;
        }
    }

    private GameObject MainHUDRoot => UIManager.Instance != null ? UIManager.Instance.mainHUD_Ref : null;
    private GameObject BattleHUDRoot => UIManager.Instance != null ? UIManager.Instance.battleHUD_Ref : null;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            OnBack();
        }
    }

    // ========================================================================
    // 1. 兼容性入口 (保持对话系统不报错)
    // ========================================================================
    public void StartBattle(CharacterData enemyData)
    {
        StageData dummyStage = ScriptableObject.CreateInstance<StageData>();
        dummyStage.stageName = "突发战斗";
        dummyStage.enemies = new List<EnemySpawnInfo> { new EnemySpawnInfo { enemyData = enemyData, slotIndex = 1 } };
        StartBattle(dummyStage);
    }

    public void StartBattle(StageData stageData)
    {
        if (MainHUDRoot == null || BattleHUDRoot == null) return;

        if (SceneFader.Instance != null)
        {
            SceneFader.Instance.FadeAndExecute(() => 
            {
                InitBattleLogic(stageData);
                if (AudioManager.Instance != null && defaultBattleBGM != null)
                    AudioManager.Instance.PlayMusic(defaultBattleBGM);
            });
        }
        else InitBattleLogic(stageData);
    }

    private void InitBattleLogic(StageData stage)
    {
        if (GameManager.Instance != null) GameManager.Instance.ChangeState(GameState.Battle);
        
        state = BattleState.Start;
        allEntities.Clear();

        MainHUDRoot.SetActive(false);
        BattleHUDRoot.SetActive(true);

        if (ui != null)
        {
            if (ui.actionPanel != null) ui.actionPanel.SetActive(false);
            if (ui.skillPanel != null) ui.skillPanel.SetActive(false);
        }

        SetupBattle(stage);
    }

    // ========================================================================
    // 2. 阵列生成与动态绑定 (核心外科手术区域)
    // ========================================================================
    private void SetupBattle(StageData stage)
    {
        if (ui == null) return;

        // 清理旧的预制体
        foreach (Transform slot in ui.playerSlots) foreach (Transform child in slot) Destroy(child.gameObject);
        foreach (Transform slot in ui.enemySlots) foreach (Transform child in slot) Destroy(child.gameObject);

        // 生成玩家 (目前固定放前排中间 Slot 1)
        SpawnEntity(GameManager.Instance.Player, true, 1);

        // 生成敌人
        foreach (var spawn in stage.enemies)
        {
            if (spawn.enemyData != null)
            {
                RuntimeCharacter enemyRuntime = new RuntimeCharacter(spawn.enemyData);
                SpawnEntity(enemyRuntime, false, spawn.slotIndex);
            }
        }

        // 绑定底部固定按钮
        if (ui.btnItem != null) { ui.btnItem.onClick.RemoveAllListeners(); ui.btnItem.onClick.AddListener(OnItemButtonClicked); }
        if (ui.btnRun != null) { ui.btnRun.onClick.RemoveAllListeners(); ui.btnRun.onClick.AddListener(OnRunButtonClicked); }

        if (ui.actionCategoryButtons != null)
        {
            for (int i = 0; i < ui.actionCategoryButtons.Count; i++)
            {
                int index = i; 
                Button btn = ui.actionCategoryButtons[i];
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => OnCategoryButton(index)); 
                }
            }
        }

        UpdateStatsUI();
        LogBattle($"遭遇强敌: {stage.stageName} !");
        
        // --- 👇 赋予所有人初始时间并启动时间轴！ ---
        foreach (var entity in allEntities)
        {
            if (!entity.isDead) entity.runtime.InitializeAV();
        }
        
        AdvanceTimeline(); // 让引擎决定谁先出手！
    }

    private void SpawnEntity(RuntimeCharacter runtime, bool isPlayerSide, int slotIndex)
    {
        Transform[] slots = isPlayerSide ? ui.playerSlots : ui.enemySlots;
        if (slotIndex < 0 || slotIndex >= slots.Length || slots[slotIndex] == null) return;

        GameObject obj = Instantiate(ui.battleEntityPrefab, slots[slotIndex]);
        UI_BattleEntity uiEntity = obj.GetComponent<UI_BattleEntity>();
        
        if (uiEntity != null)
        {
            uiEntity.nameText.text = runtime.Name;
            if (runtime.data.bodySprite_Normal != null) uiEntity.bodyImage.sprite = runtime.data.bodySprite_Normal;
            if (!isPlayerSide) obj.transform.localScale = Vector3.one * runtime.data.visualScale;
        }

        BattleEntity entity = new BattleEntity { runtime = runtime, uiEntity = uiEntity, isPlayerSide = isPlayerSide };
        allEntities.Add(entity);

        // 👇 魔法在此！动态为预制体赋予点击事件
        Button btn = obj.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(() => OnEntityClicked(entity));
        }
    }
    // ========================================================================
    // --- CTB 时间轴推进引擎 ---
    // ========================================================================
    private void AdvanceTimeline()
    {
        if (CheckBattleEnd()) return;

        LogBattle("时间轴推进中...");

        // 1. 寻找当前距离终点最近（CurrentAV 最小）的单位
        float minAV = float.MaxValue;
        foreach (var entity in allEntities)
        {
            if (entity.isDead) continue;
            if (entity.runtime.CurrentAV < minAV) minAV = entity.runtime.CurrentAV;
        }

        // 2. 所有人同时在时间轴上往前跑 minAV 的距离
        foreach (var entity in allEntities)
        {
            if (entity.isDead) continue;
            entity.runtime.CurrentAV -= minAV;
        }

        // 3. 找出 CurrentAV 归零的那个幸运儿（如果有多个同时归零，按速度快的或者玩家优先，这里取第一个找到的）
        currentActor = null;
        foreach (var entity in allEntities)
        {
            if (!entity.isDead && Mathf.Approximately(entity.runtime.CurrentAV, 0f))
            {
                currentActor = entity;
                break;
            }
        }

        if (currentActor == null) 
        {
            LogBattle("时间轴计算异常！");
            return;
        }
        // 在回合真正开始前，调用沙盘推演刷新右侧面板
        UpdateTimelineUI();

        // 4. 将回合交给幸运儿！
        if (currentActor.isPlayerSide)
        {
            PlayerTurn();
        }
        else
        {
            StartCoroutine(EnemyTurn());
        }
    }
    // ========================================================================
    // --- 视觉化：CTB 虚拟沙盘推演 ---
    // ========================================================================
    
    // 1. 定义一个仅用于推演的虚拟节点
    private class AVSimulationNode
    {
        public BattleEntity entity;
        public float simulatedAV;
    }

    // 2. 核心算法：预测未来并刷新 UI
    public void UpdateTimelineUI(int predictCount = 8)
    {
        if (ui == null || ui.timelineContainer == null || ui.timelineIconPrefab == null) return;

        // A. 建立虚拟沙盘，把所有活着的人拉进来
        List<AVSimulationNode> simNodes = new List<AVSimulationNode>();
        foreach (var e in allEntities)
        {
            if (!e.isDead) simNodes.Add(new AVSimulationNode { entity = e, simulatedAV = e.runtime.CurrentAV });
        }

        if (simNodes.Count == 0) return;

        // 用来存放预测结果的名单
        List<BattleEntity> turnOrder = new List<BattleEntity>();

        // B. 开始往未来推演 predictCount 个回合
        for (int i = 0; i < predictCount; i++)
        {
            // 找出沙盘里距离终点最近的人
            float minAV = float.MaxValue;
            AVSimulationNode nextNode = null;

            foreach (var node in simNodes)
            {
                if (node.simulatedAV < minAV)
                {
                    minAV = node.simulatedAV;
                    nextNode = node;
                }
            }

            if (nextNode == null) break;

            // 让沙盘里的所有人都往前跑 minAV 的距离
            foreach (var node in simNodes) node.simulatedAV -= minAV;

            // 记录下这个跑赢了的人
            turnOrder.Add(nextNode.entity);

            // 虚拟重置：让这个人回到起跑线，准备参与下一轮的推演竞争！
            nextNode.simulatedAV = nextNode.entity.runtime.BaseAV; 
        }

        // C. 将预测名单实体化到 UI 上
        foreach (Transform child in ui.timelineContainer) Destroy(child.gameObject); // 清空旧头像

        foreach (var entity in turnOrder)
        {
            GameObject iconObj = Instantiate(ui.timelineIconPrefab, ui.timelineContainer);
            UI_TimelineIcon iconScript = iconObj.GetComponent<UI_TimelineIcon>();

            if (iconScript != null)
            {
                // 设置立绘
                if (entity.runtime.data.portrait != null) iconScript.avatarImage.sprite = entity.runtime.data.portrait;
                
                // 边框变色：玩家是蓝色，敌人是红色
                if (iconScript.frameImage != null)
                    iconScript.frameImage.color = entity.isPlayerSide ? new Color(0.2f, 0.6f, 1f, 1f) : new Color(1f, 0.3f, 0.3f, 1f); 
            }
        }
    }

    // ========================================================================
    // 3. 目标选取与点击逻辑
    // ========================================================================
    public void OnEntityClicked(BattleEntity clickedEntity)
    {
        if (clickedEntity.isPlayerSide && state == BattleState.PlayerTurn && clickedEntity == currentActor)
        {
            // 唤出菜单
            state = BattleState.PlayerMenu;
            if (ui != null)
            {
                if (ui.actionPanel != null) ui.actionPanel.SetActive(true);
                if (ui.playerAvatarImage != null) { ui.playerAvatarImage.sprite = currentActor.runtime.data.portrait; ui.playerAvatarImage.gameObject.SetActive(true); }
            }
            LogBattle("请选择行动...");
        }
        else if (state == BattleState.SelectTarget && !clickedEntity.isDead)
        {
            if (pendingSkill == null) return;

            // 👇 严格校验玩家点击的目标阵营对不对！
            if (pendingSkill.targetScope == TargetScope.Single_Enemy && clickedEntity.isPlayerSide) { LogBattle("该技能只能对敌人释放！"); return; }
            if (pendingSkill.targetScope == TargetScope.Single_Ally && !clickedEntity.isPlayerSide) { LogBattle("该技能只能对队友释放！"); return; }

            state = BattleState.PlayerAction;
            // 将点中的这1个人包装成 List 传给执行引擎
            StartCoroutine(ExecuteMove(currentActor, new List<BattleEntity> { clickedEntity }, pendingSkill, true));
            pendingSkill = null;
        }
    }
    // 辅助获取方法
    private BattleEntity GetFirstAlivePlayer() { foreach (var e in allEntities) if (e.isPlayerSide && !e.isDead) return e; return null; }
    private BattleEntity GetFirstAliveEnemy() { foreach (var e in allEntities) if (!e.isPlayerSide && !e.isDead) return e; return null; }
// ================== 新增：群体索敌辅助 ==================
    private List<BattleEntity> GetAllAliveEnemies()
    {
        List<BattleEntity> list = new List<BattleEntity>();
        foreach (var e in allEntities) if (!e.isPlayerSide && !e.isDead) list.Add(e);
        return list;
    }

    private List<BattleEntity> GetAllAlivePlayers()
    {
        List<BattleEntity> list = new List<BattleEntity>();
        foreach (var e in allEntities) if (e.isPlayerSide && !e.isDead) list.Add(e);
        return list;
    }

    // ========================================================================
    // 4. 回合逻辑与技能 (基本原封不动)
    // ========================================================================
    private void PlayerTurn()
    {
        if (currentActor == null) return;
        currentActor.runtime.TickBuffs();
        LogBattle($"轮到 {currentActor.runtime.Name} 的回合");
        state = BattleState.PlayerTurn;
        
        if (ui != null)
        {
            if (ui.actionPanel != null) ui.actionPanel.SetActive(false);
            if (ui.playerAvatarImage != null) ui.playerAvatarImage.gameObject.SetActive(false);
            
            if (VFXManager.Instance != null && currentActor.uiEntity.bodyImage != null)
                VFXManager.Instance.FlashUnit(currentActor.uiEntity.bodyImage, Color.white, 0.5f);
        }
    }

    public void OnBack()
    {
        if (ui == null) return;

        if (state == BattleState.SelectTarget)
        {
            pendingSkill = null;
            state = BattleState.PlayerMenu;
            LogBattle("取消目标选择。");
            if (ui.skillPanel != null) ui.skillPanel.SetActive(false);
            if (ui.actionPanel != null) ui.actionPanel.SetActive(true);
        }
        else if (state == BattleState.PlayerMenu && ui.skillPanel.activeSelf)
        {
            if (ui.skillPanel != null) ui.skillPanel.SetActive(false);
            if (ui.actionPanel != null) ui.actionPanel.SetActive(true);
        }
        else if (state == BattleState.PlayerMenu)
        {
            state = BattleState.PlayerTurn;
            if (ui.actionPanel != null) ui.actionPanel.SetActive(false);
            if (ui.playerAvatarImage != null) ui.playerAvatarImage.gameObject.SetActive(false);
            LogBattle($"轮到 {currentActor.runtime.Name} 的回合");
        }
    }

    public void OnCategoryButton(int categoryIndex) { if (state == BattleState.PlayerMenu) ShowSkills((SkillCategory)categoryIndex); }

    private void ShowSkills(SkillCategory category)
    {
        if (ui == null || ui.skillContainer == null) return;
        foreach (Transform child in ui.skillContainer) Destroy(child.gameObject);
        if (ui.skillPanel != null) ui.skillPanel.SetActive(true);

        foreach (SkillData skill in currentActor.runtime.data.startingSkills)
        {
            if (skill.category == category) CreateSkillButton(skill);
        }
    }

    private void CreateSkillButton(SkillData skill)
    {
        if (ui.skillContainer == null) return;
        GameObject btnObj = Instantiate(skillButtonPrefab, ui.skillContainer);
        
        // 👇 新增：动态拼接消耗文本，并赋予颜色标识
        string costText = "";
        if (skill.damageType == DamageType.Magical && skill.mpCost > 0)
        {
            // 魔法技能显示蓝色 MP
            costText = $" <color=#4A90E2>({skill.mpCost} MP)</color>";
        }
        else if (skill.damageType == DamageType.Physical && skill.staminaCost > 0)
        {
            // 物理技能显示橙色 精力
            costText = $" <color=#F5A623>({skill.staminaCost} SP)</color>";
        }

        // 把名字和消耗拼在一起显示
        btnObj.GetComponentInChildren<TextMeshProUGUI>().text = skill.skillName + costText;
        
        btnObj.GetComponent<Button>().onClick.AddListener(() => OnSkillSelected(skill));
    }

    public void OnSkillSelected(SkillData skill)
    {
        if (state != BattleState.PlayerMenu) return;

        if (skill.damageType == DamageType.Magical && currentActor.runtime.CurrentMP < skill.mpCost) { LogBattle("魔力不足！"); return; }
        if (skill.damageType == DamageType.Physical && currentActor.runtime.CurrentStamina < skill.staminaCost) { LogBattle("精力耗尽！"); return; }

        pendingSkill = skill; // 缓存准备释放的技能

        // --- 宏观索敌判断 ---
        if (skill.targetScope == TargetScope.Self)
        {
            CloseSkillPanel(); state = BattleState.PlayerAction;
            StartCoroutine(ExecuteMove(currentActor, new List<BattleEntity> { currentActor }, skill, true));
        }
        else if (skill.targetScope == TargetScope.All_Enemies || skill.targetScope == TargetScope.Random_Enemies)
        {
            CloseSkillPanel(); state = BattleState.PlayerAction;
            // AOE 或随机：直接把所有活着的敌人都作为目标池传进去
            StartCoroutine(ExecuteMove(currentActor, GetAllAliveEnemies(), skill, true));
        }
        else if (skill.targetScope == TargetScope.Single_Enemy)
        {
            state = BattleState.SelectTarget; CloseSkillPanel();
            LogBattle($"请点击你要攻击的【敌人】！");
        }
        else if (skill.targetScope == TargetScope.Single_Ally)
        {
            state = BattleState.SelectTarget; CloseSkillPanel();
            LogBattle($"请点击你要施法的【队友】！");
        }
    }
    private void CloseSkillPanel()
    {
        if (ui == null) return;
        if (ui.skillPanel != null) ui.skillPanel.SetActive(false);
        if (ui.actionPanel != null) ui.actionPanel.SetActive(false);
    }

    public void OnItemButtonClicked()
    {
        if (state != BattleState.PlayerMenu) return;
        UI_Inventory inventoryUI = FindObjectOfType<UI_Inventory>(true); 
        if (inventoryUI != null) inventoryUI.OpenMenu(); 
        else LogBattle("找不到背包面板！");
    }

    private IEnumerator EnemyTurn()
    {
        state = BattleState.EnemyTurn;
        if (ui != null && ui.playerAvatarImage != null) ui.playerAvatarImage.gameObject.SetActive(false);
        
        currentActor.runtime.TickBuffs();
        UpdateStatsUI();

        LogBattle($"轮到 {currentActor.runtime.Name} 的回合");
        yield return new WaitForSeconds(0.5f);
        LogBattle($"{currentActor.runtime.Name} 正在思考...");
        yield return new WaitForSeconds(1f);

        BattleEntity targetPlayer = GetFirstAlivePlayer();
        if (targetPlayer == null) yield break;

        SkillData chosenSkill = null;
        if (currentActor.runtime.data.aiProfile != null)
            chosenSkill = currentActor.runtime.data.aiProfile.GetAction(currentActor.runtime, targetPlayer.runtime);
        if (chosenSkill == null && currentActor.runtime.data.startingSkills.Count > 0)
            chosenSkill = currentActor.runtime.data.startingSkills[0];

        if (chosenSkill != null) 
        {
            // 👇 AI 也会使用多目标！
            List<BattleEntity> aiTargets = new List<BattleEntity>();
            if (chosenSkill.targetScope == TargetScope.Self || chosenSkill.targetScope == TargetScope.Single_Ally) aiTargets.Add(currentActor);
            else if (chosenSkill.targetScope == TargetScope.All_Enemies || chosenSkill.targetScope == TargetScope.Random_Enemies) aiTargets = GetAllAlivePlayers();
            else aiTargets.Add(targetPlayer);

            yield return StartCoroutine(ExecuteMove(currentActor, aiTargets, chosenSkill, false));
        }
        else
        {
            LogBattle($"{currentActor.runtime.Name} 呆住了。");
            yield return new WaitForSeconds(1f);
            currentActor.runtime.ResetAVAfterTurn(); AdvanceTimeline();
        }
    }
    // ========================================================================
    // 5. 战斗执行核心 (完全使用您的旧版逻辑，仅替换图片获取方式)
    // ========================================================================
    private IEnumerator ExecuteMove(BattleEntity attacker, List<BattleEntity> defenders, SkillData skill, bool isPlayerAction)
    {
        LogBattle($"{attacker.runtime.Name} 使用了 {skill.skillName} !");
        
        if (AudioManager.Instance != null && skill.sfxClip != null) AudioManager.Instance.PlaySFX(skill.sfxClip);
        if (skill.cutInImage != null && UI_CutIn.Instance != null) yield return StartCoroutine(UI_CutIn.Instance.PlayCutIn(skill, isPlayerAction));
        else yield return new WaitForSeconds(0.5f);

        foreach (SkillEffect effect in skill.effects)
        {
            int actualHits = Mathf.Max(1, effect.hitCount);
            int totalDamageDealtThisEffect = 0; 

            for (int hit = 0; hit < actualHits; hit++)
            {
                // 1. 每段攻击前，剔除刚才被打死的敌人，找出还活着的目标池
                List<BattleEntity> validDefenders = new List<BattleEntity>();
                foreach (var d in defenders) if (!d.isDead) validDefenders.Add(d);

                if (validDefenders.Count == 0 && effect.effectTarget != EffectTarget.Self) break;

                // 2. 决定本次 Hit 究竟打谁！
                List<BattleEntity> hitTargets = new List<BattleEntity>();
                if (effect.effectTarget == EffectTarget.Self)
                {
                    hitTargets.Add(attacker); // 效果微观锁定自己
                }
                else if (skill.targetScope == TargetScope.Random_Enemies)
                {
                    // 随机弹射：每次 Hit 在活人里随机抽 1 个
                    hitTargets.Add(validDefenders[UnityEngine.Random.Range(0, validDefenders.Count)]);
                }
                else
                {
                    // 单体或全体 AOE：直接把所有有效目标装入准星
                    hitTargets.AddRange(validDefenders);
                }

                int damageDealtThisHit = 0;

                // 3. 枪林弹雨！对所有瞄准的目标瞬间结算
                foreach (var realTarget in hitTargets)
                {
                    Image targetImg = realTarget.uiEntity.bodyImage; 

                    float statValue = 0;
                    switch (effect.scalingStat)
                    {
                        case ScalingStat.Attack: statValue = attacker.runtime.Attack; break;
                        case ScalingStat.Defense: statValue = attacker.runtime.Defense; break;
                        case ScalingStat.MaxHP: statValue = attacker.runtime.MaxHP; break;
                        case ScalingStat.CurrentHP: statValue = attacker.runtime.CurrentHP; break;
                        case ScalingStat.MaxMP: statValue = attacker.runtime.MaxMP; break;
                        case ScalingStat.CurrentMP: statValue = attacker.runtime.CurrentMP; break;
                        case ScalingStat.Speed: statValue = attacker.runtime.Speed; break;
                    }

                    float rawOutput = effect.baseValue + (statValue * effect.scalingMultiplier);

                    if (effect.effectType == EffectType.Damage)
                    {
                        bool isCritical = (UnityEngine.Random.value < attacker.runtime.CritRate);
                        float damageFloat = rawOutput;
                        if (isCritical) damageFloat *= attacker.runtime.CritDamage;

                        int finalDamage = Mathf.RoundToInt(damageFloat) - realTarget.runtime.Defense;
                        finalDamage = Mathf.Max(1, finalDamage);

                        string sourceID = attacker.runtime.data != null ? attacker.runtime.data.characterID ?? "" : "";
                        int actualDamageTaken = realTarget.runtime.TakeDamage(finalDamage, sourceID);
                        damageDealtThisHit += actualDamageTaken; 
                        
                        if (AudioManager.Instance != null) AudioManager.Instance.PlayHitSound();
                        LogBattle($"对 {realTarget.runtime.Name} 造成 {finalDamage} 伤害！" + (isCritical ? "【暴击】" : ""));

                        if (VFXManager.Instance != null && targetImg != null)
                        {
                            float shakeStr = isCritical ? 12f : 5f; 
                            VFXManager.Instance.ShakeUnit(targetImg.gameObject, 0.2f, shakeStr);
                            VFXManager.Instance.FlashUnit(targetImg, isCritical ? Color.yellow : Color.red);
                            
                            Transform popupPoint = realTarget.uiEntity.vfxSpawnPoint != null ? realTarget.uiEntity.vfxSpawnPoint : targetImg.transform;
                            VFXManager.Instance.ShowDamagePopup(popupPoint.position, actualDamageTaken, isCritical, false);
                            
                            if (skill.hitVFXPrefab != null) Instantiate(skill.hitVFXPrefab, targetImg.transform).transform.localPosition = Vector3.zero;
                        }
                        StartCoroutine(ReactHitSprite(realTarget));
                    }
                    
                    else if (effect.effectType == EffectType.Heal)
                    {
                        int healAmount = Mathf.RoundToInt(rawOutput);
                        realTarget.runtime.CurrentHP += healAmount;
                        if (realTarget.runtime.CurrentHP > realTarget.runtime.MaxHP) realTarget.runtime.CurrentHP = realTarget.runtime.MaxHP;
                        
                        LogBattle($"恢复 {realTarget.runtime.Name} {healAmount} 点生命！");

                        if (VFXManager.Instance != null && targetImg != null)
                        {
                            VFXManager.Instance.FlashUnit(targetImg, Color.green);
                            Transform popupPoint = realTarget.uiEntity.vfxSpawnPoint != null ? realTarget.uiEntity.vfxSpawnPoint : targetImg.transform;
                            VFXManager.Instance.ShowDamagePopup(popupPoint.position, healAmount, false, true);
                            if (skill.hitVFXPrefab != null) Instantiate(skill.hitVFXPrefab, targetImg.transform).transform.localPosition = Vector3.zero;
                        }
                    }
                    else if (effect.effectType == EffectType.ApplyBuff)
                    {
                        if (effect.buffToApply != null)
                        {
                            realTarget.runtime.ApplyBuff(effect.buffToApply, attacker.runtime);
                            LogBattle($"对 {realTarget.runtime.Name} 施加状态: {effect.buffToApply.buffName} !");
                        }
                    }
                }

                totalDamageDealtThisEffect += damageDealtThisHit;
                UpdateStatsUI();
                UpdateTimelineUI();
                if (effect.hitCount > 1) yield return new WaitForSeconds(0.15f); // 连击微小停顿
            }

            // 4. 结算这整个 Effect 累积的巨额吸血！
            if (effect.effectType == EffectType.Damage && effect.lifestealPercent > 0 && totalDamageDealtThisEffect > 0)
            {
                int lifestealAmount = Mathf.RoundToInt(totalDamageDealtThisEffect * effect.lifestealPercent);
                attacker.runtime.CurrentHP += lifestealAmount;
                if (attacker.runtime.CurrentHP > attacker.runtime.MaxHP) attacker.runtime.CurrentHP = attacker.runtime.MaxHP;
                
                LogBattle($"吸血触发，恢复 {lifestealAmount} 生命！");
                
                if (VFXManager.Instance != null && attacker.uiEntity.bodyImage != null)
                {
                    VFXManager.Instance.FlashUnit(attacker.uiEntity.bodyImage, Color.green);
                    Transform popupPoint = attacker.uiEntity.vfxSpawnPoint != null ? attacker.uiEntity.vfxSpawnPoint : attacker.uiEntity.bodyImage.transform;
                    VFXManager.Instance.ShowDamagePopup(popupPoint.position, lifestealAmount, false, true);
                }
                UpdateStatsUI();
                yield return new WaitForSeconds(0.3f);
            }
        }

        bool triggerCurse = skill.damageType == DamageType.Magical;
        if (skill.damageType == DamageType.Magical) attacker.runtime.ConsumeMana(skill.mpCost);
        else attacker.runtime.ConsumeStamina(skill.staminaCost);

        UpdateStatsUI();
        yield return new WaitForSeconds(1f);
        attacker.runtime.turnCount++;

        if (isPlayerAction && ui != null && ui.playerAvatarImage != null) ui.playerAvatarImage.gameObject.SetActive(false);

        if (!CheckBattleEnd())
        {
            attacker.runtime.ResetAVAfterTurn(); 
            AdvanceTimeline(); 
        }
    }

    private bool CheckBattleEnd()
    {
        bool playerDead = true, enemyDead = true;
        foreach (var e in allEntities)
        {
            if (e.isPlayerSide && !e.isDead) playerDead = false;
            if (!e.isPlayerSide && !e.isDead) enemyDead = false;
        }

        if (playerDead) { state = BattleState.Lost; EndBattle(); return true; }
        if (enemyDead) { state = BattleState.Won; EndBattle(); return true; }
        return false;
    }

    // ==========================================
    // 视觉表现：受击差分切换
    // ==========================================
    private IEnumerator ReactHitSprite(BattleEntity target)
    {
        if (target == null || target.isDead || target.uiEntity.bodyImage == null) yield break;

        // 切换到受击立绘
        if (target.runtime.data.bodySprite_Hit != null)
        {
            target.uiEntity.bodyImage.sprite = target.runtime.data.bodySprite_Hit;
            
            // 维持 0.4 秒的痛苦表情
            yield return new WaitForSeconds(0.4f);
            
            // 恢复前必须再次检查：万一在这 0.4 秒内它被多段伤害打死了，就绝对不能切回 Normal！
            if (!target.isDead && target.uiEntity.bodyImage != null)
            {
                target.uiEntity.bodyImage.sprite = target.runtime.data.bodySprite_Normal;
            }
        }
    }

    // ========================================================================
    // 6. UI 更新 & 结算 (完美保留原样)
    // ========================================================================
    public void UpdateStatsUI()
    {
        if (ui == null) return;
        foreach (var entity in allEntities)
        {
            UI_BattleEntity u = entity.uiEntity;

            if (entity.isDead)
            {
                if (u.bodyImage != null) 
                {
                    // 👇 核心修改：如果有死亡立绘，就换上死亡立绘，并稍微变暗(灰色)增加死亡氛围！
                    if (entity.runtime.data.bodySprite_Dead != null)
                    {
                        u.bodyImage.sprite = entity.runtime.data.bodySprite_Dead;
                        u.bodyImage.color = new Color(0.4f, 0.4f, 0.4f, 1f); // 调暗变成灰白色
                    }
                    else
                    {
                        // 如果没配死亡立绘，就老规矩直接透明蒸发
                        u.bodyImage.color = Color.clear;
                    }
                }
                
                // 彻底隐藏活人的 UI
                if (u.hpSlider != null) u.hpSlider.gameObject.SetActive(false);
                if (u.mpSlider != null) u.mpSlider.gameObject.SetActive(false);
                if (u.staminaSlider != null) u.staminaSlider.gameObject.SetActive(false); 
                if (u.shieldSlider != null) u.shieldSlider.gameObject.SetActive(false);
                if (u.nameText != null) u.nameText.gameObject.SetActive(false); 
                if (u.buffContainer != null) u.buffContainer.gameObject.SetActive(false); 
                
                // 彻底关闭点击功能，防止玩家对着尸体释放单体技能
                Button btn = u.GetComponent<Button>();
                if (btn != null) btn.interactable = false;
                
                continue;
            }

            // 👇 活着的单位：确保 UI 都是开启的（以防未来有复活技能）
            if (u.bodyImage != null) u.bodyImage.color = Color.white;
            if (u.hpSlider != null) u.hpSlider.gameObject.SetActive(true);
            if (u.mpSlider != null) u.mpSlider.gameObject.SetActive(true);
            if (u.staminaSlider != null) u.staminaSlider.gameObject.SetActive(true);
            if (u.nameText != null) u.nameText.gameObject.SetActive(true);
            if (u.buffContainer != null) u.buffContainer.gameObject.SetActive(true);
            Button activeBtn = u.GetComponent<Button>();
            if (activeBtn != null) activeBtn.interactable = true;


            // 原本的数值刷新逻辑保持不变
            RuntimeCharacter r = entity.runtime;
            // HP
            if (u.hpSlider != null) u.hpSlider.value = (float)r.CurrentHP / r.MaxHP;
            if (u.hpText != null) u.hpText.text = $"{r.CurrentHP}/{r.MaxHP}"; 

            // MP
            if (u.mpSlider != null) u.mpSlider.value = (float)r.CurrentMP / r.MaxMP;
            if (u.mpText != null) u.mpText.text = $"{r.CurrentMP}/{r.MaxMP}"; 

            // Stamina
            if (u.staminaSlider != null) 
            {
                float maxStamina = r.MaxStamina > 0 ? r.MaxStamina : 1;
                u.staminaSlider.value = (float)r.CurrentStamina / maxStamina;
            }
            if (u.staminaText != null) u.staminaText.text = $"{r.CurrentStamina}/{r.MaxStamina}";
            
            if (u.shieldSlider != null)
            {
                u.shieldSlider.maxValue = r.MaxHP;
                u.shieldSlider.value = r.CurrentShield;
                u.shieldSlider.gameObject.SetActive(r.CurrentShield > 0);
            }
            UpdateBuffUI(r, u.buffContainer);
        }
        if (UIManager.Instance != null) UIManager.Instance.RefreshPlayerStatus();
    }

    private void UpdateBuffUI(RuntimeCharacter character, Transform container)
    {
        if (container == null) return;
        foreach (Transform child in container) Destroy(child.gameObject);
        foreach (var buff in character.activeBuffs)
        {
            if (buff.data.icon != null)
            {
                GameObject iconObj = new GameObject("BuffIcon");
                iconObj.transform.SetParent(container, false);
                Image img = iconObj.AddComponent<Image>();
                img.sprite = buff.data.icon;
                LayoutElement layout = iconObj.AddComponent<LayoutElement>();
                layout.minWidth = 30; layout.minHeight = 30; layout.preferredWidth = 30; layout.preferredHeight = 30;
            }
        }
    }

    private void EndBattle()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.StopMusic();

        float waitTime = 2f; 
        if (state == BattleState.Won)
        {
            // 👇 修复：检查是否是真的杀死了敌人（而不是逃跑）
            bool actuallyWon = false;
            foreach (var e in allEntities) if (!e.isPlayerSide && e.isDead) actuallyWon = true;

            if (actuallyWon)
            {
                LogBattle("战斗胜利！");
                if (TimeManager.Instance != null)
                {
                    TimeManager.Instance.AdvanceTime(60);
                }
                if (AudioManager.Instance != null) waitTime = AudioManager.Instance.PlayCombatJingle(true) + 0.5f;
                
                int expGain = 0, goldGain = 0;
                string dropMsg = "获得战利品: ";
                bool hasDrops = false;

                // 只有被杀死的敌人才会掉落奖励，并从地图上抹除
                foreach (var enemy in allEntities)
                {
                    if (!enemy.isPlayerSide && enemy.isDead) 
                    {
                        expGain += enemy.runtime.data.killExpReward;
                        goldGain += enemy.runtime.data.killGoldReward;
                        
                        if (QuestManager.Instance != null) QuestManager.Instance.OnEnemyKilled(enemy.runtime.data.characterID);
                        
                        // 👇 就是这行！通知 UIManager 把大地图上的对应明雷怪物实体删掉！
                        if (UIManager.Instance != null) UIManager.Instance.RemoveNPC(enemy.runtime.data);
                        
                        if (enemy.runtime.data.lootTable != null)
                        {
                            var drops = enemy.runtime.data.lootTable.GenerateLoot();
                            foreach (var drop in drops)
                            {
                                InventoryManager.Instance.AddItem(drop.itemData, drop.amount);
                                dropMsg += $"{drop.itemData.itemName}x{drop.amount} ";
                                hasDrops = true;
                            }
                        }
                    }
                }

                GetFirstAlivePlayer().runtime.GainExp(expGain);
                GetFirstAlivePlayer().runtime.Gold += goldGain;
                if (expGain > 0 && UI_SystemToast.Instance != null)
                    UI_SystemToast.Instance.Show("BattleExp", "获得经验:", expGain, null);
                if (goldGain > 0 && UI_SystemToast.Instance != null)
                    UI_SystemToast.Instance.Show("Gold", "获得金币:", goldGain, null);
                LogBattle($"获得经验: {expGain}, 金币: {goldGain}");
                if (hasDrops) LogBattle(dropMsg);
            }
            else
            {
                // 这是逃跑成功的结算
                LogBattle("成功脱离战斗。");
            }
        }
        else if (state == BattleState.Lost)
        {
            LogBattle("战斗失败...");
            if (AudioManager.Instance != null) waitTime = AudioManager.Instance.PlayCombatJingle(false) + 0.5f;
        }

        pendingCGs.Clear();
        foreach (var entity in allEntities)
        {
            if (entity.isDead && entity.runtime != null && entity.runtime.data != null)
            {
                string eventID = entity.runtime.data.GetDefeatCG(entity.runtime.lastKillerID);
                if (!string.IsNullOrEmpty(eventID))
                {
                    pendingCGs.Add(new PendingCG
                    {
                        eventID = eventID,
                        victim = entity
                    });
                }
            }
        }

        StartCoroutine(ProcessCGAndEnd(waitTime));
    }
    public void OnRunButtonClicked() { if (state == BattleState.PlayerMenu) StartCoroutine(AttemptEscape()); }

    private IEnumerator AttemptEscape()
    {
        state = BattleState.PlayerAction;
        if (ui != null)
        {
            if (ui.actionPanel != null) ui.actionPanel.SetActive(false);
            if (ui.playerAvatarImage != null) ui.playerAvatarImage.gameObject.SetActive(false);
        }

        LogBattle($"{currentActor.runtime.Name} 尝试逃跑...");
        yield return new WaitForSeconds(1f);

        BattleEntity fastestEnemy = null;
        int maxEnemySpeed = 0;
        foreach (var e in allEntities) if (!e.isPlayerSide && !e.isDead && e.runtime.Speed > maxEnemySpeed) { maxEnemySpeed = e.runtime.Speed; fastestEnemy = e; }

        float escapeChance = maxEnemySpeed > 0 ? (float)currentActor.runtime.Speed / (float)maxEnemySpeed * 0.5f : 1f;
        escapeChance = Mathf.Clamp(escapeChance, 0.1f, 1.0f);

        if (UnityEngine.Random.value < escapeChance)
        {
            LogBattle("逃跑成功！");
            yield return new WaitForSeconds(1f);
            state = BattleState.Won; 
            EndBattle(); 
        }
        else
        {
            LogBattle("逃跑失败！被敌人拦截了！");
            yield return new WaitForSeconds(1f);
            StartCoroutine(EnemyTurn());
        }
    }

    private IEnumerator ProcessCGAndEnd(float initialWait)
    {
        yield return new WaitForSeconds(initialWait);
        while (pendingCGs.Count > 0)
        {
            var current = pendingCGs[0];
            pendingCGs.RemoveAt(0);

            BattleEntity entity = current.victim;
            string eventID = current.eventID;

            if (entity != null && entity.runtime != null)
            {
                bool armorDestroyed = entity.runtime.SetDurability(EquipmentSlot.Body, 0);

                if (armorDestroyed && UI_SystemToast.Instance != null && entity.runtime.data != null)
                {
                    string cName = !string.IsNullOrEmpty(entity.runtime.data.characterName)
                                   ? entity.runtime.data.characterName
                                   : entity.runtime.Name;
                    UI_SystemToast.Instance.Show("ArmorDestroyed", $"{cName} 的防具已彻底损毁！", 0, null);
                }

                entity.runtime.CurrentHP = 1;
            }

            if (DialogueManager.Instance != null)
            {
                DialogueManager.Instance.StartDialogue(eventID);
                yield return new WaitUntil(() => DialogueManager.Instance == null || !DialogueManager.Instance.IsActive);
            }
        }
        yield return StartCoroutine(CloseBattleDelay(0f));
    }

    private IEnumerator CloseBattleDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (SceneFader.Instance != null)
        {
            // 屏幕开始变黑，在纯黑的瞬间执行委托内的代码
            SceneFader.Instance.FadeAndExecute(() => 
            {
                if (BattleHUDRoot != null) BattleHUDRoot.SetActive(false);
                if (MainHUDRoot != null) MainHUDRoot.SetActive(true);

                // 👇 核心劫持：判断是胜利还是战败
                if (state == BattleState.Lost)
                {
                    ExecuteDefeatPenaltyAndTeleport();
                }
                else
                {
                    // 胜利或逃跑的正常回归逻辑
                    if (GameManager.Instance != null) 
                    {
                        GameManager.Instance.ChangeState(GameState.Exploration);
                        if (GameManager.Instance.currentLocation != null && GameManager.Instance.currentLocation.backgroundMusic != null)
                            AudioManager.Instance.PlayMusic(GameManager.Instance.currentLocation.backgroundMusic);
                    }
                }
            });
        }
        else
        {
            // 兜底逻辑 (没有黑屏过渡时的硬切)
            if (BattleHUDRoot != null) BattleHUDRoot.SetActive(false);
            if (MainHUDRoot != null) MainHUDRoot.SetActive(true);
            
            if (state == BattleState.Lost) ExecuteDefeatPenaltyAndTeleport();
            else if (GameManager.Instance != null) GameManager.Instance.ChangeState(GameState.Exploration);
        }
    }

    // ==========================================
    // 💀 死神之手：战败惩罚与传送枢纽
    // ==========================================
    private void ExecuteDefeatPenaltyAndTeleport()
    {
        RuntimeCharacter player = GameManager.Instance.Player;
        if (player == null) return;

        // 获取当前难度 (读取您刚配置好的 currentDifficulty)
        GameDifficulty diff = GameManager.Instance != null ? GameManager.Instance.currentDifficulty : GameDifficulty.Origin;

        // 1. 深渊模式：真实死亡 (Permadeath)
        if (diff == GameDifficulty.Abyss)
        {
            LogBattle("【深渊模式】全军覆没，存档已粉碎。");
            
            // 精准删除当前记录的那个存档！
            if (SaveManager.Instance != null) 
            {
                SaveManager.Instance.DeleteSave(SaveManager.Instance.currentSaveID);
            }
            
            // 物理踢回主菜单
            UnityEngine.SceneManagement.SceneManager.LoadScene("Scene_Title");
            return;
        }

        // 2. 宽容/标准模式：扣除金币惩罚
        float goldLossPercent = (diff == GameDifficulty.Story) ? 0.2f : 0.5f;
        int lostGold = Mathf.RoundToInt(player.Gold * goldLossPercent);
        player.Gold -= lostGold;

        // 👇 新增：起源模式专属惩罚 —— 黑死咒加深！
        if (diff == GameDifficulty.Origin)
        {
            TraitData curseData = Resources.Load<TraitData>("Traits/Trait_BlackCurse");
            if (curseData != null)
            {
                player.AddTrait(curseData, 1);
                if (UI_SystemToast.Instance != null) 
                    UI_SystemToast.Instance.Show("Curse", "死亡的阴影掠过... 黑死咒加深了！", 0, null);
            }
        }
        
        // （防具耐久度清零与 HP=1 已经在之前的 ProcessCGAndEnd 里执行过了，保持狼狈状态）

        // 播报惩罚
        if (UI_SystemToast.Instance != null)
        {
            UI_SystemToast.Instance.Show("DefeatPenalty", $"你被击倒了... 丢失了 {lostGold} 金币。", 0, null);
        }
        LogBattle($"战败惩罚结算完毕，剩余金币: {player.Gold}");

        // 3. 传送回“家”
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ChangeState(GameState.Exploration);
            GameManager.Instance.TeleportToHome();
        }
    }
    public bool TryUseItem(ItemData item)
    {
        if (state != BattleState.PlayerMenu) { LogBattle("现在不能使用物品！"); return false; }
        state = BattleState.PlayerAction; 
        if (ui != null)
        {
            if (ui.actionPanel != null) ui.actionPanel.SetActive(false);
            if (ui.skillPanel != null) ui.skillPanel.SetActive(false);
        }
        StartCoroutine(ExecuteItemUsage(currentActor, item));
        return true; 
    }

    private IEnumerator ExecuteItemUsage(BattleEntity user, ItemData item)
    {
        LogBattle($"{user.runtime.Name} 使用了 {item.itemName} !");
        
        if (VFXManager.Instance != null && user.uiEntity.bodyImage != null)
            VFXManager.Instance.FlashUnit(user.uiEntity.bodyImage, Color.green);
        yield return new WaitForSeconds(0.5f);

        InventoryManager.Instance.ApplyItemEffect(user.runtime, item);
        
        if (VFXManager.Instance != null && user.uiEntity.bodyImage != null)
             if (item.healAmount > 0)
             {
                 Transform popupPoint = user.uiEntity.vfxSpawnPoint != null ? user.uiEntity.vfxSpawnPoint : user.uiEntity.bodyImage.transform;
                 VFXManager.Instance.ShowDamagePopup(popupPoint.position, item.healAmount, false, true);
             }

        UpdateStatsUI(); 
        yield return new WaitForSeconds(1f);

        user.runtime.turnCount++;
        if (ui != null && ui.playerAvatarImage != null) ui.playerAvatarImage.gameObject.SetActive(false);

        // 👇 吃完药，重置时间，推演时间轴！
        user.runtime.ResetAVAfterTurn();
        AdvanceTimeline();
    }

    private void LogBattle(string msg)
    {
        if (ui != null && ui.battleLogText != null) ui.battleLogText.text = msg;
        Debug.Log($"[Battle] {msg}");
    }


}