using UnityEngine;
using UnityEngine.SceneManagement;

[CreateAssetMenu(fileName = "Effect_CurseDeath", menuName = "Origin/Trait Effects/Curse Max Death")]
public class Effect_CurseDeath : TraitEffectBase
{
    public override void OnTraitAdded(RuntimeCharacter target, int currentLevel)
    {
        Debug.LogError($"[Trait Effect] 触发极其恶劣的被动：{target.Name} 灵魂溃散！");
        
        target.CurrentHP = 0;
        // 删档
        if (SaveManager.Instance != null && SaveManager.Instance.currentSaveID >= 0) 
        {
            SaveManager.Instance.DeleteSave(SaveManager.Instance.currentSaveID);
        }
        // 踢出游戏
        SceneManager.LoadScene("Scene_Title");
    }
}