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
    
    // 🗓️ [预留] 关卡专属掉落或首次通关奖励
    // public LootTable stageClearReward; 
}