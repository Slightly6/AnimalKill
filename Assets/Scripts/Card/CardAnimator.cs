using System.Collections;
using UnityEngine;

/// <summary>
/// 卡牌动画工具：纯补间，不关心战斗逻辑。
/// 每个方法都是协程，用 yield return / StartCoroutine 调用。
/// 传的是 Transform，所以卡牌、筹码、牌堆都能复用。
/// </summary>
public static class CardAnimator
{
    // 边移动边旋转（猛锤、回位都用它）。toRot 直接传目标朝向，用 Slerp 插值更稳。
    public static IEnumerator MoveAndRotate(Transform target, Vector3 to, Quaternion toRot, float duration)
    {
        Vector3 fromPos = target.position;
        Quaternion fromRot = target.rotation;
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float p = elapsed / duration;
            target.position = Vector3.Lerp(fromPos, to, p);
            target.rotation = Quaternion.Slerp(fromRot, toRot, p);
            yield return null;
        }
        target.position = to;
        target.rotation = toRot;
    }
}
