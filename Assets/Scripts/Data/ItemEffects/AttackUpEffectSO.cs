using UnityEngine;

/// <summary>
/// 效果：我方全体在场卡牌 +value 战力。
/// </summary>
[CreateAssetMenu(fileName = "AttackUpEffect", menuName = "Shop Effects/Attack Up")]
public class AttackUpEffectSO : ItemEffectSO
{
    public int value = 1;

    public override void Apply()
    {
        GameManager.Instance.BuffPlayerCards(value);
        Debug.Log("[道具] 我方全体 +" + value + " 攻");
    }
}
