using UnityEngine;

/// <summary>
/// 效果：跳过本回合敌方的攻击阶段（敌方本回合不打）。
/// 只对当前回合生效，用一次就没。
/// </summary>
[CreateAssetMenu(fileName = "SkipEnemyTurnEffect", menuName = "Shop Effects/Skip Enemy Turn")]
public class SkipEnemyTurnEffectSO : ItemEffectSO
{
    public override void Apply()
    {
        BattleManager.Instance.SkipEnemyAttack();
        Debug.Log("[道具] 跳过敌方本回合攻击");
    }
}
