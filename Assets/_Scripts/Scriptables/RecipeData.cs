using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct CraftingIngredient
{
    public ItemData item;   // 所需材料 (例如：铁矿石)
    public int amount;      // 所需数量 (例如：5)
}

[CreateAssetMenu(fileName = "NewRecipe", menuName = "Origin/Forge/Recipe Data")]
public class RecipeData : ScriptableObject
{
    [Header("产出 (Output)")]
    public string recipeID;
    public EquipmentData outputEquipment; // 锻造成功后生成的图纸原型

    [Header("消耗 (Cost)")]
    public int craftingCost; // 消耗金币
    public List<CraftingIngredient> ingredients; // 消耗材料列表

    [Header("解锁条件 (Unlock)")]
    public bool isUnlockedByDefault = true; 
    // 未来可以拓展: public string unlockQuestID; (完成某任务后才出现在铁匠铺)
}