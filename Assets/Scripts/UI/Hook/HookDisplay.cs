using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 小丑栏：商店买下的小丑实体飞到挂点，单摆悬挂展示。
/// 只管外观和动画（飞行/单摆/碰撞晃动）；数据与购买逻辑在 JokerManager / ShopManager。
/// </summary>
public class HookDisplay : MonoBehaviour
{
    [Header("挂点（Hook 下建 5 个空物体，摆好，从左到右按顺序拖进来）")]
    public Transform[] hangPoints = new Transform[5];

    [Header("小丑外观")]
    public float cardScale = 0.6f;   // 小丑缩放（比手牌小一圈）
    public float hangDrop = 0.78f;   // 挂上后中心离挂点往下多远 = 1.3×cardScale（牌高2.6的一半），改 cardScale 要跟着改
    public float bumpKick = 30f;     // 新小丑挂上来时，踢老小丑一脚的角速度

    List<GameObject> spawned = new List<GameObject>();   // 支点列表（index 对应第几个小丑）

    // 小丑实体飞向第 index 个挂点：EaseIn 飞行 → 挂入支点开始单摆 → 踢老小丑
    // 由 JokerManager.Add 调用
    public IEnumerator FlyJokerToHook(Transform joker, int index)
    {
        if (joker == null || index < 0 || index >= hangPoints.Length || hangPoints[index] == null)
            yield break;

        // 支点：空物体放在挂点上，小丑挂在它下面绕着荡
        GameObject pivot = new GameObject("Joker_" + index);
        pivot.transform.SetParent(hangPoints[index], false);
        pivot.transform.localPosition = Vector3.zero;
        pivot.transform.localRotation = Quaternion.identity;
        spawned.Add(pivot);

        // 终态：支点正下方、缩到小丑尺寸、转向钩子姿态
        Vector3 startPos = joker.position;
        Quaternion startRot = joker.rotation;
        Vector3 startScale = joker.localScale;
        Vector3 endPos = pivot.transform.TransformPoint(new Vector3(0f, -hangDrop, 0f));
        Quaternion endRot = pivot.transform.rotation * Quaternion.Euler(0f, 180f, 0f);
        Vector3 endScale = Vector3.one * cardScale;

        float duration = 0.4f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            float ease = p * p;   // EaseIn：起手慢、挂上时快
            if (joker == null) yield break;
            joker.position = Vector3.Lerp(startPos, endPos, ease);
            joker.rotation = Quaternion.Slerp(startRot, endRot, ease);
            joker.localScale = Vector3.Lerp(startScale, endScale, ease);
            yield return null;
        }

        if (joker == null) yield break;

        // 挂入支点：归零局部变换，单摆物理生效
        joker.SetParent(pivot.transform, false);
        joker.localPosition = new Vector3(0f, -hangDrop, 0f);
        joker.localRotation = Quaternion.Euler(0f, 180f, 0f);
        joker.localScale = endScale;
        pivot.AddComponent<HangingCard>();

        // 新小丑挂上：踢一脚之前的小丑（一起晃）
        for (int j = 0; j < spawned.Count - 1; j++)
        {
            HangingCard h = spawned[j].GetComponent<HangingCard>();
            if (h != null) h.Bump(bumpKick);
        }
    }
}
