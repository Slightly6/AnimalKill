using UnityEngine;

/// <summary>
/// 动画事件转发器（中转站）。
/// 挂在「带 Animator 的那个子物体」上。
///
/// 为什么要它：
/// Unity 的动画事件只会发给「Animator 自己所在的那个物体」（这里是子物体），
/// 它不会往上找父物体。而 MoveToHand / MoveToBack 这些方法
/// 写在父物体（Hero）的 WeaponManager 里，所以动画事件根本找不到它们。
///
/// 解决：让动画事件先打到这个转发器（在子物体上），再由转发器转给父物体的真实逻辑。
///
/// 不用手动拖：WeaponManager 在 Start() 里会自动把它挂到 Animator 所在物体上。
/// </summary>
public class AnimationEventRelay : MonoBehaviour
{
    private WeaponManager weaponManager;   // 父物体上的武器管理器（管拿刀收刀）
    private HeroController hero;           // 父物体上的角色控制器（管移动）
    private Animator anim;                 // 自己身上的动画器（读根运动位移）
    public float scale = 1f;               // 根运动放大倍数：动画自带位移太小，放大到合适的前冲+跳起
    // 往上找父物体里的这些脚本，并拿到自己身上的动画器
    void Start()
    {
        weaponManager = GetComponentInParent<WeaponManager>();
        hero = GetComponentInParent<HeroController>();
        anim = GetComponent<Animator>();
    }

    // 根运动：开了 applyRootMotion 后，动画每帧算出的位移会送到这里。
    // 我们把位移转发给 HeroController，让身体真的往前/往上动（攻击的突进）。
     void OnAnimatorMove()
    {
        // 只有 applyRootMotion = true（攻击时）才会进这里
        if (anim == null || hero == null) return;

        // 动画自带位移太小，乘一个倍数放大成合适的前冲+跳起；
        // 乘的是整体（XZ 前冲 + Y 跳起），节奏弧线还是动画本来的样子。
        Vector3 delta = anim.deltaPosition * scale;
        // 把放大后的位移加到根节点上
        hero.transform.position += delta;

        // 攻击跟着动画转：把动画自带的旋转也转到根节点上，
        // 这样跳劈时 Boss 的朝向会跟着劈砍动画自然转（不再锁死）。
        hero.transform.rotation *= anim.deltaRotation;
    }
    // 拿刀动画里「抓刀那一帧」→ 转发给 WeaponManager，把刀挂到手上
    public void MoveToHand()
    {
        if (weaponManager != null) weaponManager.MoveToHand();
    }

    // 收刀动画里「放回那一帧」→ 转发给 WeaponManager，把刀挂回背上
    public void MoveToBack()
    {
        if (weaponManager != null) weaponManager.MoveToBack();
    }

    // 攻击动画「开始那一帧」→ 转发给 HeroController，开 Root Motion、锁移动
    public void StartAttack()
    {
        if (hero != null) hero.StartAttack();
    }

    // 攻击动画「结束那一帧」→ 转发给 HeroController，关 Root Motion、恢复移动
    public void EndAttack()
    {
        if (hero != null) hero.EndAttack();
    }
}
