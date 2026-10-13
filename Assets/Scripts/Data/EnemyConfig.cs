using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "EnemyConfig",
    menuName = "Poker Battle/Enemy Config"
)]
public class EnemyConfig : ScriptableObject
{
    [Header("敌人信息")]
    [Tooltip("敌人名字，显示在 UI 上")]
    public string enemyName;

    [Tooltip("敌人简介，描述这个敌人的特点（如：鲁莽的牛仔，第一回合必定全力出击）")]
    [TextArea(2, 4)] public string description;

    public int Chips = 100;

    [Header("敌人牌库")]
    public List<CardDataSO> deck = new List<CardDataSO>();
    public int handSize = 7;

    [Header("出牌数量（每回合在区间内随机）")]
    [Range(1, 5)] public int minCardCount = 1;   // 最少出几张
    [Range(1, 5)] public int maxCardCount = 5;   // 最多出几张

    [Header("弃牌数量（Fold 时在区间内随机）")]
    [Range(0, 5)] public int minDiscard = 1;     // 最少弃几张
    [Range(0, 5)] public int maxDiscard = 3;     // 最多弃几张
    [Header("敌人行为池（按 weight 加权随机选一个执行）")]
    public List<EnemyBehaviorSO> behaviors = new List<EnemyBehaviorSO>();

    [Header("备用行为池（Switch 变脸等机制切换时用；空=不切换）")]
    public List<EnemyBehaviorSO> backupBehaviors = new List<EnemyBehaviorSO>();

    [Header("被动机制（不参与 weight 随机，按钩子自动触发）")]
    public List<EnemyBehaviorSO> passives = new List<EnemyBehaviorSO>();
}