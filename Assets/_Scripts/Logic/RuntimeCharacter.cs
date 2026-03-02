using UnityEngine;
using System.Collections.Generic;

[System.Serializable] 
public class RuntimeCharacter
{
    // --- 核心引用 ---
    public CharacterData data;

    // --- 基础状态 ---
    public string Name => data != null ? data.characterName : "Unknown";
    public int CurrentHP;      
    public int CurrentMP;
    public int CurrentStamina;
    public int CurrentCurse;   // ✅ 保留
    public int turnCount = 0;  // ✅ 保留

    // --- CTB 行动条系统 ---
    public float BaseAV { get; private set; }  // 跑完一圈所需的时间 (常数 / 速度)
    public float CurrentAV { get; set; }        // 距离下次出手还剩多少时间 (归零时行动)

    // --- 👇 新增: 成长状态 (Growth Stats) ---
    public int Level = 1;
    public int CurrentExp = 0;
    public int TalentPoints = 0; // 未分配的天赋点
    // 👇 新增: 钱包
    public int Gold = 0;

    public string lastKillerID = ""; // 记录最后给出致命一击的实体ID
    
    // 记录已分配的天赋点 (StatType -> 投入点数, 1点=1%)
    public Dictionary<StatType, int> allocatedTalents = new Dictionary<StatType, int>();

    // --- 装备槽位 ---
    public Dictionary<EquipmentSlot, EquipmentData> equipment = new Dictionary<EquipmentSlot, EquipmentData>();
    // 运行时耐久度记录表 (按槽位记录当前耐久)
    public Dictionary<EquipmentSlot, int> equipmentDurability = new Dictionary<EquipmentSlot, int>();

    // --- 构造函数 ---
    public RuntimeCharacter(CharacterData sourceData)
    {
        data = sourceData;
        
        // 初始化成长状态
        Level = 1;
        CurrentExp = 0;
        TalentPoints = 0;
        
        // 初始状态回满 (使用 MaxHP 属性，此时等级为 1)
        CurrentHP = MaxHP;
        CurrentMP = MaxMP;
        CurrentStamina = MaxStamina;
        turnCount = 0;
        
    }

    // --- 👇 核心：动态属性计算器 (Master Formula) ---
    // Formula: [(Base * LevelCurve) + Equip_Flat] * (1 + Equip_Percent) * (1 + Talent_Percent)
    
