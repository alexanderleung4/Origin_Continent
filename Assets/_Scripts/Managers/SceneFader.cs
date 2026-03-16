using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneFader : MonoBehaviour
{
    public static SceneFader Instance { get; private set; }

    [Header("UI References")]
    public CanvasGroup fadeGroup; // 用来控制透明度
    public Image fadeImage;       // 用来挡住屏幕 (黑色)

    [Header("Settings")]
    public float fadeDuration = 0.5f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // 游戏刚启动时，如果是黑的，要淡出
        if (fadeGroup != null) StartCoroutine(FadeIn());
    }

    // --- 公开方法: 切换场景 ---
    public void FadeToScene(string sceneName)
    {
        StartCoroutine(FadeOutAndLoad(sceneName));
    }

    private IEnumerator FadeOutAndLoad(string sceneName)
    {
        // 0. 👇 新增: 如果有 AudioManager，先让它开始淡出音乐
        // 这里的 fadeDuration 是画面变黑的时间，让音乐也用这个时间淡出，达成同步
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.FadeOutMusic(fadeDuration);
        }

        // 1. 屏幕变黑 (Fade Out)
        fadeGroup.blocksRaycasts = true; 
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            fadeGroup.alpha = t / fadeDuration;
            yield return null;
        }
        fadeGroup.alpha = 1f;

        // 2. 加载场景
        yield return SceneManager.LoadSceneAsync(sceneName);

        // 3. 屏幕变亮 (Fade In)
        yield return StartCoroutine(FadeIn());
    }

    public IEnumerator FadeIn()
    {
        float t = fadeDuration;
        while (t > 0f)
        {
            t -= Time.deltaTime;
            fadeGroup.alpha = t / fadeDuration;
            yield return null;
        }
        fadeGroup.alpha = 0f;
        fadeGroup.blocksRaycasts = false; // 恢复点击
    }
    // --- 👇 新增: 逻辑转场 (不切 Unity Scene，只做黑屏操作) ---
    // action: 黑屏中间要执行的代码 (比如换图、换BGM)
    public void FadeAndExecute(System.Action action)
    {
        StartCoroutine(FadeAndExecuteRoutine(action));
    }

    private IEnumerator FadeAndExecuteRoutine(System.Action action)
    {
        // 0. 阻挡点击 & 音乐淡出
        fadeGroup.blocksRaycasts = true;
        if (AudioManager.Instance != null) AudioManager.Instance.FadeOutMusic(fadeDuration);

        // 1. 变黑 (Fade Out)
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            fadeGroup.alpha = t / fadeDuration;
            yield return null;
        }
        fadeGroup.alpha = 1f;

        // 2. ✨ 执行核心逻辑 (换图、换UI、换BGM) ✨
        try
        {
            action?.Invoke(); 
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SceneFader] 转场逻辑报错 (已拦截，防止死机): {e}");
        }
        
        yield return new WaitForSeconds(0.2f); // 缓冲

        // 3. 变亮 (Fade In)
        t = fadeDuration;
        while (t > 0f)
        {
            t -= Time.deltaTime;
            fadeGroup.alpha = t / fadeDuration;
            yield return null;
        }
        fadeGroup.alpha = 0f;
        fadeGroup.blocksRaycasts = false;
    }
}