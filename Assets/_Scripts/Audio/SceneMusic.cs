using UnityEngine;

public class SceneMusic : MonoBehaviour
{
    [Header("Music Settings")]
    public AudioClip backgroundMusic; // 拖入你想在这个场景放的音乐
    public float fadeTime = 1.5f;     // 淡入淡出时间 (默认 1.5秒)

    private void Start()
    {
        // 保护机制：如果 AudioManager 还没初始化 (比如直接运行该场景且没放 Manager)，就不报错
        if (AudioManager.Instance != null && backgroundMusic != null)
        {
            AudioManager.Instance.PlayMusic(backgroundMusic, fadeTime);
        }
    }
}