using UnityEngine;
using System.Collections.Generic;

public class ForgeManager : MonoBehaviour
{
    [Header("测试专用")]
    public RecipeData testRecipe; // 👈 在这里拖入您配好的测试配方
    public static ForgeManager Instance { get; private set; }

    [Header("品质生成概率 (Rarity Weights 0-100)")]
    public float weightCommon = 60f;    // 白
    public float weightRare = 25f;      // 蓝
    public float weightEpic = 10f;      // 紫
    public float weightLegendary = 5f;  // 金！

    

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 核心锻造接口 (由铁匠铺 UI 调用)
    /// </summary>
    public bool TryCraftEquipment(RecipeData recipe)
    {
        if (recipe == null || recipe.outputEquipment == null) return false;

        // 1. 验证金币
        if (GameManager.Instance.Player.Gold < recipe.craftingCost)
        {
            if (UI_SystemToast.Instance != null) UI_SystemToast.Instance.Show("Sys", "金币不足！", 0, null);
            return false;
        }

        // 2. 验证所有材料是否充足
        foreach (var ing in recipe.ingredients)
        {
            if (!InventoryManager.Instance.HasItem(ing.item, ing.amount))
            {
                if (UI_SystemToast.Instance != null) UI_SystemToast.Instance.Show("Sys", $"材料不足: {ing.item.itemName}", 0, ing.item.icon);
                return false;
            }
        }

        // 3. 扣除费用与材料
        GameManager.Instance.Player.Gold -= recipe.craftingCost;
        foreach (var ing in recipe.ingredients)
        {
            InventoryManager.Instance.RemoveItem(ing.item, ing.amount);
        }

        // 4. 🎲 摇骰子：决定命运的品质！
        EquipmentRarity rolledRarity = RollRarity();
        
        // 5. 铸造肉身
        RuntimeEquipment newEquip = new RuntimeEquipment(recipe.outputEquipment, rolledRarity);

        // 6. 注入随机词条池 (Affixes)
        GenerateRandomAffixes(newEquip);

        // 7. 发货到玩家背包
        InventoryManager.Instance.AddItem(newEquip, 1, true); // true = 静默添加，因为我们要定制更炫酷的播报

        // 8. 炫酷播报
        string rarityColor = GetRarityColor(rolledRarity);
        string toastMsg = $"锻造成功: <color={rarityColor}>{recipe.outputEquipment.itemName}</color>";
        
        if (rolledRarity == EquipmentRarity.Legendary) 
        {
            toastMsg = $"<color=#FFD700>【大成功】绝世神兵出炉：{recipe.outputEquipment.itemName}！</color>";
            // TODO: 未来在这里可以加全屏特效或专属音效！
        }
        
        if (UI_SystemToast.Instance != null) 
            UI_SystemToast.Instance.Show(newEquip.uid, toastMsg, 0, recipe.outputEquipment.icon);

        Debug.Log($"[Forge] 成功打造了 {newEquip.blueprint.itemName} ({rolledRarity})，附带了 {newEquip.affixes.Count} 条属性！");
        return true;
    }
    /// <summary>
    /// 核心拆解接口 (将肉身解构为材料)
    /// </summary>
    public bool SalvageEquipment(RuntimeEquipment equip)
    {
        if (equip == null) return false;

        // 1. 物理寻址：从背包里找到这件唯一的肉身并抹除它
        InventorySlot slot = InventoryManager.Instance.inventory.Find(s => s.equipmentInstance == equip);
        if (slot == null) 
        {
            Debug.LogWarning("[Forge] 拆解失败：背包中找不到该装备实体！");
            return false; 
        }
        InventoryManager.Instance.inventory.Remove(slot);

        // 2. 算力乘区：品质越好，拆出来的材料越多！
        // 普通 1.0x | 稀有 1.5x | 史诗 2.0x | 传说 3.0x
        float rarityMult = 1f;
        if (equip.rarity == EquipmentRarity.Rare) rarityMult = 1.5f;
        if (equip.rarity == EquipmentRarity.Epic) rarityMult = 2.0f;
        if (equip.rarity == EquipmentRarity.Legendary) rarityMult = 3.0f;

        string toastMsg = $"拆解 {equip.blueprint.itemName} 获得:\n";

        // 3. 发放产物
        if (equip.blueprint.salvageRewards == null || equip.blueprint.salvageRewards.Count == 0)
        {
            toastMsg += "一堆毫无价值的残渣...";
        }
        else
        {
            foreach (var reward in equip.blueprint.salvageRewards)
            {
                // 计算最终数量 (保底最少1个)
                int finalAmount = Mathf.Max(1, Mathf.RoundToInt(reward.amount * rarityMult));
                InventoryManager.Instance.AddItem(reward.item, finalAmount, true); // true 代表静默添加，不单独弹窗
                toastMsg += $"{reward.item.itemName} x{finalAmount}  ";
            }
        }

        // 4. 触发全局刷新与华丽播报
        InventoryManager.Instance.OnInventoryChanged?.Invoke();

        if (UI_SystemToast.Instance != null)
            UI_SystemToast.Instance.Show(equip.uid, toastMsg, 0, equip.blueprint.icon);

        Debug.Log($"[Forge] {toastMsg}");
        return true;
    }

