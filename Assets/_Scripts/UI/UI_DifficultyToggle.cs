using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Button))]
public class UI_DifficultyToggle : MonoBehaviour
{
    [Header("UI 引用")]
    [Tooltip("用来显示当前难度文字的 TextMeshPro")]
    public TextMeshProUGUI difficultyText;

    private GameDifficulty currentDiff;

    private void Start()
    {
        // 1. 界面打开时，读取本地硬盘记录的难度
        currentDiff = (GameDifficulty)PlayerPrefs.GetInt("GlobalDifficulty", (int)GameDifficulty.Origin);
        UpdateButtonText();
        
        // 2. 绑定点击事件
        GetComponent<Button>().onClick.AddListener(CycleDifficulty);
    }

    private void CycleDifficulty()
    {
        // 1. 循环切换：0(Story) -> 1(Origin) -> 2(Abyss) -> 0(Story)
        int nextDiff = ((int)currentDiff + 1) % 3;
        currentDiff = (GameDifficulty)nextDiff;
        
        // 2. 写入本地硬盘 (PlayerPrefs)
        PlayerPrefs.SetInt("GlobalDifficulty", (int)currentDiff);
        PlayerPrefs.Save(); // 强制落地保存
        
        // 3. 如果是在游戏进行中点击的，立刻同步给 GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.currentDifficulty = currentDiff;
        }

        // 4. 刷新视觉
        UpdateButtonText();
        Debug.Log($"[Settings] 难度已切换为: {currentDiff}");
    }

    private void UpdateButtonText()
    {
        if (difficultyText == null) return;
        
        switch (currentDiff)
        {
            case GameDifficulty.Story:  
                difficultyText.text = "难度: 叙事 (Story)"; 
                difficultyText.color = new Color(0.6f, 1f, 0.6f); // 绿色
                break;
            case GameDifficulty.Origin: 
                difficultyText.text = "难度: 起源 (Origin)"; 
                difficultyText.color = Color.white; 
                break;
            case GameDifficulty.Abyss:  
                difficultyText.text = "难度: 渊灭 (Abyss)"; 
                difficultyText.color = new Color(1f, 0.4f, 0.4f); // 红色警告
                break;
        }
    }
}