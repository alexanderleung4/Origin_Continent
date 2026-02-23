using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct DialogueLine
{
    public string speakerName; // 谁在说话
    [TextArea] public string content; // 说什么
    public string expression; // 表情 (预留)
    // 👇 新增: 立绘图片
    public Sprite portrait;
    // 👇 新增: 事件指令 (例如 "AddGold:100", "Battle:Slime")
    public string eventCommand;
}

[CreateAssetMenu(fileName = "NewDialogue", menuName = "Origin/Dialogue Data")]
public class DialogueData : ScriptableObject
{
    public string dialogueID;
    public List<DialogueLine> lines; // 对话列表
}