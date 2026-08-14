using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局事件总线。模块之间发消息用，不用互相引用。
///
/// 怎么用：
///   发事件：EventBus.Publish(new CardDiedEvent { card = xxx });
///   听事件：EventBus.Subscribe<CardDiedEvent>(OnCardDied);
///   取消听：EventBus.Unsubscribe<CardDiedEvent>(OnCardDied);
/// </summary>
public static class EventBus
{
    // 存所有事件的处理器
    private static Dictionary<Type, Delegate> handlers = new Dictionary<Type, Delegate>();

    // 订阅事件
    public static void Subscribe<T>(Action<T> handler) where T : struct
    {
        Type type = typeof(T);
        if (handlers.ContainsKey(type))
        {
            handlers[type] = Delegate.Combine(handlers[type], handler);
        }
        else
        {
            handlers[type] = handler;
        }
    }

    // 取消订阅
    public static void Unsubscribe<T>(Action<T> handler) where T : struct
    {
        Type type = typeof(T);
        if (handlers.ContainsKey(type))
        {
            handlers[type] = Delegate.Remove(handlers[type], handler);
            if (handlers[type] == null)
            {
                handlers.Remove(type);
            }
        }
    }

    // 发送事件
    public static void Publish<T>(T eventData) where T : struct
    {
        Type type = typeof(T);
        if (handlers.ContainsKey(type))
        {
            Delegate del = handlers[type];
            Action<T> action = del as Action<T>;
            if (action != null)
            {
                action.Invoke(eventData);
            }
        }
    }

    // 清空所有订阅（切换场景时调用）
    public static void Clear()
    {
        handlers.Clear();
    }
}

// ============================================================
// 所有游戏事件（就是一些数据包，从这发到那）
// ============================================================

// 回合阶段变了
public struct PhaseChangedEvent
{
    public TurnPhase phase;      // 当前阶段
    public bool isPlayerTurn;    // 是不是玩家回合
}

// 出牌了
public struct CardPlayedEvent
{
    public Card card;
    public int laneIndex;        // 第几路 0-4
    public bool isPlayerSide;
}

// 攻击了
public struct CardAttackedEvent
{
    public Card attacker;
    public Card target;
    public int damage;
}

// 卡死了
public struct CardDiedEvent
{
    public Card card;
    public int laneIndex;
    public bool isPlayerSide;
}

// 游戏结束
public struct GameOverEvent
{
    public bool playerWin;
}

// 筹码变化
public struct ChipsChangedEvent
{
    public int playerChips;   // 玩家当前筹码
    public int enemyChips;    // 敌人当前筹码
}

// 战利品变化（打脸收牌）
public struct TrophyChangedEvent
{
    public int count;
}

// 结束出牌阶段（玩家点按钮跳过等待）
public struct EndPlayPhaseEvent
{
    // 空的结构体，只是一个信号
}
public struct HandChangedEvent
{

}

// 进入一关（开始战斗）
public struct LevelStartedEvent
{
    public int levelIndex;   // 第几关 0~51
    public bool isBoss;
}

// 过关（打光敌人筹码，非整局胜利）
public struct LevelClearedEvent
{
}

// 地图刷新（生成/更新地图）
public struct MapChangedEvent
{
}

// 回合阶段
public enum TurnPhase
{
    Draw,    // 抽牌
    Play,    // 出牌
    Battle,  // 战斗
    End      // 回合结束
}
