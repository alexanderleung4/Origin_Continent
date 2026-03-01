using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;

[System.Serializable]
public class InventorySlot
{
    public ItemData itemData;
    public int amount;
    public InventorySlot(ItemData item, int count) { itemData = item; amount = count; }
    public void Add(int count) => amount += count;
    public void Remove(int count) => amount -= count;
}


public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [Header("Storage")]
    public List<InventorySlot> inventory = new List<InventorySlot>();
    public UnityEvent OnInventoryChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void AddItem(ItemData item, int count = 1, bool isSilent = false)
    {
        if (item == null) return;
        bool itemAdded = false;
        if (item.isStackable)
        {
            InventorySlot existingSlot = inventory.Find(slot => slot.itemData == item);
            if (existingSlot != null) { existingSlot.Add(count); itemAdded = true; }
        }
        if (!itemAdded) inventory.Add(new InventorySlot(item, count));
        Debug.Log($"[Inventory] 获得: {item.itemName} x{count}");
        OnInventoryChanged?.Invoke();
        if (!isSilent && UI_SystemToast.Instance != null)
        {
            UI_SystemToast.Instance.Show(item.name, $"获得物品: {item.itemName}", count, item.icon);
        }
    }
    // --- 👇 新增: 卸下装备并放回背包 ---
    public void UnequipItem(EquipmentSlot slot)
    {
        // 👇 核心改动：从当前 UI 焦点角色身上脱装备
        var player = (UI_CharacterSheet.Instance != null && UI_CharacterSheet.Instance.CurrentFocusCharacter != null) 
                     ? UI_CharacterSheet.Instance.CurrentFocusCharacter 
                     : GameManager.Instance.Player;
        if (player == null) return;

        // 1. 从身上脱下
        EquipmentData removedItem = player.Unequip(slot);

        // 2. 放回背包
        if (removedItem != null)
        {
            AddItem(removedItem, 1);
            Debug.Log($"[Inventory] 卸下装备: {removedItem.itemName}");
        }
        
        // 3. 刷新 UI (角色面板)
        // 这一步通常由 UI 监听 Inventory 变化自动完成，或者手动刷新
        if (UIManager.Instance != null) UIManager.Instance.RefreshPlayerStatus();
    }
    // 公开的移除方法 (用于商店卖出、任务上交等)
    public void RemoveItem(ItemData item, int count = 1)
    {
        InventorySlot slot = inventory.Find(s => s.itemData == item);
        if (slot != null)
        {
            slot.Remove(count);
            if (slot.amount <= 0)
            {
                inventory.Remove(slot);
            }
            OnInventoryChanged?.Invoke();
        }
    }

    public bool HasItem(ItemData item, int count = 1)
    {
        InventorySlot slot = inventory.Find(s => s.itemData == item);
        return slot != null && slot.amount >= count;
    }

    // --- 核心功能: 使用物品 (修改版) ---
    public void UseItem(ItemData item)
    {
        if (item == null) return;

        // 分流 A: 消耗品
        if (item.type == ItemType.Consumable)
        {
            if (GameManager.Instance.CurrentState == GameState.Battle)
            {
                if (BattleManager.Instance.TryUseItem(item)) ConsumeItem(item);
            }
            else
            {
                // 👇 核心改动：呼叫目标选择器！问玩家这药喂给谁！
                if (UI_TargetSelector.Instance != null)
                {
                    UI_TargetSelector.Instance.OpenSelector($"请选择目标：\n使用 {item.itemName}", (selectedTarget) => 
                    {
                        ApplyItemEffect(selectedTarget, item);
                        ConsumeItem(item);
                        Debug.Log($"对 {selectedTarget.Name} 使用了 {item.itemName}");
                    });
                }
                else
                {
                    // 兜底
                    ApplyItemEffect(GameManager.Instance.Player, item);
                    ConsumeItem(item);
                }
            }
        }
            // 👇 分流 B: 装备 (升级为路由到详情面板)
        else if (item is EquipmentData equipData) 
        {
            if (UI_EquipmentDetailPanel.Instance != null)
            {
                // 拦截成功，弹出面板，告诉它是从背包(Inventory)打开的
                UI_EquipmentDetailPanel.Instance.OpenPanel(equipData, EquipmentPanelSource.Inventory);
            }
            else
            {
                // 保底逻辑：如果详情UI坏了，直接呼叫目标选择器盲穿！
                if (UI_TargetSelector.Instance != null)
                {
                    UI_TargetSelector.Instance.OpenSelector($"请选择穿戴者：\n{equipData.itemName}", (selectedTarget) => 
                    {
                        EquipItemLogic(equipData, selectedTarget);
                    });
                }
                else
                {
                    // 最终物理保底：直接强行塞给队长
                    EquipItemLogic(equipData, GameManager.Instance.Player);
                }
            }
        }
        else
        {
            Debug.Log($"这东西({item.itemName})不能直接使用！类型: {item.type}");
        }
    }

    // --- 👇 装备逻辑实现 ---
    public void EquipItemLogic(EquipmentData newEquip, RuntimeCharacter target)
    {
        if (target == null) return;

        EquipmentData oldEquip = target.Unequip(newEquip.slotType);

        if (oldEquip != null)
        {
            AddItem(oldEquip, 1, true); // 静默塞回背包，不弹窗
        }

        target.Equip(newEquip);
        ConsumeItem(newEquip);

        Debug.Log($"[换装] {target.Name} 穿上了 {newEquip.itemName} | 新攻击力: {target.Attack}");
        // 可选: 播放穿装备音效
    }

    private void ConsumeItem(ItemData item)
    {
        InventorySlot slot = inventory.Find(s => s.itemData == item);
        if (slot != null)
        {
            slot.Remove(1);
            if (slot.amount <= 0) inventory.Remove(slot);
            OnInventoryChanged?.Invoke(); 
        }
    }

    public void ApplyItemEffect(RuntimeCharacter target, ItemData item)
    {
        if (target == null || item == null) return;
        if (item.healAmount > 0)
        {
            target.CurrentHP += item.healAmount;
            // 使用属性 MaxHP 确保不超过上限
            if (target.CurrentHP > target.MaxHP) target.CurrentHP = target.MaxHP;
        }
        if (item.manaAmount > 0)
        {
            target.CurrentMP += item.manaAmount;
            if (target.CurrentMP > target.MaxMP) target.CurrentMP = target.MaxMP;
        }
        if (UIManager.Instance != null) UIManager.Instance.RefreshPlayerStatus();
    }
    
    [ContextMenu("Test: Print Inventory")]
    public void PrintInventory()
    {
        Debug.Log("--- 当前背包内容 ---");
        foreach (var slot in inventory)
        {
            Debug.Log($"- {slot.itemData.itemName}: {slot.amount}");
        }
    }

    
}