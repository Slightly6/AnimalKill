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
    public abstract void Apply();
}
