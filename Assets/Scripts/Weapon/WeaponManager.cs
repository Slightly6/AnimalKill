using UnityEngine;

/// <summary>
/// 武器管理器：挂在 Hero 上。
/// 用 Hero 动画器的参数（Hold Knife / Knife Type / AttackType）驱动拿刀、收刀、攻击。
/// 拿刀 = 武器从收纳节点挂到手上节点；收刀 = 挂回收纳节点。
/// </summary>
public class WeaponManager : MonoBehaviour
{
    [Header("Hero 的动画器（拖 Hero 上的 Animator）")]
    public Animator animator;

    [Header("装备格（所有武器，有几把拖几把）")]
    public Weapon[] weapons;

    [Header("手上节点（手骨的子物体）")]
    public Transform handNode;

    [Header("当前用第几把（从 0 开始）")]
    public int currentIndex = 0;

    private Weapon currentWeapon;   // 当前武器

    void Start()
    {
        // 修一下越界
        if (currentIndex < 0) currentIndex = 0;
        if (weapons == null || weapons.Length == 0) return;
        if (currentIndex >= weapons.Length) currentIndex = 0;

        currentWeapon = weapons[currentIndex];
        if (currentWeapon != null) currentWeapon.Sheathe();   // 一开场先收刀
    }

    // 换到第 index 把武器（只换武器，不拿刀）
    public void Equip(int index)
    {
        if (weapons == null || weapons.Length == 0) return;
        if (index < 0 || index >= weapons.Length) return;

        if (currentWeapon != null) currentWeapon.Sheathe();   // 旧的收回收纳节点

        currentIndex = index;
        currentWeapon = weapons[currentIndex];

        // 告诉动画器现在用哪把武器（Knife Type：0=第一把 1=第二把…）
        if (animator != null) animator.SetInteger("Knife Type", currentIndex);
    }

    // 拿刀：设 Hold Knife = true，武器挂到手上
    public void DrawWeapon()
    {
        if (animator != null) animator.SetBool("Hold Knife", true);
        MoveToHand();
    }

    // 收刀：设 Hold Knife = false，武器挂回收纳节点
    public void SheatheWeapon()
    {
        if (animator != null) animator.SetBool("Hold Knife", false);
        MoveToBack();
    }

    // 把武器挂到手上（想更自然，就在「拿刀」动画手摸到刀那一帧加动画事件调这个）
    public void MoveToHand()
    {
        if (currentWeapon != null) currentWeapon.Draw(handNode);
    }

    // 把武器挂回收纳节点（「收刀」动画刀贴回背那一帧加事件调这个）
    public void MoveToBack()
    {
        if (currentWeapon != null) currentWeapon.Sheathe();
    }

    // 攻击：设 AttackType 参数（0=攻击1，1=攻击2）
    public void Attack(int attackType)
    {
        if (animator != null) animator.SetInteger("AttackType", attackType);
    }

    void Update()
    {
        // 只在第一人称能操作
        if (GameProgress.currentStage != GameStage.FirstPerson) return;

        // 按 0/1/2 选武器（0=第一把），选完自动拿刀
        if (Input.GetKeyDown(KeyCode.Alpha0)) { Equip(0); DrawWeapon(); }
        if (Input.GetKeyDown(KeyCode.Alpha1)) { Equip(1); DrawWeapon(); }
        if (Input.GetKeyDown(KeyCode.Alpha2)) { Equip(2); DrawWeapon(); }

        // 按 F 收刀 / 再按 F 拿刀（来回切换）
        if (Input.GetKeyDown(KeyCode.F))
        {
            bool holding = animator != null && animator.GetBool("Hold Knife");
            if (holding) SheatheWeapon();
            else DrawWeapon();
        }
    }
}
