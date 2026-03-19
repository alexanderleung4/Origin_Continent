using UnityEngine;
using System.Collections.Generic;

// 定义单个敌人的生成信息
[System.Serializable]
public class EnemySpawnInfo
{
    public CharacterData enemyData;
    [Tooltip("站位编号: 0,1,2 为前排(上中下) | 3,4,5 为后排(上中下)")]
    [Range(0, 5)] public int slotIndex; 
}

[CreateAssetMenu(fileName = "NewStage", menuName = "Origin/Stage Data")]
public class StageData : ScriptableObject
{
    [Header("📝 关卡基础信息")]
    public string stageID;
    public string stageName;
    public Sprite backgroundImage; // 关卡背景图

    [Header("⚔️ 敌人阵列配置")]
    public List<EnemySpawnInfo> enemies;
    
    [Header("🔗 关卡链配置")]
    [Tooltip("通关后自动衔接的下一关，留空代表此关卡是终点")]
    public StageData nextStage;

    [Tooltip("勾选后通关直接进入下一关（适合Boss战连续演出），不勾选则弹出选择面板")]
    public bool autoAdvance = false;

    [Tooltip("选择面板上显示的提示文字，如「是否继续深入？」")]
    public string advancePrompt = "是否继续前进？";

    [Header("🏆 通关奖励")]
    public int clearExp = 0;
    public int clearGold = 0;
    public LootTable stageLoot;
}