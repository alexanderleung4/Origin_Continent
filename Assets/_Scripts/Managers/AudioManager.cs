using UnityEngine;
using UnityEngine.Audio;
using System.Collections;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("🔧 Configuration")]
    public AudioMixer mainMixer; 
    
    [Header("🎧 Audio Sources")]
    public AudioSource musicSource; 
    public AudioSource sfxSource; 

    // 👇 新增: 用来记录当前正在跑的音乐协程，防止冲突
    private Coroutine currentMusicCoroutine;
    
    // 👇 新增: 默认音乐最大音量 (以后可以跟设置系统挂钩)
    private float maxMusicVolume = 1.0f;

    [Header("🎧 Global UI Sounds")]
    public AudioClip uiClickClip;   // 通用点击音效
    public AudioClip uiHoverClip;   // 通用悬停音效 (可选)
    public AudioClip uiErrorClip;   // 错误/禁止操作音效

    [Header("⚔️ Combat Sounds")]
    public AudioClip genericHitClip;  // 通用受击声 (比如拳头打到肉)
    public AudioClip victoryClip;     // 胜利简短 BGM
    public AudioClip defeatClip;      // 失败简短 BGM

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ========================================================================
    // 🎵 BGM System (修复版)
    // ========================================================================
    
    public void PlayMusic(AudioClip clip, float fadeDuration = 1.0f)
    {
        // 1. 如果要播的和正在播的一样，且正在播放中，就不折腾了
        if (musicSource.clip == clip && musicSource.isPlaying) return;

        // 2. 🛑 掐断之前所有的淡入/淡出操作 (关键!)
        if (currentMusicCoroutine != null) StopCoroutine(currentMusicCoroutine);

        // 3. 启动新的切换
        currentMusicCoroutine = StartCoroutine(FadeMusicRoutine(clip, fadeDuration));
    }

    // 专门用于转场时的淡出
    public void FadeOutMusic(float duration)
    {
        if (currentMusicCoroutine != null) StopCoroutine(currentMusicCoroutine);
        currentMusicCoroutine = StartCoroutine(FadeOutRoutine(duration));
    }

    private IEnumerator FadeMusicRoutine(AudioClip newClip, float duration)
    {
        // A. 淡出旧音乐 (如果当前有声音的话)
        float startVolume = musicSource.volume;
        // 如果当前已经是静音或还没播，这一步会瞬间完成
        while (musicSource.volume > 0)
        {
            musicSource.volume -= startVolume * Time.deltaTime / duration;
            yield return null;
        }
        
        musicSource.Stop(); // 彻底切歌
        musicSource.volume = 0;

        // B. 换碟
        musicSource.clip = newClip;
        musicSource.Play();

        // C. 淡入新音乐 (修复: 目标是 maxMusicVolume，而不是 startVolume)
        while (musicSource.volume < maxMusicVolume)
        {
            musicSource.volume += maxMusicVolume * Time.deltaTime / duration;
            yield return null;
        }
        musicSource.volume = maxMusicVolume;
        
        // 任务结束，清空标记
        currentMusicCoroutine = null;
    }

    private IEnumerator FadeOutRoutine(float duration)
    {
        float startVolume = musicSource.volume;
        while (musicSource.volume > 0)
        {
            musicSource.volume -= startVolume * Time.deltaTime / duration;
            yield return null;
        }
        musicSource.volume = 0;
        musicSource.Stop();
        musicSource.clip = null; // 清空引用，避免下次误判
        
        currentMusicCoroutine = null;
    }

    // ========================================================================
    // 🔊 SFX System
    // ========================================================================

    public void PlaySFX(AudioClip clip, float volumeScale = 1.0f)
    {
        if (clip == null) return;
        sfxSource.PlayOneShot(clip, volumeScale);
    }

    // --- 👇 便捷接口：播放通用 UI 音效 ---
    public void PlayClickSound()
    {
        PlaySFX(uiClickClip);
    }

    public void PlayHoverSound()
    {
        // 加上非空检查，因为悬停音效很吵，有时候可能不配
        if (uiHoverClip != null) PlaySFX(uiHoverClip, 0.5f); // 悬停声音通常小一点
    }

    // --- 👇 战斗常用接口 ---
    public void PlayHitSound()
    {
        // 稍微随机化一点音调，让连续挨打听起来不机械
        sfxSource.pitch = Random.Range(0.9f, 1.1f);
        PlaySFX(genericHitClip);
        sfxSource.pitch = 1.0f; // 复原
    }

    public float PlayCombatJingle(bool isVictory) 
    {
        AudioClip clip = isVictory ? victoryClip : defeatClip;
        if (clip != null)
        {
            // 既然是结算，我们希望它独占声道，所以先停掉其他的 SFX (可选)
            // sfxSource.Stop(); 
            
            sfxSource.PlayOneShot(clip);
            return clip.length; // ✅ 现在合法了
        }
        return 0f; // ✅ 没素材时返回 0 秒
    }
    // 👇 新增: 立即停止背景音乐 (为胜利曲腾出空间)
    public void StopMusic()
    {
        if (currentMusicCoroutine != null) StopCoroutine(currentMusicCoroutine);
        musicSource.Stop();
        musicSource.volume = maxMusicVolume; // 重置音量，以免下次播放没声
    }
    
    // 🎛️ Volume Control (设置系统)
    // ========================================================================

    public void SetMasterVolume(float value)
    {
        // 核心公式: Mathf.Log10(value) * 20
        // value 范围 0.0001 - 1
        float db = (value <= 0.001f) ? -80f : Mathf.Log10(value) * 20f;
        mainMixer.SetFloat("MasterVolume", db);
    }

    public void SetMusicVolume(float value)
    {
        float db = (value <= 0.001f) ? -80f : Mathf.Log10(value) * 20f;
        mainMixer.SetFloat("MusicVolume", db);
    }

    public void SetSFXVolume(float value)
    {
        float db = (value <= 0.001f) ? -80f : Mathf.Log10(value) * 20f;
        mainMixer.SetFloat("SFXVolume", db);
    }

    // 用于 UI 初始化时读取当前 Mixer 的值 (反向转换: dB -> 0-1)
    public float GetVolume(string paramName)
    {
        if (mainMixer.GetFloat(paramName, out float db))
        {
            return (db <= -80f) ? 0f : Mathf.Pow(10f, db / 20f);
        }
        return 1f;
    }
}