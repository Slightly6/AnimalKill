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

    // 绕 pivot 点、沿 axis 轴转 angle 度（戳出去）。用于卡牌攻击的「前倾戳刺」。
    // 只动位置和朝向，缩放不变。回位用 MoveAndRotate。
    public static IEnumerator ThrustOut(Transform target, Vector3 pivot, Vector3 axis, float angle, float duration)
    {
        Vector3 startPos = target.position;
        Quaternion startRot = target.rotation;
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float p = elapsed / duration;
            float cur = Mathf.Lerp(0f, angle, p);
            Quaternion rot = Quaternion.AngleAxis(cur, axis);
            target.position = pivot + rot * (startPos - pivot);
            target.rotation = rot * startRot;
            yield return null;
        }
        Quaternion endRot = Quaternion.AngleAxis(angle, axis);
        target.position = pivot + endRot * (startPos - pivot);
        target.rotation = endRot * startRot;
    }

    // 边前倾边沿半圆弧飞过去：位置走抛物线（像半圆），倾斜也走同样的抛物线（中间最斜、两头 0）。
    // 落点回到原朝向、不倾斜，所以不会插进桌子。
    public static IEnumerator ArcWithTilt(Transform target, Vector3 to, float height, float angle, Vector3 dir, float duration)
    {
        Vector3 fromPos = target.position;
        Quaternion fromRot = target.rotation;
        Vector3 axis = Vector3.Cross(Vector3.up, dir);
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float p = elapsed / duration;
            Vector3 pos = Vector3.Lerp(fromPos, to, p);
            pos.y += height * 4f * p * (1f - p);   // 高度：起 0 → 顶 height → 落 0
            float tiltP = 4f * p * (1f - p);       // 倾斜：中间最大、两头 0
            Quaternion rot = Quaternion.AngleAxis(angle * tiltP, axis) * fromRot;
            target.position = pos;
            target.rotation = rot;
            yield return null;
        }
        target.position = to;
        target.rotation = fromRot;
    }
}
