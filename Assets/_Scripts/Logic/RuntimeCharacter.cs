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
    public float BaseAV { get; private set; }  
    public float CurrentAV { get; set; }        

    // --- 成长状态 ---
    public int Level = 1;
    public int CurrentExp = 0;
    public int TalentPoints = 0; 
    public int Gold = 0;

    public string lastKillerID = ""; 
    
    public Dictionary<StatType, int> allocatedTalents = new Dictionary<StatType, int>();

    // 👇 核心脱壳 1：装备字典的 Value 彻底换成 RuntimeEquipment (实体肉身)
    public Dictionary<EquipmentSlot, RuntimeEquipment> equipment = new Dictionary<EquipmentSlot, RuntimeEquipment>();
    
    // ⚠️ 注意：equipmentDurability 字典已被废弃！因为 RuntimeEquipment 内部自带了 currentDurability。

    // --- 构造函数 ---
    public RuntimeCharacter(CharacterData sourceData)
    {
        data = sourceData;
        Level = 1;
        CurrentExp = 0;
        TalentPoints = 0;
        
        CurrentHP = MaxHP;
        CurrentMP = MaxMP;
        CurrentStamina = MaxStamina;
        turnCount = 0;
    }

    // --- 👇 核心脱壳 2：重写动态属性计算器 ---
    private int GetStat(StatType type)
    {
        if (data == null) 
        {
            Debug.LogError("[RuntimeCharacter] 严重警告: CharacterData 引用丢失！");
            return 0;
        }

        // 1. 获取白值
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

        // 3. 计算装备加成 (读取肉身数据)
        float equipFlat = 0;
        float equipPercent = 0;

        foreach (var kvp in equipment)
        {
            RuntimeEquipment item = kvp.Value;
            if (item != null)
            {
                // A. 基础面板 (读取锻造升维后的动态数值)
                if (type == StatType.Attack && item.blueprint.slotType == EquipmentSlot.Weapon) equipFlat += item.DynamicDamage;
                if (type == StatType.Defense) equipFlat += item.DynamicDefense;
                if (type == StatType.MaxHP) equipFlat += item.DynamicMaxHP;
                if (type == StatType.MaxMP) equipFlat += item.DynamicMaxMP;

                //  B. 补回：读取装备的【固有死属性】 (Innate Modifiers)
                foreach (var mod in item.blueprint.modifiers)
                {
                    if (mod.statType == type)
                    {
                        if (mod.type == ModifierType.Flat) equipFlat += mod.value;
                        else if (mod.type == ModifierType.Percent) equipPercent += mod.value;
                    }
                }

                // C. 随机词条 (Affixes)
                foreach (var affix in item.affixes)
                {
                    if (affix.statType == type)
                    {
                        if (!affix.isPercent) equipFlat += affix.value;
                        else equipPercent += affix.value;
                    }
                }
            }
        }

        // 遍历提取特质提供的数值修饰
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
                        else if (mod.type == ModifierType.Percent) traitPercent += mod.value; 
                    }
                }
            }
        }

        // 4. 基础计算
        float step1 = baseValue + equipFlat + traitFlat;
        float step2 = step1 * (1f + ((equipPercent + traitPercent) / 100f));

        // 5. 天赋 (👇 核心重构：引入属性权重汇率)
        int talentFlat = 0;
        if (allocatedTalents.ContainsKey(type)) 
        {
            int investedPoints = allocatedTalents[type];
            // 定义不同属性的转化权重
            switch (type)
            {
                case StatType.MaxHP: talentFlat = investedPoints * 10; break;     // 1 点天赋 = 10 HP
                case StatType.MaxMP: talentFlat = investedPoints * 5; break;      // 1 点天赋 = 5 MP
                case StatType.MaxStamina: talentFlat = investedPoints * 5; break; // 1 点天赋 = 5 Stamina
                case StatType.Attack: talentFlat = investedPoints * 2; break;     // 1 点天赋 = 2 攻击力
                case StatType.Defense: talentFlat = investedPoints * 2; break;    // 1 点天赋 = 2 防御力
                case StatType.Speed: talentFlat = investedPoints * 1; break;      // 1 点天赋 = 1 速度 (速度收益高，保持1:1)
                default: talentFlat = investedPoints; break;
            }
        }
        float finalValue = step2 + talentFlat;
        // Buff
        int buffBonus = 0;
        foreach (var buff in activeBuffs)
        {
            if (type == StatType.Attack && buff.data.type == BuffType.StatBoost_Attack) buffBonus += buff.dynamicValue;
            else if (type == StatType.Defense && buff.data.type == BuffType.StatBoost_Defense) buffBonus += buff.dynamicValue;
        }
        finalValue += buffBonus; 

        // 难度乘区
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

    // --- Buff 与 特质 ---
    public class ActiveBuff
    {
        public BuffData data;
        public int remainingTurns;
        public int dynamicValue; 
    }
    public List<ActiveBuff> activeBuffs = new List<ActiveBuff>();

    public class ActiveTrait
    {
        public TraitData data;
        public int level;
        public int remainingDays; 
    }
    public List<ActiveTrait> traits = new List<ActiveTrait>();
    public int CurrentShield = 0; 

    // 公开属性访问器
    public int MaxHP => GetStat(StatType.MaxHP);
    public int MaxMP => GetStat(StatType.MaxMP);
    public int MaxStamina => GetStat(StatType.MaxStamina); 
    public int Attack => GetStat(StatType.Attack);
    public int Defense => GetStat(StatType.Defense);
    public int Speed => GetStat(StatType.Speed);

    // 👇 核心脱壳 3：暴击与爆伤读取肉身的词条池 (Affixes)
    public float CritRate
    {
        get
        {
            float total = data.baseCritRate;
            
            foreach (var kvp in equipment)
            {
                if (kvp.Value == null) continue;
                
                // 1. 读取固有死属性 (蓝图)
                foreach (var mod in kvp.Value.blueprint.modifiers)
                {
                    if (mod.statType == StatType.CritRate) total += mod.value / 100f; 
                }

                // 2. 读取神锻随机词条 (肉身)
                foreach (var affix in kvp.Value.affixes)
                {
                    if (affix.statType == StatType.CritRate) total += affix.value / 100f; 
                }
            }
            
            // 3. 天赋加成 (1点天赋 = 0.5% 暴击率)
            if (allocatedTalents.ContainsKey(StatType.CritRate)) 
                total += (allocatedTalents[StatType.CritRate] * 0.5f) / 100f; 
                
            return total; // 👈 必须有返回值
        }
    }

    public float CritDamage
    {
        get
        {
            float total = data.baseCritDamage;
            
            foreach (var kvp in equipment)
            {
                if (kvp.Value == null) continue;
                
                // 1. 读取固有死属性 (蓝图)
                foreach (var mod in kvp.Value.blueprint.modifiers)
                {
                    if (mod.statType == StatType.CritDamage) total += mod.value / 100f;
                }

                // 2. 读取神锻随机词条 (肉身)
                foreach (var affix in kvp.Value.affixes)
                {
                    if (affix.statType == StatType.CritDamage) total += affix.value / 100f;
                }
            }
            
            // 3. 天赋加成 (1点天赋 = 2.0% 暴击伤害)
            if (allocatedTalents.ContainsKey(StatType.CritDamage)) 
                total += (allocatedTalents[StatType.CritDamage] * 2.0f) / 100f; 
                
            return total; // 👈 必须有返回值
        }
    }

    // --- 升级逻辑 ---
    public int GetExpForLevel(int level) { return level > data.maxLevel ? int.MaxValue : Mathf.RoundToInt(data.expCurve.Evaluate(level)); }

    public void GainExp(int amount)
    {
        int startLevel = Level;
        if (Level >= data.maxLevel) return;

        CurrentExp += amount;
        Debug.Log($"[Growth] {Name} 获得经验: {amount}, 当前总经验: {CurrentExp}");

        while (Level < data.maxLevel && CurrentExp >= GetExpForLevel(Level + 1)) LevelUp();

        if (Level > startLevel && UI_SystemToast.Instance != null)
        {
            int levelsGained = Level - startLevel;
            string prefix = levelsGained > 1 ? $"【{Name}】连升多级！当前: Lv." : $"【{Name}】升级啦！当前: Lv.";
            string uniqueMergeID = $"LevelUp_{data.characterID}";
            UI_SystemToast.Instance.Show(uniqueMergeID, $"{prefix}{Level}", 0, data.portrait);
        }
    }

    // Buff 与 区间经验逻辑省略展开 (原样保留)...
    public void ApplyBuff(BuffData buffData, RuntimeCharacter caster)
    {
        if (buffData == null) return;
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

        if (buffData.type == BuffType.Shield) { CurrentShield += calculatedValue; }

        activeBuffs.Add(new ActiveBuff { data = buffData, remainingTurns = buffData.durationTurns, dynamicValue = calculatedValue });
        if (UIManager.Instance != null) UIManager.Instance.RefreshPlayerStatus();
    }

    public void TickBuffs()
    {
        for (int i = activeBuffs.Count - 1; i >= 0; i--)
        {
            activeBuffs[i].remainingTurns--;
            if (activeBuffs[i].remainingTurns <= 0)
            {
                if (activeBuffs[i].data.type == BuffType.Shield && activeBuffs[i].dynamicValue > 0)
                {
                    CurrentShield -= activeBuffs[i].dynamicValue;
                    if (CurrentShield < 0) CurrentShield = 0;
                }
                activeBuffs.RemoveAt(i);
            }
        }
    }

    public int ExpBase => GetExpForLevel(Level);
    public int ExpTotalTarget => GetExpForLevel(Level + 1);
    public int CurrentLevelProgress { get { if (Level >= data.maxLevel) return 1; return CurrentExp - ExpBase; } }
    public int ExpRequiredForLevelUp { get { if (Level >= data.maxLevel) return 1; return ExpTotalTarget - ExpBase; } }

    private void LevelUp()
    {
        Level++;
        
        TalentPoints++; 
        CurrentHP = MaxHP;
        CurrentMP = MaxMP;
        if (UIManager.Instance != null) UIManager.Instance.RefreshPlayerStatus();
    }

    public void SpendTalent(StatType type)
    {
        if (TalentPoints > 0)
        {
            TalentPoints--;
            if (allocatedTalents.ContainsKey(type)) allocatedTalents[type]++;
            else allocatedTalents.Add(type, 1);
            if (UIManager.Instance != null) UIManager.Instance.RefreshPlayerStatus();
        }
    }

    // --- 👇 核心脱壳 4：穿脱逻辑接管肉身 ---
    public void Equip(RuntimeEquipment newEquip)
    {
        if (newEquip == null) return;
        EquipmentSlot slot = newEquip.blueprint.slotType;
        if (equipment.ContainsKey(slot)) equipment[slot] = newEquip;
        else equipment.Add(slot, newEquip);
        Debug.Log($"[Runtime] 装备了 {newEquip.blueprint.itemName}({newEquip.rarity}) | 当前攻击力: {Attack}");
    }
    
    public RuntimeEquipment Unequip(EquipmentSlot slot)
    {
        if (equipment.ContainsKey(slot)) { var old = equipment[slot]; equipment.Remove(slot); return old; }
        return null;
    }

    public bool SetDurability(EquipmentSlot slot, int value)
    {
        if (equipment != null && equipment.TryGetValue(slot, out RuntimeEquipment equip) && equip != null)
        {
            if (equip.blueprint.maxDurability <= 0) return false;

            int oldValue = equip.currentDurability;
            int newValue = Mathf.Clamp(value, 0, equip.blueprint.maxDurability);
            equip.currentDurability = newValue;

            Debug.Log($"[{Name}] 的 {equip.blueprint.itemName} ({slot}) 耐久度变更为: {newValue}/{equip.blueprint.maxDurability}");

            if (newValue == 0 && oldValue > 0)
            {
                Unequip(slot);
                return true; // 爆衣！
            }
        }
        return false;
    }

    // 战斗与状态逻辑... (原样保留)
    public int TakeDamage(int rawDamage, string sourceID = "")
    {
        float totalDR = 0f;
        foreach (var buff in activeBuffs) { if (buff.data.type == BuffType.DamageReduction) totalDR += (buff.dynamicValue / 100f); }
        totalDR = Mathf.Clamp(totalDR, 0f, 0.9f); 
        int damageAfterDR = Mathf.RoundToInt(rawDamage * (1f - totalDR));

        int actualHPLost = 0;

        if (CurrentShield > 0)
        {
            if (CurrentShield >= damageAfterDR)
            {
                CurrentShield -= damageAfterDR;
                foreach (var buff in activeBuffs)
                {
                    if (buff.data.type == BuffType.Shield && buff.dynamicValue > 0) { buff.dynamicValue -= damageAfterDR; break; }
                }
                damageAfterDR = 0; 
            }
            else
            {
                damageAfterDR -= CurrentShield;
                CurrentShield = 0;
                foreach (var buff in activeBuffs) if (buff.data.type == BuffType.Shield) buff.dynamicValue = 0;
            }
        }

        if (damageAfterDR > 0)
        {
            bool wasAlive = CurrentHP > 0;
            CurrentHP -= damageAfterDR;
            actualHPLost = damageAfterDR;
            if (CurrentHP < 0) CurrentHP = 0;
            if (CurrentHP <= 0 && wasAlive) lastKillerID = sourceID ?? "";
        }

        if (UIManager.Instance != null) UIManager.Instance.RefreshPlayerStatus();
        return actualHPLost;
    }

    public void ConsumeMana(int amount) { if (CurrentMP >= amount) CurrentMP -= amount; if (UIManager.Instance != null) UIManager.Instance.RefreshPlayerStatus(); }
    public void ConsumeStamina(int amount) { CurrentStamina -= amount; if (CurrentStamina < 0) CurrentStamina = 0; if (UIManager.Instance != null) UIManager.Instance.RefreshPlayerStatus(); }
    public void RestoreStats() { CurrentHP = MaxHP; CurrentMP = MaxMP; CurrentStamina = MaxStamina; if (UIManager.Instance != null) UIManager.Instance.RefreshPlayerStatus(); }
    public void InitializeAV() { float safeSpeed = Mathf.Max(1f, Speed); BaseAV = 10000f / safeSpeed; CurrentAV = BaseAV; }
    public void ResetAVAfterTurn() { CurrentAV = BaseAV; }

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
                ExecuteTraitPlugins(existing);
            }
        }
        else
        {
            ActiveTrait t = new ActiveTrait { 
                data = newTrait, level = Mathf.Clamp(levelsToAdd, 1, newTrait.maxLevel),
                remainingDays = newTrait.isPermanent ? -1 : newTrait.durationDays 
            };
            traits.Add(t);
            ExecuteTraitPlugins(t);
        }
        if (CurrentHP > MaxHP) CurrentHP = MaxHP;
        if (UIManager.Instance != null) UIManager.Instance.RefreshPlayerStatus();
        if (BattleManager.Instance != null) BattleManager.Instance.UpdateStatsUI();
    }

    public void TickTraits(int daysPassed = 1)
    {
        bool hasChanges = false;
        for (int i = traits.Count - 1; i >= 0; i--)
        {
            ActiveTrait t = traits[i];
            if (t.data != null && !t.data.isPermanent && t.remainingDays > 0)
            {
                t.remainingDays -= daysPassed;
                if (t.remainingDays <= 0) { traits.RemoveAt(i); hasChanges = true; }
            }
        }
        if (hasChanges)
        {
            if (CurrentHP > MaxHP) CurrentHP = MaxHP;
            if (UIManager.Instance != null) UIManager.Instance.RefreshPlayerStatus();
            if (BattleManager.Instance != null) BattleManager.Instance.UpdateStatsUI();
        }
    }

    private void ExecuteTraitPlugins(ActiveTrait trait)
    {
        if (trait.data == null || trait.level <= 0 || trait.level > trait.data.levels.Count) return;
        TraitLevel currentLevelData = trait.data.levels[trait.level - 1];
        foreach (var plugin in currentLevelData.specialEffects) if (plugin != null) plugin.OnTraitAdded(this, trait.level);
    }
}