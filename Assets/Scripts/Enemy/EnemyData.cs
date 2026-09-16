using UnityEngine;

/// <summary>
/// 敌人数据（ScriptableObject）。
/// 每个敌人是一份 .asset：在 Project 面板右键 → Create → 敌人 → 新敌人 就能新建。
///
/// 跟武器（WeaponData）、卡牌（CardDataSO）一样是「填数据不用改代码」：
/// 关卡配置（LevelConfig）里填这份敌人数据，打完牌触发第一人称战斗时就按它动态生成敌人。
/// Boss 关填 Boss 数据（isBoss 勾上），普通关填小怪数据（可以 count 填多个）。
/// </summary>
[CreateAssetMenu(fileName = "新敌人", menuName = "敌人/新敌人")]
public class EnemyData : ScriptableObject
{
    [Header("名字")]
    public string enemyName = "小怪";

    [Header("敌人预制体（复用 BossSquirrel AI 的 prefab：Boss 拖 OneBoss(A)，小怪拖小怪 prefab）")]
    public GameObject enemyPrefab;

    [Header("是不是 Boss（true=Boss 死了直接亮门；false=小怪，要全灭才亮门）")]
    public bool isBoss = false;

    [Header("生成几个（Boss 填 1，小怪可以填多个）")]
    public int count = 1;
}
