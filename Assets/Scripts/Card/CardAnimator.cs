using System.Collections;
using UnityEngine;

/// <summary>
/// 卡牌动画工具：纯补间，不关心战斗逻辑。
/// 每个方法都是协程，用 yield return / StartCoroutine 调用。
/// 传的是 Transform，所以卡牌、筹码、牌堆都能复用。
/// </summary>
public static class CardAnimator
{
    // 缓动类型。命名和常见缓动函数一致：
    //   OutQuad  = 快速起步、结尾减速（冲出去的爆发感）
    //   OutCubic = 比 OutQuad 更猛的减速（后仰用）
    //   OutBack  = 终点前轻微过冲再回来（回弹、弹性）
    public enum Ease
    {
        Linear,
        OutQuad,
        OutCubic,
        OutBack
    }

    static float EaseFunc(float p, Ease e)
    {
        switch (e)
        {
            case Ease.OutQuad: return 1f - (1f - p) * (1f - p);
            case Ease.OutCubic: return 1f - Mathf.Pow(1f - p, 3f);
            case Ease.OutBack:
                {
                    const float s = 1.70158f;
                    float u = p - 1f;
                    return 1f + (s + 1f) * u * u * u + s * u * u;
                }
            default: return p;
        }
    }

    // 边移动边旋转（线性，旧调用兼容）
    public static IEnumerator MoveAndRotate(Transform target, Vector3 to, Quaternion toRot, float duration)
    {
        return MoveAndRotate(target, to, toRot, duration, Ease.Linear);
    }

