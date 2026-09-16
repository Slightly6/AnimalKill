using UnityEngine;

/// <summary>
/// 挂在带 Animator 的模型物体（Orc Idle）上，BossSquirrel 会在运行时自动挂上。
///
/// 作用：攻击时 BossSquirrel 临时打开 applyRootMotion，本脚本在 OnAnimatorMove 里
/// 把动画自带的根位移（前冲 + 跳起）转发到 Boss 根节点，而不是让模型自己动 ——
/// 这样 HitBox、武器、玩家锁定方向都跟着根节点一起走。
///
/// 走路时 applyRootMotion 是关的，OnAnimatorMove 不会被调用，本脚本不生效，
/// 走路仍然由 BossSquirrel 用代码驱动。
/// </summary>
public class BossRootMotionForwarder : MonoBehaviour
{
    [HideInInspector]
    public Transform root;   // Boss 根节点（OneBoss(A)），由 BossSquirrel 在运行时指过来

    [HideInInspector]
    public float scale = 1f; // 根位移放大倍数：动画自带位移太小，放大到合适的前冲+跳起

    private Animator anim;

    [Header("撞墙检测")]
    public LayerMask wallLayer;        // 哪些层算墙（在 Inspector 里选）
    public float wallCheckRadius = 0.5f; // 角色半径
    public float wallCheckHeight = 2f;   // 角色高度
    void Awake()
    {
        anim = GetComponent<Animator>();
    }

    void OnAnimatorMove()
    {
        // 只有 applyRootMotion = true（攻击时）才会进这里
        if (anim == null || root == null) return;

        // 动画自带位移太小，乘一个倍数放大成合适的前冲+跳起；
        // 乘的是整体（XZ 前冲 + Y 跳起），节奏弧线还是动画本来的样子。
        Vector3 delta = anim.deltaPosition * scale;

        if (delta.magnitude > 0.001f)
        {
            Vector3 dir = delta.normalized;
            float dist = delta.magnitude;

            // 用胶囊体检测，考虑角色体积
            float radius = wallCheckRadius;
            float height = wallCheckHeight;

            if (Physics.CapsuleCast(
                root.position + Vector3.up * radius,
                root.position + Vector3.up * (height - radius),
                radius,
                dir,
                out RaycastHit hit,
                dist + 0.1f,
                wallLayer))
            {
                // 撞墙了，位移砍到墙前面
                delta = dir * Mathf.Max(0f, hit.distance - 0.1f);
            }
        }
        // 把放大后的位移加到根节点上
        root.position += delta;

        // 攻击跟着动画转：把动画自带的旋转也转到根节点上，
        // 这样跳劈时 Boss 的朝向会跟着劈砍动画自然转（不再锁死）。
        root.rotation *= anim.deltaRotation;
    }
}
