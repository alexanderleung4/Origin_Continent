using UnityEngine;
using UnityEngine.UI;

public class UI_SceneStage : MonoBehaviour
{
    [Header("绑定的关卡数据")]
    public StageData stageData;

    private void Start()
    {
        Button btn = GetComponent<Button>();
        if (btn != null && stageData != null)
        {
            btn.onClick.AddListener(() => 
            {
                // 点下去的瞬间，呼叫战斗引擎！
                if (BattleManager.Instance != null)
                {
                    BattleManager.Instance.StartBattle(stageData);
                }
            });
        }
    }
}