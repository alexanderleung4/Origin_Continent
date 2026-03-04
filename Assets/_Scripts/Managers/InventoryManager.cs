using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;

[System.Serializable]
public class InventorySlot
{
    public ItemData itemData;
    public int amount;
    public RuntimeEquipment equipmentInstance;
    
    public InventorySlot(ItemData item, int count, RuntimeEquipment equip = null) 
    { 
        itemData = item; 
        amount = count; 
        equipmentInstance = equip;
    }
    
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

    // 默认获取物品逻辑
    public void AddItem(ItemData item, int count = 1, bool isSilent = false)
    {
        if (item == null) return;
        if (item is EquipmentData equipBlueprint)
        {
            // 🚨 警告系统：告知开发者不应该直接 AddItem(图纸)
            Debug.LogWarning($"[Inventory] 警告：拦截到试图直接向背包发放图纸 [{item.itemName}]。已强行通过 ForgeEngine 为其兜底生成白板肉身！请尽量传入 RuntimeEquipment！");
            
            for (int i = 0; i < count; i++)
            {
                RuntimeEquipment newEquip = ForgeEngine.Generate(equipBlueprint, EquipmentRarity.Common);
                inventory.Add(new InventorySlot(equipBlueprint, 1, newEquip));
            }
            OnInventoryChanged?.Invoke();
            if (!isSilent && UI_SystemToast.Instance != null) UI_SystemToast.Instance.Show(item.name, $"获得装备: {item.itemName}", count, item.icon);
            return; 
        }

        bool itemAdded = false;
        if (item.isStackable)
        {
            InventorySlot existingSlot = inventory.Find(slot => slot.itemData == item);
            if (existingSlot != null) { existingSlot.Add(count); itemAdded = true; }
        }
        if (!itemAdded) inventory.Add(new InventorySlot(item, count));
        OnInventoryChanged?.Invoke();
        if (!isSilent && UI_SystemToast.Instance != null) UI_SystemToast.Instance.Show(item.name, $"获得物品: {item.itemName}", count, item.icon);
    }

    // 👇 核心脱壳重载：用于卸下装备时，原封不动把“肉身”塞回背包
    public void AddItem(RuntimeEquipment equip, int count = 1, bool isSilent = false)
    {
        if (equip == null) return;
        inventory.Add(new InventorySlot(equip.blueprint, 1, equip));
        OnInventoryChanged?.Invoke();
        if (!isSilent && UI_SystemToast.Instance != null) 
            UI_SystemToast.Instance.Show(equip.uid, $"获得装备: {equip.blueprint.itemName}", 1, equip.blueprint.icon);
    }

    public void UnequipItem(EquipmentSlot slot)
    {
        var player = (UI_CharacterSheet.Instance != null && UI_CharacterSheet.Instance.CurrentFocusCharacter != null) 
                     ? UI_CharacterSheet.Instance.CurrentFocusCharacter : GameManager.Instance.Player;
        if (player == null) return;

        RuntimeEquipment removedItem = player.Unequip(slot);

        if (removedItem != null)
        {
            // 👇 修复：静默塞回背包 (isSilent = true)
            AddItem(removedItem, 1, true); 
            
            // 👇 修复：在这里手动呼叫精准的“卸下”播报
            if (UI_SystemToast.Instance != null) 
                UI_SystemToast.Instance.Show(removedItem.uid, $"卸下装备: {removedItem.blueprint.itemName}", 0, removedItem.blueprint.icon);
                
            Debug.Log($"[Inventory] 卸下装备: {removedItem.blueprint.itemName} (UID: {removedItem.uid})");
        }
        
        if (UIManager.Instance != null) UIManager.Instance.RefreshPlayerStatus();
    }

    public void RemoveItem(ItemData item, int count = 1)
    {
        InventorySlot slot = inventory.Find(s => s.itemData == item);
        if (slot != null)
        {
            slot.Remove(count);
            if (slot.amount <= 0) inventory.Remove(slot);
            OnInventoryChanged?.Invoke();
        }
    }

    public bool HasItem(ItemData item, int count = 1)
    {
        InventorySlot slot = inventory.Find(s => s.itemData == item);
        return slot != null && slot.amount >= count;
    }

    // --- 👇 核心脱壳：引入 UseSlot 以精准锁定肉身 ---
    public void UseSlot(InventorySlot slot)
    {
        if (slot == null || slot.itemData == null) return;

        if (slot.equipmentInstance != null)
        {
            // 它是实体装备！路由给详情面板！
            if (UI_EquipmentDetailPanel.Instance != null)
            {
                UI_EquipmentDetailPanel.Instance.OpenPanel(slot.equipmentInstance, EquipmentPanelSource.Inventory);
            }
            else
            {
                // UI坏了的保底盲穿
                if (UI_TargetSelector.Instance != null)
                {
                    UI_TargetSelector.Instance.OpenSelector($"请选择穿戴者：\n{slot.equipmentInstance.blueprint.itemName}", AvatarDisplayMode.NameOnly, (selectedTarget) => 
                    {
                        EquipItemLogic(slot.equipmentInstance, selectedTarget);
                    });
                }
                else EquipItemLogic(slot.equipmentInstance, GameManager.Instance.Player);
            }
        }
        else
        {
            // 它是普通消耗品，走旧逻辑
            UseItem(slot.itemData);
        }
    }

    // 保留给消耗品的旧接口
    public void UseItem(ItemData item)
    {
        if (item == null) return;
        if (item.type == ItemType.Consumable)
        {
            if (GameManager.Instance.CurrentState == GameState.Battle)
            {
                if (BattleManager.Instance.TryUseItem(item)) ConsumeItem(item);
            }
            else
            {
                if (UI_TargetSelector.Instance != null)
                {
                    UI_TargetSelector.Instance.OpenSelector($"请选择目标：\n使用 {item.itemName}", AvatarDisplayMode.FullStats, (selectedTarget) => 
                    {
                        ApplyItemEffect(selectedTarget, item);
                        ConsumeItem(item);
                        Debug.Log($"对 {selectedTarget.Name} 使用了 {item.itemName}");
                    });
                }
                else
                {
                    ApplyItemEffect(GameManager.Instance.Player, item);
                    ConsumeItem(item);
                }
            }
        }
    }

    // --- 👇 穿戴逻辑彻底接管肉身 ---
    public void EquipItemLogic(RuntimeEquipment newEquip, RuntimeCharacter target)
    {
        if (target == null || newEquip == null) return;

        // 把旧衣服脱了塞回背包
        RuntimeEquipment oldEquip = target.Unequip(newEquip.blueprint.slotType);
        if (oldEquip != null) AddItem(oldEquip, 1, true);

        // 穿新衣服
        target.Equip(newEquip);

        // 从背包抹除这件肉身
        InventorySlot slot = inventory.Find(s => s.equipmentInstance == newEquip);
        if (slot != null) { inventory.Remove(slot); OnInventoryChanged?.Invoke(); }

        Debug.Log($"[换装] {target.Name} 穿上了 {newEquip.blueprint.itemName}({newEquip.rarity}) | 新攻击力: {target.Attack}");
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
            if (target.CurrentHP > target.MaxHP) target.CurrentHP = target.MaxHP;
        }
        if (item.manaAmount > 0)
        {
            target.CurrentMP += item.manaAmount;
            if (target.CurrentMP > target.MaxMP) target.CurrentMP = target.MaxMP;
        }
        if (UIManager.Instance != null) UIManager.Instance.RefreshPlayerStatus();
    }
}