    private int GetStat(StatType type)
    {
        // 🛡️ 第一道防线: 如果 data 丢了，直接返回 0，别报错
        if (data == null) 
        {
            Debug.LogError("[RuntimeCharacter] 严重警告: CharacterData 引用丢失！");
            return 0;
        }
        // 1. 获取白值 (Base Value)
        float baseValue = 0;
        switch (type)
        {
            case StatType.MaxHP: baseValue = data.maxHP; break;
            case StatType.MaxMP: baseValue = data.maxMP; break;
            case StatType.MaxStamina: baseValue = data.maxStamina; break;
            case StatType.Attack: baseValue = data.attack; break;
            case StatType.Defense: baseValue = data.defense; break;
            case StatType.Speed: baseValue = data.speed; break;
        }

        // 2. 应用等级成长曲线
        if (type != StatType.MaxStamina)
        {
            float levelMult = (data.statGrowthCurve != null) ? data.statGrowthCurve.Evaluate(Level) : 1.0f;
            baseValue *= levelMult;
        }

        // 3. 计算装备加成
        float equipFlat = 0;
        float equipPercent = 0;

        foreach (var kvp in equipment)
        {
            EquipmentData item = kvp.Value;
            if (item != null)
            {
                // A. 基础面板
                if (type == StatType.Attack && item.slotType == EquipmentSlot.Weapon) equipFlat += item.baseDamage;
                if (type == StatType.Defense) equipFlat += item.baseDefense;
                if (type == StatType.MaxHP) equipFlat += item.baseMaxHP;
                if (type == StatType.MaxMP) equipFlat += item.baseMaxMP;

                // B. Modifiers
                foreach (var mod in item.modifiers)
                {
                    if (mod.statType == type)
                    {
                        if (mod.type == ModifierType.Flat) equipFlat += mod.value;
                        else if (mod.type == ModifierType.Percent) equipPercent += mod.value;
                    }
                }
            }
        }
        // 👇 新增：遍历提取特质提供的数值修饰
        float traitFlat = 0;
        float traitPercent = 0;
        foreach (var trait in traits)
        {
            if (trait.data != null && trait.level > 0 && trait.level <= trait.data.levels.Count)
            {
                TraitLevel levelData = trait.data.levels[trait.level - 1];
                foreach (var mod in levelData.modifiers)
                {
                    if (mod.statType == type)
                    {
                        if (mod.type == ModifierType.Flat) traitFlat += mod.value;
                        else if (mod.type == ModifierType.Percent) traitPercent += mod.value; // 例如黑死咒配置了 -10%，这里就会加上 -10
                    }
                }
            }
        }

        // 4. 基础计算: (白值 + 装备固定) * (1 + 装备%)
        float step1 = baseValue + equipFlat + traitFlat;
        float step2 = step1 * (1f + ((equipPercent + traitPercent) / 100f));

        // 5. 👇 修改核心: 天赋改为【固定值加算】 (除了暴击)
        // 1 天赋点 = +1 属性点
        int talentFlat = 0;
        if (allocatedTalents.ContainsKey(type))
        {
            talentFlat = allocatedTalents[type]; 
        }

        // 最终结果 = 基础属性 + 天赋点数
        float finalValue = step2 + talentFlat;

        // 计算 Buff 提供的临时属性加成
        // ==========================================
        int buffBonus = 0;
        foreach (var buff in activeBuffs)
        {
            if (type == StatType.Attack && buff.data.type == BuffType.StatBoost_Attack)
            {
                buffBonus += buff.dynamicValue;
            }
            else if (type == StatType.Defense && buff.data.type == BuffType.StatBoost_Defense)
            {
                buffBonus += buff.dynamicValue;
            }
            // (未来如果需要加 MaxHP 或 Speed 的 Buff，直接在这里加 else if 即可)
        }
        finalValue += buffBonus; // 把 Buff 加成算进最终结果
        // ==========================================
        // 👇 新增：怪物难度乘区 (在一切算完之后，如果是怪物，套上最终乘法)
        // ==========================================
        if (data.team == TeamType.Enemy && GameManager.Instance != null)
        {
            float difficultyMult = 1.0f;
            switch (GameManager.Instance.currentDifficulty)
            {
                case GameDifficulty.Story:  if (type == StatType.MaxHP || type == StatType.Attack) difficultyMult = 0.7f; break;
                case GameDifficulty.Abyss:  
                    if (type == StatType.MaxHP || type == StatType.Attack) difficultyMult = 1.5f; 
                    else if (type == StatType.Speed) difficultyMult = 1.2f;
                    break;
            }
            finalValue *= difficultyMult;
        }
        
        return Mathf.RoundToInt(Mathf.Max(0, finalValue));
    }

    // --- 👇 新增: 运行时 Buff 记录 ---
    public class ActiveBuff
    {
        public BuffData data;
        public int remainingTurns;
        public int dynamicValue; // 动态计算出来的最终数值 (比如破盾前剩余的护盾量)
    }

    public List<ActiveBuff> activeBuffs = new List<ActiveBuff>();
    // --- 👇 新增: 被动特质系统 (Traits) ---
    public class ActiveTrait
    {
        public TraitData data;
        public int level;
        public int remainingDays; // 👇 新增：剩余天数 (-1代表永久)
    }
    public List<ActiveTrait> traits = new List<ActiveTrait>();
    public int CurrentShield = 0; // 当前护盾总量

    // 公开属性访问器
    public int MaxHP => GetStat(StatType.MaxHP);
    public int MaxMP => GetStat(StatType.MaxMP);
    public int MaxStamina => GetStat(StatType.MaxStamina); 
    public int Attack => GetStat(StatType.Attack);
    public int Defense => GetStat(StatType.Defense);
    public int Speed => GetStat(StatType.Speed);

