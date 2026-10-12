using System.Collections;
using UnityEngine;

/// <summary>
/// 能飞回牌堆的商店物体（扑克牌 ShopCard / 小丑 HookCard 都实现它）。
/// MapManager.ClearShopCardsRoutine 统一遍历调用，不用分支判组件类型。
/// </summary>
public interface IShopFlyable
{
    IEnumerator FlyStraight(Transform card, Vector3 targetPos, float duration);
}
