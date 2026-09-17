using UnityEngine;

/// <summary>
/// 效果：玩家获得 amount 筹码。
/// </summary>
[CreateAssetMenu(fileName = "AddChipsEffect", menuName = "Shop Effects/Add Chips")]
public class AddChipsEffectSO : ItemEffectSO
{
    public int amount = 5;

    public override void Apply()
    {
        GameManager.Instance.AddChips(amount);
        Debug.Log("[道具] 玩家 +" + amount + " 筹码");
    }
}
