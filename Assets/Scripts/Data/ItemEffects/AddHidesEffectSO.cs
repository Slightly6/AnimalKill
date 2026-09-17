using UnityEngine;

/// <summary>
/// 效果：获得 amount 兽皮。
/// </summary>
[CreateAssetMenu(fileName = "AddHidesEffect", menuName = "Shop Effects/Add Hides")]
public class AddHidesEffectSO : ItemEffectSO
{
    public int amount = 1;

    public override void Apply()
    {
        GameProgress.hides += amount;
        Debug.Log("[道具] +" + amount + " 兽皮");
    }
}
