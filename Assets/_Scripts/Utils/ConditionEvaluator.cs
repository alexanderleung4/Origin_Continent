using UnityEngine;

// 高扩展性的返回结构体
public struct ConditionResult
{
    public bool isMet;
    public string lockHint; 
}

public static class ConditionEvaluator
{
    public static ConditionResult Evaluate(string conditionCommand)
    {
        // 1. 如果策划没配条件，默认直接放行
        if (string.IsNullOrWhiteSpace(conditionCommand))
        {
            return new ConditionResult { isMet = true, lockHint = "" };
        }

        // 2. 切割指令
        string[] parts = conditionCommand.Split(':');
        if (parts.Length < 1) return new ConditionResult { isMet = true, lockHint = "" };

        string type = parts[0].Trim();

        // 3. 路由字典
        switch (type)
        {
            // 语法格式：Require:[角色ID]:[好感维度]:[数值] 
            // 例子：Require:Luna:Intimacy:50
            case "Require":
                if (parts.Length >= 4)
                {
                    string charID = parts[1].Trim();
                    if (System.Enum.TryParse(parts[2].Trim(), out AffinityType affType) &&
                        int.TryParse(parts[3].Trim(), out int reqAmount))
                    {
                        // 查户口
                        int currentAmount = AffinityManager.Instance != null ? AffinityManager.Instance.GetAffinity(charID, affType) : 0;
                        bool met = currentAmount >= reqAmount;
                        
                        return new ConditionResult
                        {
                            isMet = met,
                            // 如果没达标，生成带颜色的提示文案
                            lockHint = met ? "" : $"<color=#888888> <size=80%>[锁定] ({affType} 需 {reqAmount})</size></color>"
                        };
                    }
                }
                break;
                
            // 💡 未来扩展预留地：
            // case "RequireItem": ... 
            // case "RequireGold": ...
        }

        // 4. 解析失败兜底（防填表手滑）
        Debug.LogWarning($"[ConditionEvaluator] 无法解析的条件指令，已放行: {conditionCommand}");
        return new ConditionResult { isMet = true, lockHint = "" };
    }
}