    // 暴击率 (1点 = 1%)
    public float CritRate
    {
        get
        {
            float total = data.baseCritRate;
            
            // 装备
            foreach (var kvp in equipment)
            {
                if (kvp.Value == null) continue;
                foreach (var mod in kvp.Value.modifiers)
                {
                    if (mod.statType == StatType.CritRate) total += mod.value / 100f; 
                }
            }
            
            // 天赋 (1点 = 1% = 0.01)
            if (allocatedTalents.ContainsKey(StatType.CritRate))
                total += allocatedTalents[StatType.CritRate] / 100f; // 👈 保持除以100
                
            return total;
        }
    }

    // 暴击伤害 (1点 = 1%)
    public float CritDamage
    {
        get
        {
            float total = data.baseCritDamage;
            
            foreach (var kvp in equipment)
            {
                if (kvp.Value == null) continue;
                foreach (var mod in kvp.Value.modifiers)
                {
                    if (mod.statType == StatType.CritDamage) total += mod.value / 100f;
                }
            }
            
            if (allocatedTalents.ContainsKey(StatType.CritDamage))
                total += allocatedTalents[StatType.CritDamage] / 100f; // 👈 保持除以100

            return total;
        }
    }

    // --- 👇 升级逻辑 (Level Up) ---
    
    // 获取下一级所需总经验
    public int GetExpForLevel(int level)
    {
        if (level > data.maxLevel) return int.MaxValue;
        // 使用 AnimationCurve 读取总经验需求
        return Mathf.RoundToInt(data.expCurve.Evaluate(level));
    }

    public void GainExp(int amount)
    {
        int startLevel = Level;
        if (Level >= data.maxLevel) return;

        CurrentExp += amount;
        Debug.Log($"[Growth] {Name} 获得经验: {amount}, 当前总经验: {CurrentExp}");

        // 检查升级 (循环支持一次升多级)
        // 下一级所需经验 > 当前经验 ?
        while (Level < data.maxLevel && CurrentExp >= GetExpForLevel(Level + 1))
        {
            LevelUp();
        }

        // 👇 核心修复：移除“仅限主角”的判定！只要升级了，全员皆可播报！
        if (Level > startLevel && UI_SystemToast.Instance != null)
        {
            int levelsGained = Level - startLevel;
            
            // 文本拼接：带上角色真名
            string prefix = levelsGained > 1 ? $"【{Name}】连升多级！当前: Lv." : $"【{Name}】升级啦！当前: Lv.";
            
            // 聚合码拼接：用角色的专属 ID 作为 mergeID，防止不同角色同时升级时提示框互相覆盖
            string uniqueMergeID = $"LevelUp_{data.characterID}";
            
            // 呼出带头像的播报
            UI_SystemToast.Instance.Show(uniqueMergeID, $"{prefix}{Level}", 0, data.portrait);
        }
    }

    // --- 👇 新增: Buff 管理 ---
    
    // 给自己挂载一个 Buff
    public void ApplyBuff(BuffData buffData, RuntimeCharacter caster)
    {
        if (buffData == null) return;

        // 1. 计算这个 Buff 的动态数值 (比如 50 + 施法者最大生命值的 10%)
        float statValue = 0;
        switch (buffData.scalingStat)
        {
            case ScalingStat.Attack: statValue = caster.Attack; break;
            case ScalingStat.Defense: statValue = caster.Defense; break;
            case ScalingStat.MaxHP: statValue = caster.MaxHP; break;
            case ScalingStat.CurrentHP: statValue = caster.CurrentHP; break;
            case ScalingStat.MaxMP: statValue = caster.MaxMP; break;
            case ScalingStat.CurrentMP: statValue = caster.CurrentMP; break;
            case ScalingStat.Speed: statValue = caster.Speed; break;
        }
        int calculatedValue = Mathf.RoundToInt(buffData.baseValue + (statValue * buffData.scalingMultiplier));

        // 2. 如果是护盾，直接加到当前护盾池里
        if (buffData.type == BuffType.Shield)
        {
            CurrentShield += calculatedValue;
            Debug.Log($"[Buff] {Name} 获得了 {calculatedValue} 点护盾！当前总护盾: {CurrentShield}");
        }

        // 3. 记录到列表里 (用于追踪回合数和免伤状态)
        activeBuffs.Add(new ActiveBuff 
        { 
            data = buffData, 
            remainingTurns = buffData.durationTurns,
            dynamicValue = calculatedValue
        });
        
        if (UIManager.Instance != null) UIManager.Instance.RefreshPlayerStatus();
    }

