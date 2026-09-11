using UnityEngine;

/// <summary>
/// 地面检测器：每帧从物体上方往下打射线，把物体的 Y 锁在地面上。
/// 只改 Y，不影响 XZ 移动（XZ 由 BossSquirrel 的代码驱动）。
///
/// 挂在 Boss 根物体（OneBoss(A)）上，或任何需要贴地的物体上。
/// </summary>
public class Floor : MonoBehaviour
{
    [Header("检测设置")]
    public float checkDistance = 5f;        // 射线检测距离（从物体上方往下打）
    public float groundOffset = 0f;         // 贴地偏移：脚底到地面的距离（0 = 脚底贴地）
    public LayerMask groundLayer;           // 地面层（只检测这一层，避免打到 Boss 自己）

    [Header("调试")]
    public bool drawDebugRay = true;        // 在 Scene 视图画射线

    private float baseY = 0f;               // 初始 Y（备用）

    void Start()
    {
        baseY = transform.position.y;
    }

    void Update()
    {
        KeepOnGround();
    }

    void KeepOnGround()
    {
        // 从物体上方往下打射线
        Vector3 origin = transform.position + Vector3.up * checkDistance;
        Vector3 direction = Vector3.down;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, checkDistance * 2f, groundLayer))
        {
            // 把 Y 锁在地面高度 + 偏移
            Vector3 pos = transform.position;
            pos.y = hit.point.y + groundOffset;
            transform.position = pos;

            if (drawDebugRay)
                Debug.DrawLine(origin, hit.point, Color.green);
        }
        else
        {
            if (drawDebugRay)
                Debug.DrawLine(origin, origin + direction * checkDistance * 2f, Color.red);
        }
    }
}