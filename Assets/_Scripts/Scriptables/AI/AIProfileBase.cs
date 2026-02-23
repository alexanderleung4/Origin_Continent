using UnityEngine;

// 这是所有 AI 的基类 (Abstract Base Class)
// 它不负责具体逻辑，只规定所有大脑都必须有个 "思考(GetAction)" 的能力
public abstract class AIProfileBase : ScriptableObject
{
    /// <summary>
    /// AI 的核心思考函数
    /// </summary>
    /// <param name="me">我自己 (获取我的蓝量/技能)</param>
    /// <param name="target">敌人 (获取他的血量/状态)</param>
    /// <returns>决定使用的技能 (如果返回 null，代表发呆或普攻)</returns>
    public abstract SkillData GetAction(RuntimeCharacter me, RuntimeCharacter target);
}