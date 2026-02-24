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
}