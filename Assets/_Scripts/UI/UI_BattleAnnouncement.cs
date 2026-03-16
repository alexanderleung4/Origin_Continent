using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// 战斗公告演出组件
/// 负责战斗开始的双侧滑入合并、胜利/失败的独立视觉演出
/// 挂载在 BattleHUD 下的专属 Panel 上
/// </summary>
public class UI_BattleAnnouncement : MonoBehaviour
{
    public static UI_BattleAnnouncement Instance { get; private set; }

    [Header("根节点")]
    public GameObject panelRoot; // 整个公告系统的根节点，平时隐藏

    [Header("战斗开始 - 双侧文字")]
    public RectTransform textLeft;   // 左侧文字（如「遭遇」）
    public RectTransform textRight;  // 右侧文字（如「战斗」）
    public CanvasGroup announcementGroup; // 用于整体淡出

    [Header("胜利演出")]
    public GameObject victoryRoot;        // 胜利专属根节点
    public TextMeshProUGUI victoryText;   // 胜利文字
    public Image victoryBG;              // 胜利背景光晕（可选）

    [Header("失败演出")]
    public GameObject defeatRoot;         // 失败专属根节点
    public TextMeshProUGUI defeatText;    // 失败文字
    public Image defeatBG;               // 失败背景（暗红色）

    [Header("参数")]
    public float slideDistance = 600f;   // 文字从多远的地方滑入
    public float slideDuration = 0.4f;   // 滑入时长
    public float holdDuration = 0.8f;    // 合并后停留时长
    public float fadeOutDuration = 0.3f; // 淡出时长

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        HideAll();
    }

    private void HideAll()
    {
        if (announcementGroup != null)
        {
            announcementGroup.alpha = 0f;
            announcementGroup.blocksRaycasts = false;
        }
        if (victoryRoot != null) victoryRoot.SetActive(false);
        if (defeatRoot != null) defeatRoot.SetActive(false);
        
        // 把开场文字隐藏，防止被胜利/失败演出透视到
        if (textLeft != null) textLeft.gameObject.SetActive(false);
        if (textRight != null) textRight.gameObject.SetActive(false);
    }

    // ========================================================================
    // 对外接口
    // ========================================================================

    /// <summary>战斗开始演出，播完后自动隐藏</summary>
    public void PlayBattleStart(string leftWord = "遭　遇", string rightWord = "战　斗")
    {
        StartCoroutine(BattleStartRoutine(leftWord, rightWord));
    }

    /// <summary>胜利演出</summary>
    public void PlayVictory()
    {
        StartCoroutine(VictoryRoutine());
    }

    /// <summary>失败演出</summary>
    public void PlayDefeat()
    {
        StartCoroutine(DefeatRoutine());
    }

    // ========================================================================
    // 协程实现
    // ========================================================================

    private IEnumerator BattleStartRoutine(string leftWord, string rightWord)
    {
        if (panelRoot == null) yield break;

        // 初始化
        if (announcementGroup != null)
        {
            announcementGroup.alpha = 1f;
            if (textLeft != null) textLeft.gameObject.SetActive(true);
            if (textRight != null) textRight.gameObject.SetActive(true);
            announcementGroup.blocksRaycasts = true;
        }
        if (victoryRoot != null) victoryRoot.SetActive(false);
        if (defeatRoot != null) defeatRoot.SetActive(false);

        // 设置文字（如果有TextMeshPro组件的话）
        if (textLeft != null)
        {
            var tmp = textLeft.GetComponent<TextMeshProUGUI>();
            if (tmp != null) tmp.text = leftWord;
        }
        if (textRight != null)
        {
            var tmp = textRight.GetComponent<TextMeshProUGUI>();
            if (tmp != null) tmp.text = rightWord;
        }

        // 重置位置：左侧从左边飞入，右侧从右边飞入
        Vector2 leftOrigin = textLeft != null ? textLeft.anchoredPosition : Vector2.zero;
        Vector2 rightOrigin = textRight != null ? textRight.anchoredPosition : Vector2.zero;

        if (textLeft != null) textLeft.anchoredPosition = new Vector2(leftOrigin.x - slideDistance, leftOrigin.y);
        if (textRight != null) textRight.anchoredPosition = new Vector2(rightOrigin.x + slideDistance, rightOrigin.y);

        // 重置透明度
        if (announcementGroup != null) announcementGroup.alpha = 1f;

        // 滑入动画
        float elapsed = 0f;
        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / slideDuration);
            if (textLeft != null)
                textLeft.anchoredPosition = Vector2.Lerp(
                    new Vector2(leftOrigin.x - slideDistance, leftOrigin.y), leftOrigin, t);
            if (textRight != null)
                textRight.anchoredPosition = Vector2.Lerp(
                    new Vector2(rightOrigin.x + slideDistance, rightOrigin.y), rightOrigin, t);
            yield return null;
        }

        // 复位到原始位置
        if (textLeft != null) textLeft.anchoredPosition = leftOrigin;
        if (textRight != null) textRight.anchoredPosition = rightOrigin;

        // 停留
        yield return new WaitForSeconds(holdDuration);

        // 整体淡出
        elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            if (announcementGroup != null)
                announcementGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
            yield return null;
        }

        HideAll();
    }

    private IEnumerator VictoryRoutine()
    {
        if (panelRoot == null) yield break;

        if (announcementGroup != null)
        {
            announcementGroup.alpha = 1f;
            announcementGroup.blocksRaycasts = true;
        }
        if (defeatRoot != null) defeatRoot.SetActive(false);
        if (victoryRoot != null) victoryRoot.SetActive(true);

        // 胜利：文字从小放大冲出
        if (victoryText != null)
        {
            victoryText.transform.localScale = Vector3.zero;
            float elapsed = 0f;
            float duration = 0.5f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                // 超过1再弹回，做出弹性感
                float scale = t < 0.8f ? Mathf.Lerp(0f, 1.2f, t / 0.8f) : Mathf.Lerp(1.2f, 1f, (t - 0.8f) / 0.2f);
                victoryText.transform.localScale = Vector3.one * scale;
                yield return null;
            }
            victoryText.transform.localScale = Vector3.one;
        }

        // 停留2秒后自动淡出
        yield return new WaitForSeconds(2f);

        if (announcementGroup != null)
        {
            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                announcementGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
                yield return null;
            }
        }

        HideAll();
    }

    private IEnumerator DefeatRoutine()
    {
        if (panelRoot == null) yield break;

        if (announcementGroup != null)
        {
            announcementGroup.alpha = 1f;
            announcementGroup.blocksRaycasts = true;
        }
        if (victoryRoot != null) victoryRoot.SetActive(false);
        if (defeatRoot != null) defeatRoot.SetActive(true);

        // 失败：背景从透明变暗红，文字从上方缓慢落下
        if (defeatBG != null)
        {
            defeatBG.color = new Color(0.4f, 0f, 0f, 0f);
            float elapsed = 0f;
            float duration = 0.8f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                defeatBG.color = new Color(0.4f, 0f, 0f, t * 0.85f);
                yield return null;
            }
        }

        if (defeatText != null)
        {
            RectTransform rt = defeatText.rectTransform;
            Vector2 origin = rt.anchoredPosition;
            rt.anchoredPosition = new Vector2(origin.x, origin.y + 80f);

            float elapsed = 0f;
            float duration = 0.6f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                rt.anchoredPosition = Vector2.Lerp(
                    new Vector2(origin.x, origin.y + 80f), origin, t);
                yield return null;
            }
            rt.anchoredPosition = origin;
        }

        // 失败演出不自动消失，等 BattleManager 的战败管线接管
    }
}