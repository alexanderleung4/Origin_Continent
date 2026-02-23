using UnityEngine;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public static class CSVLoader
{
    // 定义 CSV 的分隔规则 (处理逗号, 兼容引号内的逗号)
    // 简单的 split(',') 在遇到 "Hello, world" 时会出错，所以用正则
    static string SPLIT_RE = @",(?=(?:[^""]*""[^""]*"")*(?![^""]*""))";
    static string LINE_SPLIT_RE = @"\r\n|\n\r|\n|\r";

    public static List<DialogueLine> LoadCSV(string csvFileName)
    {
        List<DialogueLine> lines = new List<DialogueLine>();

        // 1. 从 Resources 文件夹加载文本
        // 路径不用加后缀 .csv
        TextAsset data = Resources.Load<TextAsset>($"Dialogues/{csvFileName}");
        
        if (data == null)
        {
            Debug.LogError($"[CSVLoader] 找不到文件: Resources/Dialogue/{csvFileName}");
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

            // 5. 解析数据
            // Excel 列顺序: 0:ID, 1:Speaker, 2:Content, 3:Expression, 4:NextID, 5:Event
            
            DialogueLine newLine = new DialogueLine();
            
            newLine.speakerName = CleanQuotes(values[1]);
            newLine.content = CleanQuotes(values[2]);
            
            // 👇 处理表情：读取 CSV，并把它稳稳地存进我们刚刚声明的变量里！
            string expr = (values.Length > 3) ? CleanQuotes(values[3]) : "Normal";
            newLine.expression = expr; 
            
            // 读取事件指令 
            if (values.Length > 5)
            {
                string rawEvent = CleanQuotes(values[5]);
                if (!string.IsNullOrEmpty(rawEvent))
                {
                    newLine.eventCommand = rawEvent;
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