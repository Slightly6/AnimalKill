using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 武器数据（ScriptableObject）。
/// 每把武器是一个独立的 .asset 资源文件：在 Project 面板右键 → Create → 武器 → 新武器 就能新建。
///
/// 好处：加武器不用改代码，数值在 Inspector 里单独调，还能到处拖、随便复制。
/// 这里只放「数值」，不放动画名——哪套攻击播哪个动画，由动画器（HeroAnimator）的触发器管。
/// </summary>
// 武器类型：近战刀类 / 远程弓箭
public enum WeaponType
{
    Melee,    // 近战（刀类）
    Ranged    // 远程（弓箭）
}

[CreateAssetMenu(fileName = "新武器", menuName = "武器/新武器")]
public class WeaponData : ScriptableObject
{
    [Header("图标")]
    public Sprite Picture;
    [Header("名字")]
    public string weaponName = "新武器";

    [Header("类型（近战刀类 / 远程弓箭）")]
    public WeaponType weaponType = WeaponType.Melee;

    [Header("模型（3D 武器模型预制体，换武器时实例化它挂到手上）")]
    public GameObject weaponModel;

    [Header("挂载偏移：模型挂到手上/背上后，相对那个节点的位置修正")]
    public Vector3 mountOffset = Vector3.zero;

    [Header("挂载旋转：模型挂上去后多转多少度（欧拉角，0 = 保持模型原本方向）")]
    public Vector3 mountRotation = Vector3.zero;

    [Header("挂载缩放：在模型原本大小的基础上再乘多少倍（1 = 保持原大小）")]
    public float mountScale = 1f;

    [Header("伤害")]
    public int damage = 10;

    [Header("攻速（倍率，1 = 正常速度）")]
    public float attackSpeed = 1f;

    [Header("攻击距离")]
    public float attackRange = 2f;

    [Header("击退力度")]
    public float knockback = 0f;
}
