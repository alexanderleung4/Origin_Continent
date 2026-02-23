using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class UI_TitleScreen : MonoBehaviour
{
    [Header("Game Config")]
    public string gameSceneName = "MainMenu"; 

    [Header("UI Buttons")]
    public Button btnNewGame;
    public Button btnContinue;
    public Button btnLoadGame; // 👈 加回来
    public Button btnExit;
    
    [Header("Sub Menus")]
    public UI_SaveMenu saveMenu; // 👈 引用存档菜单

    private void Start()
    {
        if (btnNewGame) btnNewGame.onClick.AddListener(OnNewGameClicked);
        if (btnContinue) btnContinue.onClick.AddListener(OnContinueClicked);
        if (btnLoadGame) btnLoadGame.onClick.AddListener(OnLoadGameClicked); // 👈 绑定
        if (btnExit) btnExit.onClick.AddListener(OnExitClicked);

        CheckContinueStatus();
    }

    private void CheckContinueStatus()
    {
        if (btnContinue == null) return;
        int latestID = SaveManager.GetLatestSaveID(); // 静态调用
        btnContinue.interactable = (latestID != -999);
    }

    // --- 按钮逻辑 ---

    private void OnNewGameClicked()
    {
        SaveManager.AutoLoadSlot = -2;
        if (SceneFader.Instance != null) 
            SceneFader.Instance.FadeToScene(gameSceneName);
        else 
            SceneManager.LoadScene(gameSceneName);
    }

    private void OnContinueClicked()
    {
        int latestID = SaveManager.GetLatestSaveID();
        if (latestID != -999)
        {
            SaveManager.AutoLoadSlot = latestID;
            if (SceneFader.Instance != null) 
                SceneFader.Instance.FadeToScene(gameSceneName);
            else 
                SceneManager.LoadScene(gameSceneName);
        }
    }
    
    // 👇 打开存档菜单
    private void OnLoadGameClicked()
    {
        if (saveMenu != null)
        {
            saveMenu.OpenMenu();
        }
    }

    private void OnExitClicked()
    {
        Debug.Log("Quit Game");
        Application.Quit();
    }
}