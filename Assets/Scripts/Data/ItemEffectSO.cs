using UnityEngine;
using System.Collections.Generic;

/// 效果触发时机
public enum EffectTiming
{
    Immediate,  // 即时：打出时立刻生效
    Delayed,    // 延时：下回合开始补牌恢复点数时生效
    Wave,       // 波次：下一波出牌结算后生效
}

/// <summary>
/// 卡牌效果的抽象基类（策略模式）。
/// 每个具体效果是一个子类 ScriptableObject，重写 Apply() 实现效果。
///
/// timing 决定何时触发：
///   Immediate → 打出时立刻调 Apply()
///   Delayed  → 挂账，下回合 GameLoop Draw 阶段统一执行
///   Wave     → 挂账，下一波 TryResolveOneHand 结算后执行
/// </summary>
public abstract class AbilityEffectSO : ScriptableObject
{
    public EffectTiming timing = EffectTiming.Immediate;

    [Header("铃铛图标（自己画，拖 Sprite 进来；空 = 卡牌上不显示标记）")]
    public Sprite bellIcon;
    [TextArea] public string description;
    public abstract void Apply();

    // 带"打出这张牌的点数"的重载：计分时卡牌会把点数传进来
    // 只有需要知道自己是被哪张牌打出的效果，才重写它；默认转调无参 Apply()，老效果零影响
    public virtual void Apply(int cardRank) { Apply(); }
}
