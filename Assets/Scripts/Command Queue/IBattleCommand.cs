using System;

/// <summary>
/// 一个排队动作。做完时必须调用 onComplete，队列才会放行下一个。
/// 铁律：onComplete 只能调一次。
/// </summary>
public interface IBattleCommand
{
    void Execute(Action onComplete);
}