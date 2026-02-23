using UnityEngine;

// Buff 的种类
public enum BuffType 
{ 
    Shield,             // 护盾 (抵挡伤害)
    DamageReduction,    // 免伤 (按百分比降低受到的伤害，例如 0.3 = 减伤 30%)
    StatBoost_Attack,   // 攻击力提升 (预留)
    StatBoost_Defense   // 防御力提升 (预留)
}

[CreateAssetMenu(fileName = "NewBuff", menuName = "Origin/Buff Data")]
public class BuffData : ScriptableObject
{
    public string buffID;
    public string buffName;
    public Sprite icon;         // UI 上显示的小图标
    public BuffType type;

    [Header("Duration (持续时间)")]
    public int durationTurns;   // 持续几个回合？

    [Header("Value Calculation (数值计算)")]
    public int baseValue;           // 基础值 (如护盾 50 点)
    public ScalingStat scalingStat; // 加成属性 (同技能配置)
    public float scalingMultiplier; // 加成倍率 (如 0.1 代表 10%)
}