    // 回合结束时调用，扣减 Buff 持续时间
    public void TickBuffs()
    {
        for (int i = activeBuffs.Count - 1; i >= 0; i--)
        {
            activeBuffs[i].remainingTurns--;
            if (activeBuffs[i].remainingTurns <= 0)
            {
                // 如果是护盾到期了，要从护盾池里扣除残余量
                if (activeBuffs[i].data.type == BuffType.Shield && activeBuffs[i].dynamicValue > 0)
                {
                    CurrentShield -= activeBuffs[i].dynamicValue;
                    if (CurrentShield < 0) CurrentShield = 0;
                }
                
                Debug.Log($"[Buff] {activeBuffs[i].data.buffName} 效果结束。");
                activeBuffs.RemoveAt(i);
            }
        }
    }
    // UI 显示专用属性 (区间经验逻辑) ---
    
    // A. 当前等级的起始门槛 (例如 Lv2 是 139)
    public int ExpBase => GetExpForLevel(Level);

    // B. 下一级所需的总门槛 (例如 Lv3 是 378 = 139 + 239)
    public int ExpTotalTarget => GetExpForLevel(Level + 1);

    // C. 分子: 当前等级内的进度 (200 - 139 = 61)
    public int CurrentLevelProgress 
    {
        get 
        {
            if (Level >= data.maxLevel) return 1; 
            return CurrentExp - ExpBase;
        }
    }

    // D. 分母: 这一级需要填多少坑才能升级 (378 - 139 = 239)
    // 这就是您说的 "239"，由曲线斜率控制，越往后越大
    public int ExpRequiredForLevelUp
    {
        get
        {
            if (Level >= data.maxLevel) return 1; 
            return ExpTotalTarget - ExpBase;
        }
    }

    private void LevelUp()
    {
        Level++;
        TalentPoints++; // 获得天赋点
        
        // 升级奖励：回满状态 (可选，很爽)
        CurrentHP = MaxHP;
        CurrentMP = MaxMP;
        
        Debug.Log($"🎉 LEVEL UP! 当前等级: {Level} | 获得 1 天赋点!");
        // 如果有 UI Manager，可以在这里发通知
        if (UIManager.Instance != null) UIManager.Instance.RefreshPlayerStatus();
    }

    // 消耗天赋点
    public void SpendTalent(StatType type)
    {
        if (TalentPoints > 0)
        {
            TalentPoints--;
            
            if (allocatedTalents.ContainsKey(type))
                allocatedTalents[type]++;
            else
                allocatedTalents.Add(type, 1);
                
            Debug.Log($"[Growth] 天赋加点: {type} +1% | 剩余点数: {TalentPoints}");
            
            // 刷新界面
            if (UIManager.Instance != null) UIManager.Instance.RefreshPlayerStatus();
        }
    }

    // --- 装备操作 (保持原有) ---
    public void Equip(EquipmentData newItem)
    {
        if (newItem == null) return;
        if (equipment.ContainsKey(newItem.slotType)) equipment[newItem.slotType] = newItem;
        else equipment.Add(newItem.slotType, newItem);
        Debug.Log($"[Runtime] 装备了 {newItem.itemName} | 当前攻击力: {Attack}");
    }
    
    public EquipmentData Unequip(EquipmentSlot slot)
    {
        if (equipment.ContainsKey(slot)) { var old = equipment[slot]; equipment.Remove(slot); return old; }
        return null;
    }

