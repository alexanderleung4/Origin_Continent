using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class UI_Battle : MonoBehaviour
{
    [Header("Global Panels (操作面板)")]
    public GameObject actionPanel;     // 攻击/技能/逃跑按钮菜单
    public GameObject skillPanel;      // 技能列表面板
    
    [Header("Old UI Buttons (旧版交互按钮)")]
    public Button btnItem;             // 道具按钮
    public Button btnRun;              // 逃跑按钮
    public List<Button> actionCategoryButtons; // 技能大类按钮 (攻击, 魔法等)
    public Transform skillContainer;   // 技能生成的父节点

    [Header("UI Text (文本引用)")]
    public TextMeshProUGUI battleLogText; // 战斗日志文本

    [Header("Player Avatar (左下角当前行动者头像)")]
    public Image playerAvatarImage;
    
    [Header("Grid & Prefabs (阵列与实体预制体)")]
    public GameObject battleEntityPrefab; // 拖入挂载了 UI_BattleEntity 的那个 Prefab

    [Tooltip("玩家阵列插槽 (0,1,2 为前排 | 3,4,5 为后排)")]
    public Transform[] playerSlots = new Transform[6];

    [Tooltip("敌人阵列插槽 (0,1,2 为前排 | 3,4,5 为后排)")]
    public Transform[] enemySlots = new Transform[6];

    [Header("Timeline UI (右侧行动条)")]
    public Transform timelineContainer;    // 挂载了 VerticalLayoutGroup 的父节点
    public GameObject timelineIconPrefab;  // 拖入挂载了 UI_TimelineIcon 的预制体
    [Header("Battle Announcement (战斗公告演出)")]
    public UI_BattleAnnouncement announcement;
}