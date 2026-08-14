using System.Collections.Generic;
using UnityEngine;
// 地图节点类型
public enum NodeType
{
    Battle,    // 战斗（关联一个关卡配置）
    Upgrade,   // 升级（占位，后期设计内容）
    Boss       // 最终 boss
}

/// <summary>
/// 一个战斗关卡的所有可变配置。放进 LevelDatabase 的大数组里，逐关填。
/// </summary>
[System.Serializable]
public class LevelConfig
{
    public string levelName = "第1关";
    public bool isBoss = false;              // 第 52 关勾上

    [Header("敌人")]
    public List<CardDataSO> enemyDeck = new List<CardDataSO>();  // 敌人牌堆
    public int enemyBonusPower = 0;          // 敌方牌额外 +战力
    public bool enemyAwakened = false;       // 敌方牌是否觉醒
    public int minPreviewCards = 2;          // 开局预告牌数
    public int maxPreviewCards = 5;
    public int minRefillCards = 2;           // 每回合补牌数
    public int maxRefillCards = 5;

    [Header("筹码")]
    public int enemyStartingChips = 100;     // 敌人开局筹码（玩家筹码跨关继承，不在这）

    [Header("手牌")]
    public int initialHandSize = 6;          // 开局手牌数
    public int drawPerTurn = 1;              // 每回合抽牌数
}