    // 边移动边旋转（带缓动）。猛锤用 OutQuad，回位用 OutBack 有弹性。
    public static IEnumerator MoveAndRotate(Transform target, Vector3 to, Quaternion toRot, float duration, Ease ease)
    {
        Vector3 fromPos = target.position;
        Quaternion fromRot = target.rotation;
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / duration);
            float k = EaseFunc(p, ease);
            target.position = Vector3.LerpUnclamped(fromPos, to, k);
            target.rotation = Quaternion.SlerpUnclamped(fromRot, toRot, k);
            yield return null;
        }
        target.position = to;
        target.rotation = toRot;
    }

    // 绕 pivot 点、沿 axis 轴转 angle 度（戳出去/被打得后仰）。
    public static IEnumerator ThrustOut(Transform target, Vector3 pivot, Vector3 axis, float angle, float duration)
    {
        return ThrustOut(target, pivot, axis, angle, duration, Ease.Linear);
    }

    public static IEnumerator ThrustOut(Transform target, Vector3 pivot, Vector3 axis, float angle, float duration, Ease ease)
    {
        Vector3 startPos = target.position;
        Quaternion startRot = target.rotation;
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float k = EaseFunc(Mathf.Clamp01(elapsed / duration), ease);
            Quaternion rot = Quaternion.AngleAxis(angle * k, axis);
            target.position = pivot + rot * (startPos - pivot);
            target.rotation = rot * startRot;
            yield return null;
        }
        Quaternion endRot = Quaternion.AngleAxis(angle, axis);
        target.position = pivot + endRot * (startPos - pivot);
        target.rotation = endRot * startRot;
    }

    // 边前倾边沿半圆弧飞过去：位置走抛物线（像半圆）。
    // 水平位移用 OutQuad（起步猛），撞击瞬间前倾角度最大（卡在最有冲击感的姿势）。
    // 终点不回正，由调用方的 MoveAndRotate 拉回时自然回正。
    public static IEnumerator ArcWithTilt(Transform target, Vector3 to, float height, float angle, Vector3 dir, float duration)
    {
        Vector3 fromPos = target.position;
        Quaternion fromRot = target.rotation;
        Vector3 axis = Vector3.Cross(Vector3.up, dir);
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / duration);
            float k = EaseFunc(p, Ease.OutQuad);   // 爆发：起步飞快
            Vector3 pos = Vector3.LerpUnclamped(fromPos, to, k);
            pos.y += height * 4f * p * (1f - p);    // 抛物线高度用线性 p，保证起落在桌面上
            float tiltK = EaseFunc(p, Ease.OutCubic);   // 越接近目标越前倾
            Quaternion rot = Quaternion.AngleAxis(angle * tiltK, axis) * fromRot;
            target.position = pos;
            target.rotation = rot;
            yield return null;
        }
        Vector3 endPos = to;
        target.position = endPos;
        target.rotation = Quaternion.AngleAxis(angle, axis) * fromRot;   // 撞上时保持前倾
    }

    // 抛物线飞行（和筹码 FlyToPotRoutine 同一套配方，只是不用刚体）：
    // 同一个物体走完整条曲线，中途不换父节点、不重生，所以没有"飞到半空突然瞬移"的缝。
    // 曲线由"顶点比起飞点高多少 + 总时长"反解重力与竖直初速，横向按总时长均分 → 落点精确。
    // 落地后再小跳一下（bounceHeight/bounceDuration）压住，就是"q弹"的手感。
    public static IEnumerator FlyArc(Transform target, Vector3 to, Quaternion toRot,
        float apexHeight, float duration, float bounceHeight, float bounceDuration)
    {
        if (target == null) yield break;

        Vector3 startPos = target.position;
        Quaternion startRot = target.rotation;

        float h = Mathf.Max(0.05f, apexHeight);
        float T = Mathf.Max(0.05f, duration);
        float drop = Mathf.Max(0.05f, (startPos.y + h) - to.y);   // 从顶点落下来的高度
        float g = 2f * Mathf.Pow(Mathf.Sqrt(h) + Mathf.Sqrt(drop), 2f) / (T * T);
        float vy0 = Mathf.Sqrt(2f * g * h);   // 竖直初速：正好拱到 apexHeight 高

        Vector3 vh = to - startPos;   // 水平分量按总时长均分，横向刚好落在目标上
        vh.y = 0f;
        vh /= T;

        float ct = 0f;
        while (ct < T)
        {
            if (target == null) yield break;   // 飞行途中被销毁（换关清场）就收手
            ct += Time.deltaTime;
            float t = Mathf.Min(ct, T);

            Vector3 pos = startPos + vh * t;
            pos.y = startPos.y + vy0 * t - 0.5f * g * t * t;
            target.position = pos;
            target.rotation = Quaternion.Slerp(startRot, toRot, t / T);
            yield return null;
        }

        if (target == null) yield break;

        // 落地回弹：小跳一下再压住，落位才有重量感
        if (bounceHeight > 0f && bounceDuration > 0f)
        {
            float bt = 0f;
            while (bt < 1f)
            {
                if (target == null) yield break;
                bt += Time.deltaTime / bounceDuration;
                target.position = to + Vector3.up * (Mathf.Sin(Mathf.Clamp01(bt) * Mathf.PI) * bounceHeight);
                yield return null;
            }
        }

        target.position = to;
        target.rotation = toRot;
    }//--------------------------------重点--------------------------------------------卡牌飞行

    // 命中停顿：全世界卡一下（打击感核心）。用真实时间等待，timeScale=0 也能恢复。
    // 战斗协程是串行的，同一时刻只会有一个 HitStop。
    public static IEnumerator HitStop(float realtimeSeconds = 0.06f, float timeScale = 0.05f)
    {
        Time.timeScale = timeScale;
        yield return new WaitForSecondsRealtime(realtimeSeconds);
        Time.timeScale = 1f;
    }

    // 原地横向抖动（被打落地后余震）。amplitude 是最大偏移，随时间衰减。
    public static IEnumerator Jitter(Transform target, Vector3 basePos, float amplitude, float duration)
    {
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(elapsed / duration);
            Vector3 off = new Vector3(
                (Random.value * 2f - 1f),
                0f,
                (Random.value * 2f - 1f)) * amplitude * k;
            target.position = basePos + off;
            yield return null;
        }
        target.position = basePos;
    }
}
