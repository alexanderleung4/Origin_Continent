using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement; // 👈 需要引用场景管理

public class UI_SaveSlot : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI infoText;
    public TextMeshProUGUI timeText;
    public GameObject emptyVisual;
    public GameObject contentVisual;

    [Header("Interaction")]
    public Button slotMainButton;
    public GameObject buttonsRoot;
    public Button btnSave;
    public Button btnLoad;
    public Button btnDelete;

    private int mySaveID;
    public bool IsExpanded { get; private set; }

    // 👇 新增: 记录游戏场景名字 (用于在主菜单读档时跳转)
    private string gameSceneName = "MainMenu"; 

    public void Setup(int saveID, string targetSceneName = "MainMenu")
    {
        mySaveID = saveID;
        this.gameSceneName = targetSceneName;
        IsExpanded = false;

        // 1. 设置标题
        if (titleText) titleText.text = (saveID == -1) ? "自动存档 (Auto)" : $"存档 {saveID + 1}";

        // 2. 👇 修改: 使用静态方法读取数据 (兼容主菜单)
        SaveData data = SaveManager.GetSaveInfoStatic(saveID);
        bool hasFile = (data != null);

        // 3. 显隐控制
        if (emptyVisual) emptyVisual.SetActive(!hasFile);
        if (contentVisual) contentVisual.SetActive(hasFile);

        // 4. 填充信息
        if (hasFile)
        {
            if (infoText) infoText.text = $"Lv.{data.player.level} 冒险者 | {data.locationID}";
            if (timeText) timeText.text = data.timestamp;
        }

        // 5. 交互初始化
        if (buttonsRoot) buttonsRoot.SetActive(false);
        if (slotMainButton)
        {
            slotMainButton.onClick.RemoveAllListeners();
            slotMainButton.onClick.AddListener(() => UI_SaveMenu.Instance.OnSlotClicked(this));
        }

        // --- 按钮逻辑绑定 ---
        
        // [保存]: 只有在游戏里 (Instance存在) 才能保存！主菜单不能保存
        if (btnSave)
        {
            // 如果 SaveManager.Instance 是 null，说明在主菜单，禁用保存
            bool canSave = (SaveManager.Instance != null);
            
            // 自动存档位通常不能手动存
            if (saveID == -1) canSave = false; 

            btnSave.interactable = canSave;
            btnSave.onClick.RemoveAllListeners();
            if (canSave)
            {
                btnSave.onClick.AddListener(() => {
                    SaveManager.Instance.SaveGame(mySaveID);
                    // 刷新一下显示
                    Setup(mySaveID, gameSceneName);
                });
            }
        }

        // [读取]: 支持双模式
        if (btnLoad)
        {
            btnLoad.interactable = hasFile;
            btnLoad.onClick.RemoveAllListeners();
            btnLoad.onClick.AddListener(OnLoadClicked);
        }

        // [删除]: 支持双模式 (静态删除)
        if (btnDelete)
        {
            btnDelete.interactable = hasFile;
            btnDelete.onClick.RemoveAllListeners();
            btnDelete.onClick.AddListener(OnDeleteClicked);
        }
    }

    // --- 点击事件处理 ---

    private void OnLoadClicked()
    {
        // 🔍 获取当前场景的名字
        string currentSceneName = SceneManager.GetActiveScene().name;
        
        // 只有当 SaveManager 存在，且【绝对不在】标题画面时，才算是“游戏中”
        // 假设您的标题场景叫 "Scene_Title" (请替换为您真实的标题场景名)
        bool isInGame = (SaveManager.Instance != null) && (currentSceneName != "Scene_Title");

        if (isInGame)
        {
            // [情况 A] 真·在游戏中 -> 原地读取
            Debug.Log("[SaveSlot] 检测到游戏中读取，刷新数据...");
            SaveManager.Instance.LoadGame(mySaveID);
            
            // 读取完最好关闭菜单，不然怪怪的
            if (UI_SaveMenu.Instance != null) UI_SaveMenu.Instance.CloseMenu();
            if (UIManager.Instance != null) UIManager.Instance.CloseAllMenus(); // 还可以顺便恢复时间流速
        }
        else
        {
            // [情况 B] 在主菜单 (或者 SaveManager 还没醒) -> 跳转场景
            Debug.Log($"[SaveSlot] 检测到标题画面读取 (Slot {mySaveID})，执行跨场景加载...");
            
            // 1. 设置信箱
            SaveManager.AutoLoadSlot = mySaveID;
            
            // 2. 切换场景
            // 这里的 gameSceneName 应该是 "MainMenu" (您之前设置的变量)
            if (SceneFader.Instance != null) 
                SceneFader.Instance.FadeToScene(gameSceneName);
            else 
                SceneManager.LoadScene(gameSceneName);
        }
    }

    private void OnDeleteClicked()
    {
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.DeleteSave(mySaveID);
        }
        else
        {
            // 主菜单模式下，我们需要手动删除文件并刷新列表
            string fileName = (mySaveID == -1) ? "save_auto.json" : $"save_{mySaveID}.json";
            string path = System.IO.Path.Combine(Application.persistentDataPath, fileName);
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
                // 通知 Menu 刷新
                UI_SaveMenu.Instance.RefreshList();
            }
        }
    }

    // --- 展开逻辑 ---
    public void SetExpanded(bool expand)
    {
        IsExpanded = expand;
        if (buttonsRoot) buttonsRoot.SetActive(expand);
    }
}