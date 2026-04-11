using UnityEngine;

public enum ItemType { Consumable, Material, Equipment, KeyItem }

[CreateAssetMenu(fileName = "NewItem", menuName = "Origin/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Basic Info")]
    public string itemID; 
    public string itemName; 
    [TextArea] public string description;
    public Sprite icon; 
    public ItemType type;

    [Header("Behavior")]
    public bool isStackable = true; 
    public int maxStack = 99;

    [Header("Economy")]
    public int buyPrice = 100;  
    public int sellPrice = 50;  
    public bool isSellable = true; 

    [Header("Consumable Stats")]
    public int healAmount;      
    public int manaAmount;      
    public int staminaAmount;   
    [Header("🔥 Enhancement (强化系统)")]
    [Tooltip("作为强化狗粮时，提供给目标装备的基础经验值")]
    public int feedExpValue = 10;

    [Header("Gift Settings (赠礼设定)")]
    public bool isGiftable; // 打勾说明能送
    public AffinityType giftAffinityType = AffinityType.Intimacy; // 默认加亲密度
    public int giftAffinityValue = 10; // 送出去加多少数值
}