    public bool SetDurability(EquipmentSlot slot, int value)
    {
        if (equipment != null && equipment.TryGetValue(slot, out EquipmentData equip) && equip != null)
        {
            if (equip.maxDurability <= 0) return false;

            int oldValue = equipmentDurability.ContainsKey(slot) ? equipmentDurability[slot] : equip.maxDurability;
            int newValue = Mathf.Clamp(value, 0, equip.maxDurability);
            equipmentDurability[slot] = newValue;

            Debug.Log($"[{Name}] 的 {equip.itemName} ({slot}) 耐久度变更为: {newValue}/{equip.maxDurability}");

            if (newValue == 0 && oldValue > 0)
            {
                Unequip(slot);
                return true;
            }
        }
        return false;
    }

    // --- 👇 完整的既有逻辑 (TakeDamage, Mana, Stamina) ---
    
    public int TakeDamage(int rawDamage, string sourceID = "")
    {
        // 1. 计算免伤 (Damage Reduction)
        float totalDR = 0f;
        foreach (var buff in activeBuffs)
        {
            if (buff.data.type == BuffType.DamageReduction)
            {
                // dynamicValue 此时可能存的是 30 (代表 30%)
                totalDR += (buff.dynamicValue / 100f); 
            }
        }
        // 免伤最高封顶 90%，防止打出 0 或负数伤害
        totalDR = Mathf.Clamp(totalDR, 0f, 0.9f); 
        int damageAfterDR = Mathf.RoundToInt(rawDamage * (1f - totalDR));

        int actualHPLost = 0;

        // 2. 护盾抵挡 (优先扣盾)
        if (CurrentShield > 0)
        {
            if (CurrentShield >= damageAfterDR)
            {
                // 盾够厚，没破盾
                CurrentShield -= damageAfterDR;
                
                // 顺便从 Buff 记录里扣除对应护盾的余量 (简单处理：优先扣除第一个找到的盾)
                foreach (var buff in activeBuffs)
                {
                    if (buff.data.type == BuffType.Shield && buff.dynamicValue > 0)
                    {
                        buff.dynamicValue -= damageAfterDR;
                        break;
                    }
                }
                
                damageAfterDR = 0; // 伤害被完全吸收
            }
            else
            {
                // 盾破了，溢出的伤害继续打血
                damageAfterDR -= CurrentShield;
                CurrentShield = 0;
                
                // 清空所有护盾 Buff 的余量
                foreach (var buff in activeBuffs) 
                    if (buff.data.type == BuffType.Shield) buff.dynamicValue = 0;
            }
        }

        // 3. 扣除真实血量
        if (damageAfterDR > 0)
        {
            bool wasAlive = CurrentHP > 0;
            CurrentHP -= damageAfterDR;
            actualHPLost = damageAfterDR;
            if (CurrentHP < 0) CurrentHP = 0;
            if (CurrentHP <= 0 && wasAlive) lastKillerID = sourceID ?? "";
        }

        // 4. UI 与日志刷新
        if (UIManager.Instance != null) UIManager.Instance.RefreshPlayerStatus();
        
        string logMsg = $"{Name} 受到了攻击！";
        if (totalDR > 0) logMsg += $" (免伤抵消了 {rawDamage - Mathf.RoundToInt(rawDamage * (1f - totalDR))} 点)";
        if (rawDamage > 0 && actualHPLost == 0) logMsg += $" [护盾完全吸收！]";
        else logMsg += $" 实际扣血: {actualHPLost}。当前 HP: {CurrentHP}/{MaxHP} (护盾: {CurrentShield})";
        
        Debug.Log(logMsg);

        // 👇 2. 在方法最后加上这行，把真实扣血量汇报出去
        return actualHPLost;
    }

    public void ConsumeMana(int amount)
    {
        if (CurrentMP >= amount)
        {
            CurrentMP -= amount;
        }
        if (UIManager.Instance != null) UIManager.Instance.RefreshPlayerStatus();
    }

    public void ConsumeStamina(int amount)
    {
        CurrentStamina -= amount;
        if (CurrentStamina < 0) CurrentStamina = 0;
        if (UIManager.Instance != null) UIManager.Instance.RefreshPlayerStatus();
    }
    
    public void RestoreStats()
    {
        CurrentHP = MaxHP; 
        CurrentMP = MaxMP; 
        CurrentStamina = MaxStamina; 
        Debug.Log($"{Name} 状态已完全恢复。");
        if (UIManager.Instance != null) UIManager.Instance.RefreshPlayerStatus();
    }

