using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class VFXManager : MonoBehaviour
{
    public static VFXManager Instance { get; private set; }

    [Header("Prefabs")]
    public GameObject damagePopupPrefab; // 伤害飘字预制体 [cite: 127]
    public Transform popupCanvas;        // 飘字的父容器 (BattleHUD)

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // --- 核心功能 1: 受击震动 (Shake) ---
    //  Unit Shake: 被打的角色头像快速左右抖动
    public void ShakeUnit(GameObject targetObj, float duration = 0.2f, float strength = 10f)
    {
        if (targetObj != null)
            StartCoroutine(DoShake(targetObj.transform, duration, strength));
    }

    private IEnumerator DoShake(Transform target, float duration, float strength)
    {
        Vector3 originalPos = target.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * strength;
            float y = Random.Range(-1f, 1f) * strength;

            target.localPosition = originalPos + new Vector3(x, y, 0);

            elapsed += Time.deltaTime;
            yield return null;
        }

        target.localPosition = originalPos; // 归位
    }

    // --- 核心功能 2: 受击闪光 (Flash) ---
    //  受击闪白: 让受击者瞬间变色
    public void FlashUnit(Image targetImg, Color flashColor, float duration = 0.15f)
    {
        if (targetImg != null)
            StartCoroutine(DoFlash(targetImg, flashColor, duration));
    }

    private IEnumerator DoFlash(Image img, Color flashColor, float duration)
    {
        Color originalColor = img.color;
        img.color = flashColor; // 瞬间变色 (通常是红色或白色)
        yield return new WaitForSeconds(duration);
        img.color = originalColor; // 恢复
    }

    // --- 核心功能 3: 伤害/治疗飘字 (升级版) ---
    public void ShowDamagePopup(Vector3 position, int value, bool isCritical = false, bool isHeal = false)
    {
        if (damagePopupPrefab == null || popupCanvas == null) return;

        // 生成飘字
        GameObject popup = Instantiate(damagePopupPrefab, popupCanvas);
        popup.transform.position = position;

        // 设置文字
        TextMeshProUGUI textComp = popup.GetComponentInChildren<TextMeshProUGUI>();
        if (textComp != null)
        {
            if (isHeal)
            {
                // 🟢 治疗模式
                textComp.text = "+" + value;
                // 使用荧光绿，比默认 Green 更亮
                textComp.color = new Color(0.2f, 1f, 0.2f); 
                textComp.fontSize *= 1.2f; // 稍微大一点
            }
            else
            {
                // 🔴 伤害模式
                textComp.text = "-" + value;
                if (isCritical)
                {
                    textComp.fontSize *= 1.5f; // 暴击变大
                    textComp.color = new Color(1f, 0.8f, 0f); // 金黄色
                }
                else
                {
                    textComp.color = new Color(1f, 0.3f, 0.3f); // 亮红色
                }
            }
        }
        
        StartCoroutine(AnimatePopup(popup.transform));
    }

    private IEnumerator AnimatePopup(Transform popup)
    {
        float timer = 0;
        Vector3 startPos = popup.position;
        
        while (timer < 1.0f) // 飘 1 秒
        {
            // 向上移动
            popup.position = startPos + new Vector3(0, timer * 50f, 0); 
            timer += Time.deltaTime;
            yield return null;
        }
        Destroy(popup.gameObject); // 销毁
    }
}