using UnityEngine;

/// 下回合出牌点数 +N
[CreateAssetMenu(fileName = "AddCoin-1", menuName = "Effects/下回合加一金币")]
public class AddCoin : AbilityEffectSO
{
    public int addCoin = 1;
    public AddCoin() { timing = EffectTiming.Delayed; }

    public override void Apply()
    {
        if (BattleManager.Instance == null) return;
        BattleManager.Instance.AddPointsNextTurn(addCoin);
    }
}