    // 战斗开始时，或者速度发生永久性改变时调用
    public void InitializeAV()
    {
        // 安全锁：防止除数为 0 导致游戏崩溃
        float safeSpeed = Mathf.Max(1f, Speed); 
        
        // 核心公式：10000 是标准跑道长度。速度 100 的人，跑一圈需要 100 的时间。
        BaseAV = 10000f / safeSpeed; 
        
        // 刚进入战斗时，大家都在起跑线上，剩余时间等于跑完一圈的时间
        CurrentAV = BaseAV; 
    }
    
    // 行动结束后，重新回到起跑线
    public void ResetAVAfterTurn()
    {
        CurrentAV = BaseAV;
    }

    // --- 👇 新增: 获得或升级特质 ---
    public void AddTrait(TraitData newTrait, int levelsToAdd = 1)
    {
        if (newTrait == null) return;
        
        ActiveTrait existing = traits.Find(t => t.data == newTrait);
        if (existing != null)
        {
            if (existing.data.isLevelable && existing.level < existing.data.maxLevel)
            {
                existing.level += levelsToAdd;
                if (existing.level > existing.data.maxLevel) existing.level = existing.data.maxLevel;
                Debug.Log($"[Trait] {Name} 的特质 [{newTrait.traitName}] 恶化/升级到了 Lv.{existing.level}!");
                
                ExecuteTraitPlugins(existing);
            }
        }
        else
        {
            // 👇 补上 remainingDays 的赋值，-1 代表永久，否则读取配置表的天数
            ActiveTrait t = new ActiveTrait { 
                data = newTrait, 
                level = Mathf.Clamp(levelsToAdd, 1, newTrait.maxLevel),
                remainingDays = newTrait.isPermanent ? -1 : newTrait.durationDays 
            };
            traits.Add(t);
            Debug.Log($"[Trait] {Name} 获得了新特质 [{newTrait.traitName}] Lv.{t.level}!");
            
            ExecuteTraitPlugins(t);
        }
        
        // 属性上限可能发生变化，强制矫正当前血量
        if (CurrentHP > MaxHP) CurrentHP = MaxHP;
        if (UIManager.Instance != null) UIManager.Instance.RefreshPlayerStatus();
        if (BattleManager.Instance != null) BattleManager.Instance.UpdateStatsUI();
    }
    // --- 👇 新增: 跨天结算特质期限 ---
    public void TickTraits(int daysPassed = 1)
    {
        bool hasChanges = false;
        
        // 倒序遍历，方便安全删除过期特质
        for (int i = traits.Count - 1; i >= 0; i--)
        {
            ActiveTrait t = traits[i];
            
            // 如果不是永久特质，且剩余天数 > 0
            if (t.data != null && !t.data.isPermanent && t.remainingDays > 0)
            {
                t.remainingDays -= daysPassed;
                
                if (t.remainingDays <= 0)
                {
                    Debug.Log($"[Trait] 经过时间的流逝，特质 [{t.data.traitName}] 已消散！");
                    traits.RemoveAt(i);
                    hasChanges = true;
                }
            }
        }

        // 如果有特质过期消失了，属性上限必定发生变化，需要全盘刷新 UI
        if (hasChanges)
        {
            if (CurrentHP > MaxHP) CurrentHP = MaxHP;
            if (UIManager.Instance != null) UIManager.Instance.RefreshPlayerStatus();
            if (BattleManager.Instance != null) BattleManager.Instance.UpdateStatsUI();
        }
    }

    // 处理特殊的机制标签 (例如黑死咒满级暴毙)
    private void ExecuteTraitPlugins(ActiveTrait trait)
    {
        if (trait.data == null || trait.level <= 0 || trait.level > trait.data.levels.Count) return;
        TraitLevel currentLevelData = trait.data.levels[trait.level - 1];
        
        // 遍历并执行所有挂载在这个层级的“特质插件”！
        foreach (var plugin in currentLevelData.specialEffects)
        {
            if (plugin != null) plugin.OnTraitAdded(this, trait.level);
        }
    }
}