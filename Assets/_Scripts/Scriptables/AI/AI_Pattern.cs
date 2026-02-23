using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "AI_Pattern", menuName = "Origin/AI/Pattern (序列/Boss)")]
public class AI_Pattern : AIProfileBase
{
    [Header("The Script (剧本)")]
    [Tooltip("Boss 将严格按照这个顺序循环释放技能")]
    public List<SkillData> patternList;

    public override SkillData GetAction(RuntimeCharacter me, RuntimeCharacter target)
    {
        if (patternList == null || patternList.Count == 0) return null;

        // 核心逻辑：取模运算
        // 第 0 回合 -> 索引 0
        // 第 1 回合 -> 索引 1
        // 第 3 回合 (如果列表长度3) -> 索引 0 (循环)
        int index = me.turnCount % patternList.Count;

        SkillData plannedSkill = patternList[index];

        // 检查蓝量 (Boss 也要讲基本法，没蓝会卡壳，或者你可以设计成 Boss 技能无消耗)
        if (CheckCost(me, plannedSkill))
        {
            return plannedSkill;
        }
        else
        {
            Debug.LogWarning($"[AI Pattern] 轮到放 {plannedSkill.skillName} 但蓝不够！跳过回合。");
            return null; // 或者返回一个默认平A
        }
    }

    private bool CheckCost(RuntimeCharacter me, SkillData skill)
    {
        if (skill.damageType == DamageType.Magical) return me.CurrentMP >= skill.mpCost;
        else return me.CurrentStamina >= skill.staminaCost;
    }
}