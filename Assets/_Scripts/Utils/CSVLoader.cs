using UnityEngine;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public static class CSVLoader
{
    // 定义 CSV 的分隔规则 (处理逗号, 兼容引号内的逗号)
    static string SPLIT_RE = @",(?=(?:[^""]*""[^""]*"")*(?![^""]*""))";
    static string LINE_SPLIT_RE = @"\r\n|\n\r|\n|\r";

    public static List<DialogueLine> LoadCSV(string csvFileName)
    {
        List<DialogueLine> lines = new List<DialogueLine>();

        // 1. 从 Resources 文件夹加载文本
        TextAsset data = Resources.Load<TextAsset>($"Dialogues/{csvFileName}");
        
        if (data == null)
        {
            Debug.LogError($"[CSVLoader] 找不到文件: Resources/Dialogues/{csvFileName}");
            return null;
        }

        // 2. 拆分行
        var rawLines = Regex.Split(data.text, LINE_SPLIT_RE);

        // 3. 遍历每一行 (从第 1 行开始，跳过第 0 行表头)
        for (int i = 1; i < rawLines.Length; i++)
        {
            string currentLine = rawLines[i];
            if (string.IsNullOrWhiteSpace(currentLine)) continue; // 跳过空行

            // 4. 拆分列
            var values = Regex.Split(currentLine, SPLIT_RE);
            if (values.Length < 3) continue; // 数据不完整，跳过

            string speaker = CleanQuotes(values[1]);

            // 🛡️ 蓝队防线 1：如果这一行是被下面“前瞻”过的 Choice 行，直接无视跳过
            if (speaker == "Choice") continue;

            // 5. 解析普通台词数据
            DialogueLine newLine = new DialogueLine();
            
            // 🎯 核心修复：必须记录 lineID，这是选项跳转时“同一表内寻址”的坐标！
            newLine.lineID = CleanQuotes(values[0]); 
            newLine.speakerName = speaker;
            newLine.content = CleanQuotes(values[2]).Replace("\\n", "\n"); // 顺手支持手动换行
            newLine.expression = (values.Length > 3) ? CleanQuotes(values[3]) : "Normal";
            
            if (values.Length > 5)
            {
                string rawEvent = CleanQuotes(values[5]);
                if (!string.IsNullOrEmpty(rawEvent)) newLine.eventCommand = rawEvent;
            }

            // ==========================================
            // 🎯 蓝队核心科技：前瞻式嗅探 (Lookahead Parsing)
            // ==========================================
            newLine.choices = new List<DialogueChoice>(); // 确保初始化，防止空指针

            for (int j = i + 1; j < rawLines.Length; j++)
            {
                string nextLine = rawLines[j];
                if (string.IsNullOrWhiteSpace(nextLine)) continue;

                var nextValues = Regex.Split(nextLine, SPLIT_RE);
                if (nextValues.Length < 3) continue;

                string nextSpeaker = CleanQuotes(nextValues[1]);
                
                if (nextSpeaker == "Choice")
                {
                    // 发现连续的 Choice 行！组装成选项塞入当前普通句子的 choices 列表里
                    DialogueChoice choice = new DialogueChoice();
                    choice.choiceText = CleanQuotes(nextValues[2]); // Content列：按钮上的文字
                    choice.nextID = (nextValues.Length > 4) ? CleanQuotes(nextValues[4]) : ""; // NextID列：点击后跳去哪
                    choice.eventCommand = (nextValues.Length > 5) ? CleanQuotes(nextValues[5]) : ""; // Event列：触发什么事（加好感）
                    // 读取第7列作为条件锁指令（列数不足则自动补空字符串，蓝队容错机制生效）
                    choice.conditionCommand = (nextValues.Length > 6) ? CleanQuotes(nextValues[6]) : "";

                    newLine.choices.Add(choice);
                }
                else
                {
                    // 嗅探到下一个普通台词了，选项结束，打断前瞻循环
                    break;
                }
            }

            lines.Add(newLine);
        }

        return lines;
    }

    private static string CleanQuotes(string s)
    {
        return s.Trim('\"').Trim();
    }
}