    // --- 内部随机引擎 ---

    private EquipmentRarity RollRarity()
    {
        float roll = Random.Range(0f, 100f);
        if (roll <= weightLegendary) return EquipmentRarity.Legendary;
        roll -= weightLegendary;
        if (roll <= weightEpic) return EquipmentRarity.Epic;
        roll -= weightEpic;
        if (roll <= weightRare) return EquipmentRarity.Rare;
        return EquipmentRarity.Common;
    }

    private void GenerateRandomAffixes(RuntimeEquipment equip)
    {
        int affixCount = 0;
        switch (equip.rarity)
        {
            case EquipmentRarity.Common: affixCount = 0; break;
            case EquipmentRarity.Rare: affixCount = Random.Range(1, 3); break;      
            case EquipmentRarity.Epic: affixCount = Random.Range(2, 4); break;      
            case EquipmentRarity.Legendary: affixCount = Random.Range(3, 5); break; 
        }

        if (affixCount == 0) return;

        // 👇 核心升级：读取图纸的专属词条池！
        List<StatType> pool = equip.blueprint.possibleAffixes;
        
        // 如果策划没配，就给个默认的全属性池
        if (pool == null || pool.Count == 0)
        {
            pool = new List<StatType> { StatType.Attack, StatType.Defense, StatType.MaxHP, StatType.MaxMP, StatType.Speed, StatType.CritRate, StatType.CritDamage };
        }

        for (int i = 0; i < affixCount; i++)
        {
            // 从专属池里抽签
            StatType randomStat = pool[Random.Range(0, pool.Count)];
            ItemAffix newAffix = new ItemAffix();
            newAffix.statType = randomStat;

            float baseVal = 0;
            bool isPercent = false;

            if (randomStat == StatType.CritRate || randomStat == StatType.CritDamage)
            {
                isPercent = true;
                baseVal = Random.Range(2f, 8f); 
            }
            else
            {
                isPercent = Random.value > 0.5f; 
                if (isPercent) baseVal = Random.Range(3f, 12f); 
                else baseVal = Random.Range(10f, 50f);          
            }

            float rarityMultiplier = 1f + ((int)equip.rarity * 0.3f); 
            newAffix.value = Mathf.Round(baseVal * rarityMultiplier * 10f) / 10f; 
            newAffix.isPercent = isPercent;

            equip.affixes.Add(newAffix);
        }
    }

    private string GetRarityColor(EquipmentRarity rarity)
    {
        switch (rarity)
        {
            case EquipmentRarity.Common: return "#FFFFFF"; 
            case EquipmentRarity.Rare: return "#00A2FF";   
            case EquipmentRarity.Epic: return "#D042FF";   
            case EquipmentRarity.Legendary: return "#FFD700"; 
            default: return "#FFFFFF";
        }
    }

    // ==========================================
    // 🛠️ 测试专用接口
    // ==========================================
    [ContextMenu("Test: 一键神锻 (无视材料与金币)")]
    public void TestCraftBypass()
    {
        if (testRecipe == null || testRecipe.outputEquipment == null) 
        {
            Debug.LogWarning("请先在 ForgeManager 的 Test Recipe 槽位里拖入一个配方！");
            return;
        }

        // 强行摇骰子
        EquipmentRarity rolledRarity = RollRarity();
        RuntimeEquipment newEquip = new RuntimeEquipment(testRecipe.outputEquipment, rolledRarity);
        GenerateRandomAffixes(newEquip);

        // 强行发货
        InventoryManager.Instance.AddItem(newEquip, 1, false); 
        
        Debug.Log($"[Cheat Forge] 成功强造了 {newEquip.blueprint.itemName} ({rolledRarity})，附带了 {newEquip.affixes.Count} 条属性！");
    }
}