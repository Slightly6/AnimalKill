using UnityEngine;

/// <summary>
/// 武器数据（ScriptableObject）。
/// 每把武器是一个独立的 .asset 资源文件：在 Project 面板右键 → Create → 武器 → 新武器 就能新建。
///
/// 好处：加武器不用改代码，数值在 Inspector 里单独调，还能到处拖、随便复制。
/// 这里只放「数值」，不放动画名——哪套攻击播哪个动画，由动画器（HeroAnimator）的触发器管。
/// </summary>
[CreateAssetMenu(fileName = "新武器", menuName = "武器/新武器")]
public class WeaponData : ScriptableObject
{
    [Header("名字")]
    public string weaponName = "新武器";

    [Header("伤害")]
    public int damage = 10;

    [Header("攻速（倍率，1 = 正常速度）")]
    public float attackSpeed = 1f;

    [Header("攻击距离")]
    public float attackRange = 2f;

    [Header("击退力度")]
    public float knockback = 0f;
}
