using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public enum DialogueVFXType
{
    None,
    FadeIn,       // 屏幕淡入
    FadeOut,      // 屏幕淡出
    ShakeLight,   // 轻震动
    ShakeHeavy,   // 重震动
    FlashWhite,   // 闪白
    PortraitSlideIn,  // 立绘滑入
    PortraitSlideOut, // 立绘滑出
    BGFade        // 背景渐变切换
}
[System.Serializable]
public struct DialogueLine
{
    public string speakerName; // 谁在说话
    [TextArea] public string content; // 说什么
    public string expression; // 表情 (预留)
    public string backgroundID; // 用于存储全屏 CG 的文件名 (Resources/Backgrounds/)
    // 立绘图片
    public Sprite portrait;
    // 事件指令 (例如 "AddGold:100", "Battle:Slime")
    public string eventCommand;
    // 演出特效指令（Asset配置用，CSV用eventCommand代替）
    public DialogueVFXType vfxType;
}

[CreateAssetMenu(fileName = "NewDialogue", menuName = "Origin/Dialogue Data")]
public class DialogueData : ScriptableObject
{
    public string dialogueID;
    public List<DialogueLine> lines; // 对话列表
}