using System.Collections;
using UnityEngine;

/// <summary>
/// 两条胳膊同时插值到目标位置+旋转+缩放（比如伸出去接小丑）。
/// 调 Reach() 触发，ReachBack() 缩回原位。
/// </summary>
public class Arm : MonoBehaviour
{
    [Header("两条胳膊（Inspector 拖）")]
    public Transform arm1;
    public Transform arm2;

    [Header("目标（Inspector 拖空物体或任意 Transform）")]
    public Transform target1;
    public Transform target2;

    [Header("动画参数")]
    public float duration = 0.4f;

    // 起始状态（位置、旋转、缩放）
    private Vector3 start1Pos, start2Pos;
    private Quaternion start1Rot, start2Rot;
    private Vector3 start1Scale, start2Scale;
    private Coroutine routine;

    /// <summary>
    /// 两条胳膊同时伸向目标（位置+旋转+缩放）
    /// </summary>
    public void Reach()
    {
        if (arm1 == null || arm2 == null) return;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(ReachRoutine());
    }

    /// <summary>
    /// 两条胳膊缩回起始状态（位置+旋转+缩放）
    /// </summary>
    public void ReachBack()
    {
        if (arm1 == null || arm2 == null) return;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(ReachBackRoutine());
    }

    IEnumerator ReachRoutine()
    {
        // 记录起始状态
        start1Pos = arm1.position;
        start2Pos = arm2.position;
        start1Rot = arm1.rotation;
        start2Rot = arm2.rotation;
        start1Scale = arm1.localScale;
        start2Scale = arm2.localScale;

        // 目标状态
        Vector3 t1Pos = target1 != null ? target1.position : start1Pos;
        Vector3 t2Pos = target2 != null ? target2.position : start2Pos;
        Quaternion t1Rot = target1 != null ? target1.rotation : start1Rot;
        Quaternion t2Rot = target2 != null ? target2.rotation : start2Rot;
        Vector3 t1Scale = target1 != null ? target1.localScale : start1Scale;
        Vector3 t2Scale = target2 != null ? target2.localScale : start2Scale;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            arm1.position = Vector3.Lerp(start1Pos, t1Pos, p);
            arm1.rotation = Quaternion.Slerp(start1Rot, t1Rot, p);
            arm1.localScale = Vector3.Lerp(start1Scale, t1Scale, p);
            arm2.position = Vector3.Lerp(start2Pos, t2Pos, p);
            arm2.rotation = Quaternion.Slerp(start2Rot, t2Rot, p);
            arm2.localScale = Vector3.Lerp(start2Scale, t2Scale, p);
            yield return null;
        }
        arm1.position = t1Pos; arm1.rotation = t1Rot; arm1.localScale = t1Scale;
        arm2.position = t2Pos; arm2.rotation = t2Rot; arm2.localScale = t2Scale;
    }

    IEnumerator ReachBackRoutine()
    {
        Vector3 from1Pos = arm1.position, from2Pos = arm2.position;
        Quaternion from1Rot = arm1.rotation, from2Rot = arm2.rotation;
        Vector3 from1Scale = arm1.localScale, from2Scale = arm2.localScale;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            arm1.position = Vector3.Lerp(from1Pos, start1Pos, p);
            arm1.rotation = Quaternion.Slerp(from1Rot, start1Rot, p);
            arm1.localScale = Vector3.Lerp(from1Scale, start1Scale, p);
            arm2.position = Vector3.Lerp(from2Pos, start2Pos, p);
            arm2.rotation = Quaternion.Slerp(from2Rot, start2Rot, p);
            arm2.localScale = Vector3.Lerp(from2Scale, start2Scale, p);
            yield return null;
        }
        arm1.position = start1Pos; arm1.rotation = start1Rot; arm1.localScale = start1Scale;
        arm2.position = start2Pos; arm2.rotation = start2Rot; arm2.localScale = start2Scale;
    }
}
