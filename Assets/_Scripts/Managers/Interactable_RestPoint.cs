using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

// 继承 IPointerClickHandler 使其可以直接挂载在场景的 UI 按钮或图片上
[RequireComponent(typeof(Button))]
public class Interactable_RestPoint : MonoBehaviour, IPointerClickHandler
{
    [Header("Rest Settings (休眠设置)")]
    public int minutesToPass = 480; // 默认睡 8 小时 (8 * 60 = 480分钟)
    public string restMessage = "经过了充分的休息，状态已完全恢复。";
    
    // 防连点锁
    private bool isResting = false;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isResting) return;
        
        // 可选：在这里如果想加一个“是否要休息？”的二次确认弹窗，可以以后扩展
        StartCoroutine(PerformRestSequence());
    }

    private IEnumerator PerformRestSequence()
    {
        isResting = true;
        Debug.Log("[RestPoint] 玩家开始休息...");

        if (SceneFader.Instance != null)
        {
            // 利用黑屏掩盖时间流逝的“作弊”过程，增加沉浸感
            SceneFader.Instance.FadeAndExecute(() => 
            {
                ExecuteRestLogic();
            });
            
            // 等待黑屏转场彻底结束再解锁
            yield return new WaitForSeconds(1.5f); 
        }
        else
        {
            // 兜底：如果没有黑屏管理器，直接执行
            ExecuteRestLogic();
            yield return new WaitForSeconds(0.5f);
        }

        isResting = false;
    }

    private void ExecuteRestLogic()
    {
        // 1. 恢复主角肉身状态
        var player = GameManager.Instance.Player;
        if (player != null)
        {
            player.CurrentHP = player.MaxHP;
            player.CurrentMP = player.MaxMP;
            player.CurrentStamina = player.MaxStamina;
            
            // 顺便清除所有回合制战斗残留的 Buff（如果有的话）
            player.activeBuffs.Clear(); 
        }

        // 2. 推进时间引擎
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.AdvanceTime(minutesToPass);
        }

        // 3. UI 刷新与全局播报
        if (UIManager.Instance != null) 
        {
            UIManager.Instance.RefreshPlayerStatus();
        }
        
        if (UI_SystemToast.Instance != null)
        {
            UI_SystemToast.Instance.Show("RestAction", restMessage, 0, null);
        }

        // TODO 未来扩展: 如果您有睡觉的音效 (如打呼噜、篝火噼啪声)，可在此调用 AudioManager
    }
}