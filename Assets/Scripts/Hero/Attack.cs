using UnityEngine;

/// <summary>
/// 攻击脚本：挂在 Hero 上，管攻击。
/// 左键 = 攻击1，右键 = 攻击2，用 Attack1/Attack2 触发器触发动画（触发器自动复位）。
/// 攻击前检查 WeaponManager 的 isKnifeOut：刀没拔出来不能打。
/// 以后命中判定、伤害、连击都往这里加。
/// </summary>
public class Attack : MonoBehaviour
{
    [Header("Hero 的动画器（不拖就自动找）")]
    public Animator animator;

    [Header("武器管理器（不拖就自动找，要和 WeaponManager 挂同一个物体上）")]
    public WeaponManager weaponManager;

    [Header("Hero 控制器（不拖就自动找，读 isAttacking 防连击）")]
    public HeroController hero;

    void Start()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (weaponManager == null) weaponManager = GetComponent<WeaponManager>();
        if (hero == null) hero = GetComponent<HeroController>();
    }

    void Update()
    {
        // 只在第一人称能操作
        if (GameProgress.currentStage != GameStage.FirstPerson) return;

        // 鼠标左键 = 攻击1，右键 = 攻击2
        if (Input.GetMouseButtonDown(0)) DoAttack(0);
        if (Input.GetMouseButtonDown(1)) DoAttack(1);
    }

    // 攻击：attackType 0=攻击1，1=攻击2
    public void DoAttack(int attackType)
    {
        if (animator == null) return;
        if (weaponManager == null) return;

        // 刀没拔出来（isKnifeOut=false）不能攻击
        if (!weaponManager.isKnifeOut) return;

        // 攻击中（isAttacking=true）不能再触发，避免连点鼠标打出第二刀
        if (hero != null && hero.isAttacking) return;

        // 用触发器触发攻击动画（触发器被消费后自动复位，不会重复播放）
        if (attackType == 0) animator.SetTrigger("Attack1");   // 攻击1
        else animator.SetTrigger("Attack2");                   // 攻击2
    }

}
