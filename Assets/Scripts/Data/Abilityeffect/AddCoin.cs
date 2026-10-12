using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 原子效果：下回合出牌点数 +N（点金币预制体那个点数）。
/// </summary>
[CreateAssetMenu(fileName = "AddCoin-1", menuName = "Effects/下回合加一金币")]
public class AddCoin : AbilityEffectSO
{
    [Header("增加点数数量")]
    public int addCoin = 1;

    public override bool Apply(Card self, Card target)
    {
        if (BattleManager.Instance == null) return false;
        BattleManager.Instance.AddPointsNextTurn(addCoin);   // 挂账，下回合开始时补进 PointsLeft
        return true;
    }
}