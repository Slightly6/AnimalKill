using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 小丑（海克斯强化）基类：获得后整局常驻的被动道具。
/// 和卡牌 Ability 的区别：不挂在任何手牌上，监听的是全局弃牌/出牌。
/// 子类只重写自己关心的钩子，不关心的用基类默认空实现。
/// </summary>
public enum HookEffectType
{
    DrawCard,      // 抽牌
    GainChips,     // 获得筹码
    DealDamage,    // 造成伤害
    Heal,          // 治疗
    Buff,          // 增益
    Debuff         // 减益
}
public enum Rarity
{
    Common,// 普通
    Rare,// 稀有
    Epic,// 史诗
    Legendary// 传奇
}
public abstract class JokerSO : ScriptableObject
{
    public string jokerName;
    public Texture2D faceTexture;
    [TextArea] public string description;
    public HookEffectType effectType; // 效果类型枚举
    public Rarity rarity;      // 稀有度
    public int price;          // 价格  

    [System.NonSerialized] public Transform view;       // 运行时实体（商店里的小丑物体），演出拿位置用

    public virtual void OnAcquire() { }                 // 获得时
    public virtual void OnRemove() { }                  // 移除时
    public virtual void OnNewLevel() { }                // 换关（清战斗内状态）
    public virtual void OnHandPlayed() { }              // 每手打出结算前（演出开始前调，加成进本手伤害一起滚分）
}