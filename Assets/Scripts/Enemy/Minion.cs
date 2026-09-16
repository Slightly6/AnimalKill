using UnityEngine;

/// <summary>
/// 小怪 AI（最简单版）：追玩家 + 碰到玩家扣血，不播动画、不用 Animator。
/// 挂在「不用动画」的小怪 prefab 上，配合 SquirrelHp（isBoss 不勾）当血量 + 死亡广播。
///
/// 模型是小怪.fbx 直接拖进去就行，不用配 Humanoid、不用 Animator Controller。
/// 伤害/间隔都在 Inspector 里调，不用改代码。
/// </summary>
public class Minion : MonoBehaviour
{
    [Header("追玩家的速度（米/秒）")]
    public float moveSpeed = 2f;

    [Header("离玩家多近就停（别贴脸穿模）")]
    public float stopDistance = 0.8f;

    [Header("碰到玩家多近就扣血")]
    public float attackRange = 1.5f;

    [Header("碰一下扣多少血（你想调就在这里改）")]
    public float damage = 10f;

    [Header("扣血间隔（秒），别每帧都扣")]
    public float attackInterval = 1f;

    [Header("玩家（不拖会自动找）")]
    public Transform player;

    private HeroHp heroHp;          // 玩家血量（扣血用）
    private float attackTimer = 0f; // 扣血倒计时

    void Start()
    {
        // 自动找玩家和玩家血量
        if (player == null)
        {
            HeroController hero = FindObjectOfType<HeroController>();
            if (hero != null) player = hero.transform;
        }
        heroHp = FindObjectOfType<HeroHp>();
    }

    void Update()
    {
        if (player == null) return;

        // 只算水平方向，别让高度差参与（玩家和小怪可能不在同一高度）
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        float dist = toPlayer.magnitude;

        // 扣血倒计时递减
        if (attackTimer > 0f) attackTimer -= Time.deltaTime;

        // 碰到玩家：到了扣血间隔就扣一下，然后站住继续贴（不追了）
        if (dist < attackRange)
        {
            if (attackTimer <= 0f)
            {
                if (heroHp != null) heroHp.TakeDamage(damage);
                attackTimer = attackInterval;
            }
            return;
        }

        // 太近就停，别一直抖
        if (dist < stopDistance) return;

        // 面向玩家 + 朝玩家走（水平移动，Y 不变 = 贴地走）
        transform.rotation = Quaternion.LookRotation(toPlayer);
        transform.position += toPlayer.normalized * moveSpeed * Time.deltaTime;
    }
}
