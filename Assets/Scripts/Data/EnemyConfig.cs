using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "EnemyConfig",
    menuName = "Poker Battle/Enemy Config"
)]
public class EnemyConfig : ScriptableObject
{
    [Header("敌人信息")]
    public string enemyName;
    public int Chips = 100;

    [Header("敌人牌库")]
    public List<CardDataSO> deck = new List<CardDataSO>();
    public int handSize = 7;

    [Header("意图概率")]
    [Range(0f, 1f)]
    public float checkChance = 0.2f;

    [Range(0f, 1f)]
    public float raiseChance = 0.25f;

    [Header("敌人伤害倍率")]
    public float callDamageMultiplier = 0.7f;
    public float raiseDamageMultiplier = 1.2f;

    [Header("出牌数量")]
    [Range(1, 5)]
    public int callCardCount = 5;

    [Range(1, 5)]
    public int raiseCardCount = 5;

}