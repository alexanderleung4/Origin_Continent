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

    [Header("状态与特效 (Buffs & VFX)")]
    public Transform buffContainer; // Buff 小图标的父节点
    public Transform vfxSpawnPoint; // (可选) 挨打时爆特效或跳字的位置，不填默认用 bodyImage
}