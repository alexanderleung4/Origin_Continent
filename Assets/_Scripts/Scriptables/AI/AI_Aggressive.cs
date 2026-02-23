using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "AI_Aggressive", menuName = "Origin/AI/Aggressive (野兽)")]
public class AI_Aggressive : AIProfileBase
{
    public override SkillData GetAction(RuntimeCharacter me, RuntimeCharacter target)
    {
        // 1. 获取我所有的技能
        List<SkillData> mySkills = me.data.startingSkills;
        if (mySkills == null || mySkills.Count == 0) return null;

        // 2. 筛选出所有 "攻击类" 且 "蓝量/精力足够" 的技能
        List<SkillData> validSkills = new List<SkillData>();

        foreach (var skill in mySkills)
        {
            // 只看攻击技能
            if (skill.category == SkillCategory.Attack)
            {
                // 检查消耗
                bool hasEnoughResource = false;
                if (skill.damageType == DamageType.Physical)
                {
                    hasEnoughResource = me.CurrentStamina >= skill.staminaCost;
                }
                else if (skill.damageType == DamageType.Magical)
                {
                    hasEnoughResource = me.CurrentMP >= skill.mpCost;
                }

                if (hasEnoughResource)
                {
                    validSkills.Add(skill);
                }
            }
        }

        // 3. 决策
        if (validSkills.Count > 0)
        {
            // 野兽逻辑：随机选一个打出去
            int randomIndex = Random.Range(0, validSkills.Count);
            return validSkills[randomIndex];
        }

        // 如果没有能用的技能（蓝空了），返回 null (BattleManager 应该处理为平A或休息)
        return null; 
    }
}