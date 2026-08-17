using UnityEngine;

/// <summary>
/// 地图镜头滚动（挂在 Map 场景 Main Camera 上）。
/// 鼠标滚轮上下移动镜头，看长地图。
/// </summary>
public class MapCameraScroll : MonoBehaviour
{
    [Header("滚动速度")]
    public float scrollSpeed = 2f;

    [Header("镜头上下范围（世界 y，按地图长短调）")]
    public float minY = -10f;
    public float maxY = 10f;

    void Update()
    {
        float wheel = Input.mouseScrollDelta.y;
        if (wheel == 0f) return;

        Vector3 pos = transform.position;
        pos.y += wheel * scrollSpeed;
        pos.y = Mathf.Clamp(pos.y, minY, maxY);
        transform.position = pos;
    }
}
