using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class UI_Settings : MonoBehaviour
{
    [Header("Sliders")]
    public Slider masterSlider;
    public Slider musicSlider;
    public Slider sfxSlider;
    
    [Header("Buttons")]
    public Button closeButton;
    public Button quitButton;
    [Header("Config")]
    public GameObject panelRoot; // 面板根节点
    public string titleSceneName = "Scene_Title";

    private void Start()
    {
        // 1. 初始化滑条位置 (读取当前音量)
        if (AudioManager.Instance != null)
        {
            if (masterSlider) masterSlider.value = AudioManager.Instance.GetVolume("MasterVolume");
            if (musicSlider) musicSlider.value = AudioManager.Instance.GetVolume("MusicVolume");
            if (sfxSlider) sfxSlider.value = AudioManager.Instance.GetVolume("SFXVolume");
        }

        // 2. 绑定事件
        if (masterSlider) masterSlider.onValueChanged.AddListener(OnMasterChanged);
        if (musicSlider) musicSlider.onValueChanged.AddListener(OnMusicChanged);
        if (sfxSlider) sfxSlider.onValueChanged.AddListener(OnSFXChanged);
        
        if (closeButton) closeButton.onClick.AddListener(ClosePanel);
        if (quitButton) quitButton.onClick.AddListener(OnQuitClicked);
    }

    public void OpenPanel()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.CloseAllMenus();
            UIManager.Instance.OnAnyMenuOpened();
        }
        panelRoot.SetActive(true);
    }

    public void ClosePanel()
    {
        panelRoot.SetActive(false);
        // 这里未来可以加 SaveSettings()
    }

    // --- 事件回调 ---

    private void OnMasterChanged(float val)
    {
        if (AudioManager.Instance) AudioManager.Instance.SetMasterVolume(val);
    }

    private void OnMusicChanged(float val)
    {
        if (AudioManager.Instance) AudioManager.Instance.SetMusicVolume(val);
    }

    private void OnSFXChanged(float val)
    {
        if (AudioManager.Instance) AudioManager.Instance.SetSFXVolume(val);
    }
    // --- 👇 新增: 退出逻辑 ---
    private void OnQuitClicked()
    {
        // 1. 安全保护: 自动存档 (防止玩家手滑没存档就退了)
        // 使用 -1 (AutoSave) 槽位
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.SaveGame(-1);
            Debug.Log("[Settings] 退出前已自动存档。");
        }

        // 2. 恢复时间流速 (防止如果在设置里暂停了游戏，切场景后还是暂停的)
        Time.timeScale = 1f;

        // 3. 执行转场 (回标题)
        if (SceneFader.Instance != null)
        {
            // 关闭面板，避免遮挡黑屏
            ClosePanel();
            
            // 使用 Fader 淡出并加载场景
            SceneFader.Instance.FadeAndExecute(() => 
            {
                SceneManager.LoadScene(titleSceneName);
                
                // 4. 切回标题的 BGM (可选，如果 Title 场景有 SceneMusic 脚本会自动处理，这里保险起见)
                // 如果标题场景有自己的 Autoplay 逻辑，这里可以不写
            });
        }
        else
        {
            SceneManager.LoadScene(titleSceneName);
        }
    }

}