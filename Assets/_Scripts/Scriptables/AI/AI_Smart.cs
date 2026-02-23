using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "AI_Smart", menuName = "Origin/AI/Smart (战术)")]
public class AI_Smart : AIProfileBase
{
    [Header("Settings")]
    public float healThreshold = 0.3f; // 血量低于 30% 尝试治疗

    public override SkillData GetAction(RuntimeCharacter me, RuntimeCharacter target)
    {
        List<SkillData> mySkills = me.data.startingSkills;
        if (mySkills == null || mySkills.Count == 0) return null;

        // --- 1. 战术判断：是否需要治疗？ ---
        float hpPercent = (float)me.CurrentHP / me.data.maxHP;
        
        if (hpPercent < healThreshold)
        {
            // 尝试找一个治疗技能
            SkillData healSkill = mySkills.Find(s => s.category == SkillCategory.Heal);
            
            // 如果有，且蓝/精力够用
            if (healSkill != null && CheckCost(me, healSkill))
            {
                Debug.Log($"[AI] 血量告急 ({hpPercent:P0})，决定使用治疗！");
                return healSkill;
            }
        }

        // --- 2. 否则：攻击模式 ---
        // 筛选所有可用的攻击技能
        List<SkillData> attackSkills = new List<SkillData>();
        foreach (var skill in mySkills)
        {
            if (skill.category == SkillCategory.Attack && CheckCost(me, skill))
            {
                attackSkills.Add(skill);
            }
        }

        if (attackSkills.Count > 0)
        {
            // 简单随机选一个打
            return attackSkills[Random.Range(0, attackSkills.Count)];
        }

        return null; // 发呆/没蓝
    }

    // 辅助方法：检查消耗是否足够
    private bool CheckCost(RuntimeCharacter me, SkillData skill)
    {
        if (skill.damageType == DamageType.Magical)
            return me.CurrentMP >= skill.mpCost;
        else
            return me.CurrentStamina >= skill.staminaCost;
    }
}