using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UI_BattleEntity : MonoBehaviour
{
    [Header("实体信息 (Entity Info)")]
    public Image bodyImage;         // 立绘显示
    public TextMeshProUGUI nameText;// 名字显示

    [Header("状态条 (Stat Bars)")]
    public Slider hpSlider;         // 血条
    public TextMeshProUGUI hpText;  // 血量具体数值
    
    public Slider mpSlider;         // 蓝条
    public TextMeshProUGUI mpText;  // 魔力具体数值
    
    public Slider shieldSlider;     // 护盾白条
    
    public Slider staminaSlider;    // 精力条
    public TextMeshProUGUI staminaText; // 精力具体数值

    [Header("状态与特效 (Buffs & VFX)")]
    public Transform buffContainer; // Buff 小图标的父节点
    public Transform vfxSpawnPoint; // (可选) 挨打时爆特效或跳字的位置，不填默认用 bodyImage
    // 👇 --- 核心新增：小人召唤与动画控制 --- 👇
    [Header("骨骼小人容器 (Chibi Container)")]
    [Tooltip("小人生成的位置，建议在这个 Prefab 里建一个空物体放在脚底")]
    public Transform chibiSpawnPoint; 
    
    // 这个不需要手动拖拽，代码会自动获取
    [HideInInspector] public Animator chibiAnimator; 
    private GameObject currentChibiInstance;

    // --- 新增：初始化实体的逻辑 ---
    public void SetupEntity(RuntimeCharacter character)
    {
        if (character == null || character.data == null) return;

        // 1. 刷新文本信息
        if (nameText != null) nameText.text = character.Name;

        // 2. 召唤骨骼小人
        SpawnChibi(character.data.combatChibiPrefab);

        // 3. 首次刷新血条等数值
        RefreshEntity(character);
    }

    private void SpawnChibi(GameObject prefab)
    {
        // 如果已经有小人了，先清理掉 (防止重复召唤)
        if (currentChibiInstance != null) Destroy(currentChibiInstance);
        if (prefab == null || chibiSpawnPoint == null) return;

        // 实例化小人作为 spawnPoint 的子物体
        currentChibiInstance = Instantiate(prefab, chibiSpawnPoint);
        currentChibiInstance.transform.localPosition = Vector3.zero;

        // 获取小人身上的动画状态机，供以后受击、攻击时调用
        chibiAnimator = currentChibiInstance.GetComponentInChildren<Animator>();
        
        if (chibiAnimator == null)
        {
            Debug.LogWarning($"[{gameObject.name}] 生成的小人身上没有找到 Animator 组件！");
        }
    }

    // 您之前写的刷新血条的逻辑放这里...
    public void RefreshEntity(RuntimeCharacter character)
    {
        // ... (更新血条、蓝条的代码保持不变) ...
    }
}