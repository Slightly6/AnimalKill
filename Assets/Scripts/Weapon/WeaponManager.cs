using UnityEngine;

/// <summary>
/// 武器管理器：挂在 Hero 上，管「拿刀/收刀/换武器」。
/// 用动画器参数 Hold Knife 驱动拿刀收刀动画。
/// 另外加一个 C# 布尔 isKnifeOut：刀拔出来没。攻击脚本（Attack.cs）要检查它，
/// 只有它为 true 才能打（拿刀=true，收刀=false）。
/// </summary>
public class WeaponManager : MonoBehaviour
{
    [Header("Hero 的动画器（拖 Hero 上的 Animator）")]
    public Animator animator;

    [Header("装备格（所有武器，有几把拖几把）")]
    public Weapon[] weapons;

    [Header("当前用第几把（从 0 开始）")]
    public int currentIndex = 0;

    [Header("刀是否已拔出（true=能攻击，false=收着不能攻击）")]
    public bool isKnifeOut = false;

    [Header("武器伤害（挂在动画器所在模型物体上，不拖自动找）")]
    public WeaponDamage weaponDamage;

    private Weapon currentWeapon;   // 当前武器

    void Start()
    {
        // 动画器在子物体上，动画事件只会发给动画器所在的子物体，打不到父物体上的本脚本。
        // 所以在动画器所在物体上挂一个「转发器」，让 MoveToHand / MoveToBack 能传回来。
        if (animator != null)
        {
            AnimationEventRelay relay = animator.gameObject.GetComponent<AnimationEventRelay>();
            if (relay == null)
            {
                animator.gameObject.AddComponent<AnimationEventRelay>();
            }
        }

        // 武器伤害组件挂在动画器所在模型物体上（动画事件直接打到它），这里自动找一下
        if (weaponDamage == null && animator != null)
        {
            weaponDamage = animator.GetComponent<WeaponDamage>();
        }

        // 修一下越界
        if (currentIndex < 0) currentIndex = 0;
        if (weapons == null || weapons.Length == 0) return;
        if (currentIndex >= weapons.Length) currentIndex = 0;

        currentWeapon = weapons[currentIndex];
        if (currentWeapon != null) currentWeapon.Sheathe();   // 一开场先收刀
        isKnifeOut = false;                                    // 收着，不能攻击

        ApplyWeaponDamage();   // 开局第一把武器的伤害
    }

    // 换到第 index 把武器（只换武器，不拿刀）
    public void Equip(int index)
    {
        if (weapons == null || weapons.Length == 0) return;
        if (index < 0 || index >= weapons.Length) return;

        if (currentWeapon != null) currentWeapon.Sheathe();   // 旧的收回收纳节点

        currentIndex = index;
        currentWeapon = weapons[currentIndex];

        ApplyWeaponDamage();   // 换武器了，更新伤害值

        // 告诉动画器现在用哪把武器（Knife Type：0=第一把 1=第二把…）
        if (animator != null) animator.SetInteger("Knife Type", currentIndex);
    }

    // 把当前武器的伤害值塞给武器伤害组件（每把武器伤害不同）
    void ApplyWeaponDamage()
    {
        if (weaponDamage == null) return;
        if (currentWeapon == null || currentWeapon.data == null) return;
        weaponDamage.SetDamage(currentWeapon.data.damage);
    }

    // 拿刀：设 Hold Knife=true + IsKnife=true（一次性触发拿刀动画）。
    // 注意：这里不再立刻挂武器了，改由「拿刀动画里的事件」在刀到手上那一帧调用 MoveToHand()，
    // 这样刀才会在正确时机出现在手上，而不是瞬间闪现。
    public void DrawWeapon()
    {
        if (animator != null)
        {
            animator.SetBool("Hold Knife", true);
            animator.SetTrigger("IsKnife");      // 一次性触发拿刀动画（触发器被消费后自动复位，不用手动关）
        }
        isKnifeOut = true;      // 刀拔出来了，能攻击
    }

    // 收刀：设 Hold Knife=false + IsKnife=true（一次性触发收刀动画）。
    // 挂回背上的时机，由「收刀动画里的事件」调用 MoveToBack() 完成。
    public void SheatheWeapon()
    {
        if (animator != null)
        {
            animator.SetBool("Hold Knife", false);
            animator.SetTrigger("IsKnife");      // 一次性触发收刀动画（触发器会自动复位）
        }
        isKnifeOut = false;     // 刀收起来了，不能攻击
    }

    // 把武器挂到它自己的手上节点（由拿刀动画里的事件，在「抓刀那一帧」调用）
    public void MoveToHand()
    {
        if (currentWeapon != null) currentWeapon.Draw();
    }

    // 把武器挂回它自己的收纳节点（由收刀动画里的事件，在「放回那一帧」调用）
    public void MoveToBack()
    {
        if (currentWeapon != null) currentWeapon.Sheathe();
    }

    void Update()
    {
        // 只在第一人称能操作
        if (GameProgress.currentStage != GameStage.FirstPerson) return;

        // 按 1/2/3 选武器（1=第一把），选完自动拿刀
        if (Input.GetKeyDown(KeyCode.Alpha1)) { Equip(0); DrawWeapon(); }
        if (Input.GetKeyDown(KeyCode.Alpha2)) { Equip(1); DrawWeapon(); }
        if (Input.GetKeyDown(KeyCode.Alpha3)) { Equip(2); DrawWeapon(); }

        // 按 F 收刀 / 再按 F 拿刀（来回切换）
        if (Input.GetKeyDown(KeyCode.F))
        {
            if (isKnifeOut) SheatheWeapon();   // 拔出来了就收
            else DrawWeapon();                 // 收着就拿
        }
    }
}
