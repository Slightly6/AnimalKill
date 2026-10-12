using UnityEngine;

/// <summary>
/// 手牌专用相机脚本：挂在你自己建的 HandCamera 子相机上。
/// 子相机只渲染手牌层，画在主相机之上，让手牌永远不被场景遮挡。
/// </summary>
public class HandCamera : MonoBehaviour
{
    /// <summary>当前手牌层的编号（0~31），未配置时为 -1</summary>
    public static int HandLayerIndex = -1;

    private Camera cam;
    private Camera parentCam;

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null) return;

        // 从自己的 cullingMask 里找出手牌层编号
        int mask = cam.cullingMask;
        for (int i = 0; i < 32; i++)
        {
            if ((mask & (1 << i)) != 0)
            {
                HandLayerIndex = i;
                break;
            }
        }

        // 强制子相机参数（避免 Inspector 漏设）
        cam.clearFlags = CameraClearFlags.Depth;   // 只清深度，保留主相机画面
        cam.cullingMask = 1 << HandLayerIndex;     // 只看手牌层

        parentCam = transform.parent != null ? transform.parent.GetComponent<Camera>() : null;
        if (parentCam != null)
        {
            cam.depth = parentCam.depth + 1f;      // 比主相机晚渲染 → 画在上面
            // 主相机排除手牌层，避免重复渲染
            parentCam.cullingMask &= ~(1 << HandLayerIndex);
        }
    }

    void LateUpdate()
    {
        // FOV 必须跟主相机同步，否则手牌透视和场景对不上（看起来像穿模/错位）
        if (cam != null && parentCam != null)
        {
            cam.fieldOfView = parentCam.fieldOfView;
            cam.nearClipPlane = parentCam.nearClipPlane;
            cam.farClipPlane = parentCam.farClipPlane;
        }
    }
}
