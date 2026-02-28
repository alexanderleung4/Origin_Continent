using UnityEngine;

// 这是一个抽象基类，所有的特殊被动效果都要继承它
public abstract class TraitEffectBase : ScriptableObject
{
    // 当特质被赋予或升级时触发
    public virtual void OnTraitAdded(RuntimeCharacter target, int currentLevel) { }
    
    // 预留接口：当回合开始时触发（例如每回合回血被动）
    public virtual void OnTurnStart(RuntimeCharacter target) { }
    
    // 预留接口：当造成伤害时触发（例如吸血被动）
    public virtual void OnDealDamage(RuntimeCharacter attacker, RuntimeCharacter defender, ref int damage) { }
}