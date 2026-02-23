using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class UI_CutIn : MonoBehaviour
{
    public static UI_CutIn Instance { get; private set; }

    [Header("UI References")]
    public GameObject panelRoot;
    public Image cutInImage;
    public RectTransform cutInRect;

    [Header("Common Settings")]
    public float holdDuration = 1.0f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    /// <summary>
    /// 播放切入动画 (根据 SkillData 里的类型分流)
    /// </summary>
    public IEnumerator PlayCutIn(SkillData skill, bool isPlayerAction)
    {
        if (skill == null || skill.cutInImage == null) yield break;

        // 1. 初始化
        panelRoot.SetActive(true);
        cutInImage.sprite = skill.cutInImage;
        cutInImage.color = Color.white; // 重置颜色
        cutInRect.localScale = Vector3.one; // 重置缩放
        cutInRect.anchoredPosition = Vector2.zero; // 重置位置

        // 2. 根据类型选择剧本
        switch (skill.cutInType)
        {
            case CutInAnimType.Hard_Impact:
                yield return StartCoroutine(Anim_HardImpact(isPlayerAction));
                break;

            case CutInAnimType.Slow_Zoom:
                yield return StartCoroutine(Anim_SlowZoom());
                break;

            case CutInAnimType.Soft_Fade:
                yield return StartCoroutine(Anim_SoftFade());
                break;
        }

        // 3. 结束
        panelRoot.SetActive(false);
    }

    // --- 剧本 A: 硬冲击 (原有的 Nuke 风格) ---
    private IEnumerator Anim_HardImpact(bool isPlayerAction)
    {
        float slideDuration = 0.2f;
        float startX = isPlayerAction ? -1200f : 1200f; 
        
        // 滑入
        float timer = 0f;
        while (timer < slideDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, timer / slideDuration);
            cutInRect.anchoredPosition = new Vector2(Mathf.Lerp(startX, 0, t), 0);
            yield return null;
        }
        cutInRect.anchoredPosition = Vector2.zero;

        // 震动
        timer = 0f;
        while (timer < holdDuration)
        {
            timer += Time.deltaTime;
            cutInRect.anchoredPosition = new Vector2(Random.Range(-20f, 20f), Random.Range(-20f, 20f));
            yield return null;
        }
    }

    // --- 剧本 B: 慢缩放 (适合蓄力 Charge) ---
    private IEnumerator Anim_SlowZoom()
    {
        // 效果：从 1.0 慢慢变大到 1.2，配合轻微抖动，暗示力量积蓄
        float timer = 0f;
        Vector3 startScale = Vector3.one;
        Vector3 targetScale = Vector3.one * 1.2f;

        while (timer < holdDuration)
        {
            timer += Time.deltaTime;
            float t = timer / holdDuration;
            
            // 变大
            cutInRect.localScale = Vector3.Lerp(startScale, targetScale, t);
            
            // 轻微高频抖动 (像是在憋气)
            float shake = Mathf.Sin(timer * 50f) * 5f; 
            cutInRect.anchoredPosition = new Vector2(shake, 0);

            yield return null;
        }
    }

    // --- 剧本 C: 柔缓动 (适合休息 Rest) ---
    private IEnumerator Anim_SoftFade()
    {
        // 效果：透明度从 0 淡入，稍微向上漂浮，代表升华/放松
        float fadeDuration = 0.3f;
        
        // 淡入
        float timer = 0f;
        cutInImage.color = new Color(1, 1, 1, 0); // 先变透明
        
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float alpha = timer / fadeDuration;
            cutInImage.color = new Color(1, 1, 1, alpha);
            yield return null;
        }
        cutInImage.color = Color.white;

        // 停留并缓慢上浮
        timer = 0f;
        while (timer < holdDuration - fadeDuration)
        {
            timer += Time.deltaTime;
            // 慢慢往上飘一点点
            cutInRect.anchoredPosition = new Vector2(0, timer * 20f);
            yield return null;
        }
    }
}