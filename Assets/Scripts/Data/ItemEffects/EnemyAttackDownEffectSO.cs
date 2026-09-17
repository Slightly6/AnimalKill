using UnityEngine;

/// <summary>
/// 效果：敌方全体在场卡牌 -value 战力。
/// </summary>
[CreateAssetMenu(fileName = "EnemyAttackDownEffect", menuName = "Shop Effects/Enemy Attack Down")]
public class EnemyAttackDownEffectSO : ItemEffectSO
{
    public int value = 1;

    public override void Apply()
    {
        GameManager.Instance.BuffEnemyCards(-value);
        Debug.Log("[道具] 敌方全体 -" + value + " 攻");
    }
}
