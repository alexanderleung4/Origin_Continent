using UnityEngine;
using System.Collections.Generic;

public class EnhanceManager : MonoBehaviour
{
    public static EnhanceManager Instance { get; private set; }

    [Header("经济设置")]
    [Tooltip("每点经验所需的金币手续费")]
    public int goldCostPerExp = 2; 

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 核心强化接口：吃掉材料，注入经验
    /// </summary>
    public bool TryEnhance(RuntimeEquipment target, Dictionary<InventorySlot, int> materials)
    {
        if (target == null || materials == null || materials.Count == 0) return false;
        
        if (target.level >= RuntimeEquipment.MAX_LEVEL)
        {
            if (UI_SystemToast.Instance != null) UI_SystemToast.Instance.Show("Sys", "装备已达满级！", 0, null);
            return false;
        }

        // 1. 计算总经验
        int totalExp = 0;
        foreach (var kvp in materials)
        {
            InventorySlot slot = kvp.Key;
            int amount = kvp.Value;

            if (slot.equipmentInstance != null)
                totalExp += slot.equipmentInstance.GetTotalFeedValue() * amount; // 肉身 amount 必为 1
            else if (slot.itemData != null)
                totalExp += slot.itemData.feedExpValue * amount;
        }

        if (totalExp <= 0) return false;

        // 2. 结算金币手续费
        int cost = totalExp * goldCostPerExp;
        if (GameManager.Instance.Player.Gold < cost)
        {
            if (UI_SystemToast.Instance != null) UI_SystemToast.Instance.Show("Sys", "金币不足！", 0, null);
            return false;
        }

        // 3. 扣除金币与狗粮实体
        GameManager.Instance.Player.Gold -= cost;
        foreach (var kvp in materials)
        {
            InventorySlot slot = kvp.Key;
            int amount = kvp.Value;

            if (slot.equipmentInstance != null)
            {
                // 实体装备，直接从背包连根拔起
                InventoryManager.Instance.inventory.Remove(slot);
            }
            else
            {
                // 堆叠材料，按数量扣除
                slot.Remove(amount);
                if (slot.amount <= 0) InventoryManager.Instance.inventory.Remove(slot);
            }
        }
        
        // 触发背包刷新事件
        InventoryManager.Instance.OnInventoryChanged?.Invoke();

        // 4. 注入经验并判定升级
        int startLevel = target.level;
        target.AddExp(totalExp, out int levelsGained);

        // 5. 播报结果
        if (levelsGained > 0)
        {
            if (UI_SystemToast.Instance != null) UI_SystemToast.Instance.Show(target.uid, $"强化成功！{target.blueprint.itemName} 升至 +{target.level}", 0, target.blueprint.icon);
            Debug.Log($"[Enhance] 武器升级！{startLevel} -> {target.level}，吸收了 {totalExp} 经验。");
        }
        else
        {
            if (UI_SystemToast.Instance != null) UI_SystemToast.Instance.Show(target.uid, $"吸收了 {totalExp} 点经验", 0, target.blueprint.icon);
            Debug.Log($"[Enhance] 喂食成功，当前经验槽进度: {target.currentExp}/{target.GetExpToNextLevel()}");
        }

        return true;
    }

    /// <summary>
    /// 智能算法：一键放入狗粮
    /// </summary>
    public Dictionary<InventorySlot, int> AutoSelectMaterials(RuntimeEquipment target, int maxSlots = 6)
    {
        Dictionary<InventorySlot, int> selected = new Dictionary<InventorySlot, int>();
        if (target == null || target.level >= RuntimeEquipment.MAX_LEVEL) return selected;

        // 设定一个小目标：先帮玩家凑够升 1 级所需的经验
        int expNeeded = target.GetExpToNextLevel() - target.currentExp;
        int currentSlotsUsed = 0;
        int expGathered = 0;

        // 获取背包中所有合法的狗粮
        List<InventorySlot> validFodders = new List<InventorySlot>();
        foreach (var slot in InventoryManager.Instance.inventory)
        {
            // 排除1：不能自己吃自己
            if (slot.equipmentInstance == target) continue; 
            
            if (slot.equipmentInstance != null)
            {
                // 排除2：绝对禁止自动吃紫装和金装 (保护机制)
                if (slot.equipmentInstance.rarity >= EquipmentRarity.Epic) continue;
                validFodders.Add(slot);
            }
            else if (slot.itemData != null && slot.itemData.feedExpValue > 0)
            {
                // 是含有经验值的普通材料 (如打磨石)
                validFodders.Add(slot);
            }
        }

        // 排序规则：专用强化石优先 -> 白装 -> 蓝装
        validFodders.Sort((a, b) => 
        {
            int scoreA = GetFodderPriorityScore(a);
            int scoreB = GetFodderPriorityScore(b);
            return scoreA.CompareTo(scoreB);
        });

        // 开始挑选
        foreach (var slot in validFodders)
        {
            if (currentSlotsUsed >= maxSlots) break; // 槽位塞满了
            if (expGathered >= expNeeded) break;     // 经验凑够了，防止严重溢出浪费

            if (slot.equipmentInstance != null)
            {
                selected.Add(slot, 1);
                currentSlotsUsed++;
                expGathered += slot.equipmentInstance.GetTotalFeedValue();
            }
            else
            {
                // 如果是堆叠材料，计算需要拿几个
                int expPerItem = slot.itemData.feedExpValue;
                int expDeficit = expNeeded - expGathered;
                int itemsNeeded = Mathf.CeilToInt((float)expDeficit / expPerItem);
                
                int itemsToTake = Mathf.Min(itemsNeeded, slot.amount);
                
                selected.Add(slot, itemsToTake);
                currentSlotsUsed++; // UI上占用一个格子
                expGathered += itemsToTake * expPerItem;
            }
        }

        return selected;
    }

    // 辅助评分：分数越低，优先度越高被吃掉
    private int GetFodderPriorityScore(InventorySlot slot)
    {
        if (slot.equipmentInstance == null) return 0; // 专用强化材料最先吃
        if (slot.equipmentInstance.rarity == EquipmentRarity.Common) return 1; // 白装
        if (slot.equipmentInstance.rarity == EquipmentRarity.Rare) return 2;   // 蓝装
        return 99; // 兜底
    }
}