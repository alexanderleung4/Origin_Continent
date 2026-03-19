using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// 关卡推进选择面板
/// 通关后弹出，让玩家选择继续下一关或撤退回地图
/// </summary>
public class UI_StageAdvance : MonoBehaviour
{
    [Header("UI 引用")]
    public CanvasGroup panelGroup;
    public TextMeshProUGUI promptText;   // 提示文字，如「是否继续深入？」
    public TextMeshProUGUI stageNameText; // 下一关名称预览
    public Button btnAdvance;            // 继续按钮
    public Button btnRetreat;            // 撤退按钮

    [Header("参数")]
    public float fadeInDuration = 0.3f;

    private StageData nextStage;
    private System.Action onAdvance;
    private System.Action onRetreat;

    private void Start()
    {
        HidePanel();
        if (btnAdvance != null) btnAdvance.onClick.AddListener(OnAdvanceClicked);
        if (btnRetreat != null) btnRetreat.onClick.AddListener(OnRetreatClicked);
    }

    /// <summary>
    /// 显示选择面板
    /// </summary>
    public void ShowPanel(StageData next, string prompt, System.Action advanceCallback, System.Action retreatCallback)
    {
        nextStage = next;
        onAdvance = advanceCallback;
        onRetreat = retreatCallback;

        if (promptText != null) promptText.text = prompt;
        if (stageNameText != null)
            stageNameText.text = next != null ? $"下一关：{next.stageName}" : "";

        StartCoroutine(FadeIn());
    }

    private void HidePanel()
    {
        if (panelGroup != null)
        {
            panelGroup.alpha = 0f;
            panelGroup.blocksRaycasts = false;
            panelGroup.interactable = false;
        }
    }

    private IEnumerator FadeIn()
    {
        if (panelGroup == null) yield break;

        panelGroup.blocksRaycasts = true;
        panelGroup.interactable = true;

        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            panelGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeInDuration);
            yield return null;
        }
        panelGroup.alpha = 1f;
    }

    private void OnAdvanceClicked()
    {
        HidePanel();
        onAdvance?.Invoke();
    }

    private void OnRetreatClicked()
    {
        HidePanel();
        onRetreat?.Invoke();
    }
}