using UnityEngine;

/// <summary>
/// 武器管理器：挂在 Hero 上。
/// 背包 = 一个 Weapon 数组；每个武器有自己的收纳节点，共用一个手上节点。
/// 切换时旧武器收回收纳节点，新武器拿出来（正拔刀就挂手，没收刀就挂收纳节点）。
/// </summary>
public class WeaponManager : MonoBehaviour
{
    [Header("背包（所有武器，有几把拖几把）")]
    public Weapon[] weapons;

    [Header("手上节点（手骨的子物体）")]
    public Transform handNode;

    [Header("当前用第几把（从 0 开始）")]
    public int currentIndex = 0;

    private Weapon currentWeapon;   // 当前武器
    private bool isDrawn = false;   // 当前武器在不在手上（true=拔刀 false=收刀）

    void Start()
    {
        // 修一下越界
        if (currentIndex < 0) currentIndex = 0;
        if (weapons == null || weapons.Length == 0) return;
        if (currentIndex >= weapons.Length) currentIndex = 0;

        currentWeapon = weapons[currentIndex];
        if (currentWeapon != null) currentWeapon.Sheathe();   // 一开场先收刀
        isDrawn = false;
    }

    // 换到第 index 把
    public void Equip(int index)
    {
        if (weapons == null || weapons.Length == 0) return;
        if (index < 0 || index >= weapons.Length) return;

        if (currentWeapon != null) currentWeapon.Sheathe();   // 旧的收回收纳节点

        currentIndex = index;
        currentWeapon = weapons[currentIndex];

        if (currentWeapon == null) return;

        // 本来拔着刀，就立刻把新武器拿到手上；本来收刀，就挂收纳节点
        if (isDrawn) currentWeapon.Draw(handNode);
        else currentWeapon.Sheathe();
    }

    // 切下一把（循环）
    public void NextWeapon()
    {
        if (weapons == null || weapons.Length == 0) return;
        int next = currentIndex + 1;
        if (next >= weapons.Length) next = 0;
        Equip(next);
    }

    // 切上一把（循环）
    public void PreviousWeapon()
    {
        if (weapons == null || weapons.Length == 0) return;
        int prev = currentIndex - 1;
        if (prev < 0) prev = weapons.Length - 1;
        Equip(prev);
    }

    // 拔刀动画事件：当前武器挂到手上
    public void DrawWeapon()
    {
        if (currentWeapon == null) return;
        currentWeapon.Draw(handNode);
        isDrawn = true;
    }

    // 收刀动画事件：当前武器挂回收纳节点
    public void SheatheWeapon()
    {
        if (currentWeapon == null) return;
        currentWeapon.Sheathe();
        isDrawn = false;
    }

    // 攻击当前武器
    public void AttackCurrent()
    {
        if (currentWeapon != null) currentWeapon.Attack();
    }
}
