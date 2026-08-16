using UnityEngine;

/// <summary>
/// 桌面摄像机：透视 + 两个机位之间平滑切换。
/// 挂在 Main Camera 上。默认低位（坐桌边看远端），拖牌时切高位（俯瞰全桌）。
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
        // 位置往目标机位靠（帧率相关平滑，平民写法）
        Vector3 targetPos = targetHigh ? highPosition : lowPosition;
        transform.position = Vector3.Lerp(transform.position, targetPos, lerpSpeed * Time.deltaTime);

        // 始终看向桌面中心
        transform.LookAt(focusPoint, Vector3.up);
    }
    // 切换机位：true=高位（俯视全桌），false=低位（看远端）。CardDisplay 拖牌时调用。
    public void SetHigh(bool high)
    {
        targetHigh = high;
    }
}
