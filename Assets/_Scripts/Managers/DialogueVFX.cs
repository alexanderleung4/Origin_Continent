using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 对话演出特效工具类
/// 专门处理立绘滑动、背景渐变等协程动画，不污染 DialogueManager 主逻辑
/// </summary>
public static class DialogueVFX
{
    // 立绘滑入：从屏幕外侧滑入到原始位置
    public static IEnumerator SlideIn(RectTransform rect, bool fromLeft, float duration = 0.3f)
    {
        if (rect == null) yield break;

        float startX = fromLeft ? -300f : 300f;
        Vector2 originalPos = rect.anchoredPosition;
        Vector2 startPos = new Vector2(originalPos.x + startX, originalPos.y);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            rect.anchoredPosition = Vector2.Lerp(startPos, originalPos, t);
            yield return null;
        }
        rect.anchoredPosition = originalPos;
    }

    // 立绘滑出：从原始位置滑出到屏幕外侧，结束后隐藏
    public static IEnumerator SlideOut(RectTransform rect, bool toLeft, float duration = 0.2f)
    {
        if (rect == null) yield break;

        float endX = toLeft ? -300f : 300f;
        Vector2 originalPos = rect.anchoredPosition;
        Vector2 endPos = new Vector2(originalPos.x + endX, originalPos.y);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            rect.anchoredPosition = Vector2.Lerp(originalPos, endPos, t);
            yield return null;
        }
        rect.gameObject.SetActive(false);
        rect.anchoredPosition = originalPos; // 复位，防止下次出场位置错误
    }

    // 背景渐变：旧背景淡出，新背景淡入
    public static IEnumerator CrossFadeBackground(Image bgImage, Sprite newSprite, float duration = 0.4f)
    {
        if (bgImage == null || newSprite == null) yield break;

        // 淡出
        float elapsed = 0f;
        Color c = bgImage.color;
        while (elapsed < duration * 0.5f)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(1f, 0f, elapsed / (duration * 0.5f));
            bgImage.color = c;
            yield return null;
        }

        // 换图
        bgImage.sprite = newSprite;
        bgImage.gameObject.SetActive(true);

        // 淡入
        elapsed = 0f;
        while (elapsed < duration * 0.5f)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(0f, 1f, elapsed / (duration * 0.5f));
            bgImage.color = c;
            yield return null;
        }
        c.a = 1f;
        bgImage.color = c;
    }

    // 屏幕闪白
    public static IEnumerator FlashWhite(Image overlay, float duration = 0.3f)
    {
        if (overlay == null) yield break;

        overlay.color = new Color(1f, 1f, 1f, 0.8f);
        overlay.gameObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float a = Mathf.Lerp(0.8f, 0f, elapsed / duration);
            overlay.color = new Color(1f, 1f, 1f, a);
            yield return null;
        }
        overlay.gameObject.SetActive(false);
    }
}