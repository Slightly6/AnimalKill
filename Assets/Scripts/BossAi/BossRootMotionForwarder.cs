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

        // 临时调试：看放大后的位移，方便调 scale（调好可删掉这行）
        Debug.Log("[Boss根位移] scale=" + scale + " delta=" + delta);

        // 把放大后的位移加到根节点上
        root.position += delta;

        // 攻击跟着动画转：把动画自带的旋转也转到根节点上，
        // 这样跳劈时 Boss 的朝向会跟着劈砍动画自然转（不再锁死）。
        root.rotation *= anim.deltaRotation;
    }
}
