using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public enum DialogueVFXType
{
    None, FadeIn, FadeOut, ShakeLight, ShakeHeavy, FlashWhite, PortraitSlideIn, PortraitSlideOut, BGFade
}

// 🎯 新增：分支选项结构体 (支持拖拽或写CSV)
[System.Serializable]
public class DialogueChoice
{
    public string choiceText;      // 按钮显示的文字
    public string nextID;          // 选后跳转的下文ID (CSV内部寻址 或 新CSV名)
    public DialogueData nextAsset; // 选后跳转的新Asset (Asset寻址)
    public string eventCommand;    // 选后瞬间触发的事件 (如 Affinity:Luna:Trust:5)
}

// 🎯 修复：依据蓝队报告，严格改为 class，避免 List 嵌套序列化丢失
[System.Serializable]
public class DialogueLine
{
    public string lineID;       // 本句的唯一ID (用于被选项跳转寻址)
    public string speakerName;  // 谁在说话
    [TextArea] public string content; // 说什么
    public string expression;   // 表情
    public string backgroundID; // 背景ID
    public Sprite portrait;     // 立绘
    public string eventCommand; // 触发事件
    public DialogueVFXType vfxType; // 特效

    // 🎯 新增：该句挂载的选项列表
    public List<DialogueChoice> choices = new List<DialogueChoice>(); 
}

[CreateAssetMenu(fileName = "NewDialogue", menuName = "Origin/Dialogue Data")]
public class DialogueData : ScriptableObject
{
    public string dialogueID;
    public List<DialogueLine> lines; 
}