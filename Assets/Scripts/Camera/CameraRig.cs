using UnityEngine;

/// <summary>
/// 桌面摄像机：透视 + 两个机位之间平滑切换。
/// 挂在 Main Camera 上。
/// 视角规则：长按拖拽牌时强制高视角；否则鼠标 Y 轴高于手牌区就高视角，低于就低视角。
/// 点击选中、鼠标左右移动都不额外动相机。
/// </summary>
public class CameraRig : Singleton<CameraRig>
{
    [Header("两个预设机位（世界坐标）")]
    public Vector3 lowPosition = new Vector3(0f, 3.2f, 6.5f);   // 低位：低、靠后，看远端
    public Vector3 highPosition = new Vector3(0f, 8.0f, 3.0f);  // 高位：高、靠中，俯瞰全桌

    [Header("看向桌面中心")]
    public Vector3 focusPoint = new Vector3(0f, 0f, -1f);

    [Header("镜头参数")]
    public float fieldOfView = 55f;   // 透视视野
    public float lerpSpeed = 6f;      // 机位切换速度（越大越快）

    [Header("鼠标驱动视角")]
    public float handViewportY = 0.25f;   // 手牌区上边缘（viewport 0~1），鼠标 Y 高于它 → 高视角

    private Camera cam;
    private bool targetHigh;          // 当前目标是高位还是低位

    void Start()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = false;     // 强制透视
        cam.fieldOfView = fieldOfView;
        transform.position = lowPosition;
    }

    void LateUpdate()
    {
        UpdateTarget();

        // 位置往目标机位靠（帧率相关平滑，平民写法）
        Vector3 targetPos = targetHigh ? highPosition : lowPosition;
        transform.position = Vector3.Lerp(transform.position, targetPos, lerpSpeed * Time.deltaTime);

        // 始终看向桌面中心
        transform.LookAt(focusPoint, Vector3.up);
    }

    // 长按拖拽强制高视角；否则鼠标 Y 轴驱动
    void UpdateTarget()
    {
        if (CardDisplay.draggingCard != null)
        {
            targetHigh = true;   // 拿牌拖拽 → 俯瞰
            return;
        }

        float mouseViewportY = Input.mousePosition.y / (float)Screen.height;   // 0=底 1=顶
        targetHigh = mouseViewportY > handViewportY;
    }
}
