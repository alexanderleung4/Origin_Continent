using UnityEngine;
using UnityEngine.EventSystems; // 👈 必须引用，用于监听鼠标事件
using UnityEngine.UI;

// 只要挂了这个脚本，就能自动响应鼠标进入和点击
public class UISound : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler
{
    [Header("Settings")]
    public bool enableClick = true;
    public bool enableHover = true;

    // 为了防止一开始游戏初始化时 UI 自动触发 Hover 声音，加个简单的时间锁
    private static float globalInputTime; 

    private void Start()
    {
        // 获取 Button 组件，如果按钮本身是不可交互的 (Interactable = false)，我们也不应该播声音
        // 这一步是可选的，看您设计需求
    }

    // 鼠标点击时触发
    public void OnPointerClick(PointerEventData eventData)
    {
        if (enableClick && IsInteractable())
        {
            if (AudioManager.Instance != null) 
                AudioManager.Instance.PlayClickSound();
        }
    }

    // 鼠标划过时触发
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (enableHover && IsInteractable())
        {
            if (AudioManager.Instance != null) 
                AudioManager.Instance.PlayHoverSound();
        }
    }

    // 辅助检查：按钮是否处于激活状态？
    private bool IsInteractable()
    {
        Button btn = GetComponent<Button>();
        if (btn != null) return btn.interactable;
        return true; // 如果没有 Button 组件（比如纯图片），默认视为可交互
    }
}