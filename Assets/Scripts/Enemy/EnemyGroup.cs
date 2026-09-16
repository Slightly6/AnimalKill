using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;   // 要用 Image 做 Boss 屏幕血条

/// <summary>
/// 敌人生成器（打完牌触发第一人称战斗时，按关卡配置的敌人列表动态生成敌人）。
/// 挂在场景里一个空物体上，GameManager 触发战斗时把当前关卡的敌人列表传给它。
///
/// 敌人 prefab 有两种：Boss 用 BossSquirrel（登场/移动/攻击/转身全套 AI）；小怪用 Minion（只会追玩家+碰一下扣血，不播动画）。
/// Boss 从固定点刷；小怪在随机区域内随机位置刷。
/// Boss 死了（isBoss=true）直接发 DiedEvent 亮门；小怪死了发 MinionDiedEvent，全灭才亮门。
/// </summary>
public class EnemyGroup : MonoBehaviour
{
    [Header("Boss 固定生成点（Boss 从这里刷，位置固定）")]
    public Transform bossSpawnPoint;

    [Header("小怪随机生成区域中心（小怪在这个点周围随机刷）")]
    public Transform minionSpawnCenter;

    [Header("小怪随机生成半径（以中心为圆心，半径内随机）")]
    public float minionSpawnRadius = 3f;

    [Header("Boss 屏幕固定血条（场景 HUD 里那个 Blood 血条，Boss 战显示；小怪不用）")]
    public Image bossHpBar;

    private int remainingMinions = 0;   // 还剩几个小怪（Boss 不算，Boss 死了自己亮门）

    void Start()
    {
        EventBus.Subscribe<MinionDiedEvent>(OnMinionDied);

        // 打牌阶段血条先藏起来，Boss 战 StartBossFight 时才显示
        if (bossHpBar != null) bossHpBar.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<MinionDiedEvent>(OnMinionDied);
    }

    // 触发第一人称战斗时调用（GameManager 调）：按敌人列表动态生成
    public void StartCombat(List<EnemyData> enemies)
    {
        if (enemies == null || enemies.Count == 0)
        {
            Debug.LogWarning("EnemyGroup：当前关卡没配敌人，第一人称战斗不会生成敌人（去 LevelDatabase 填 enemies）");
            return;
        }

        remainingMinions = 0;

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyData data = enemies[i];
            if (data == null || data.enemyPrefab == null) continue;

            for (int j = 0; j < data.count; j++)
            {
                Vector3 pos;
                Quaternion rot;

                if (data.isBoss)
                {
                    // Boss：固定点生成
                    pos = bossSpawnPoint != null ? bossSpawnPoint.position : Vector3.zero;
                    rot = bossSpawnPoint != null ? bossSpawnPoint.rotation : Quaternion.identity;
                }
                else
                {
                    // 小怪：随机区域内随机位置 + 随机朝向
                    pos = RandomMinionPosition();
                    rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                }

                GameObject go = Instantiate(data.enemyPrefab, pos, rot);

                // 用数据里的 isBoss 覆盖 prefab 上的：决定死亡时发 DiedEvent（亮门）还是 MinionDiedEvent（统计）
                SquirrelHp hp = go.GetComponentInChildren<SquirrelHp>();
                if (hp != null)
                {
                    hp.isBoss = data.isBoss;
                    // Boss 绑定屏幕固定血条（小怪不绑，没血条）
                    if (data.isBoss) hp.hpFill = bossHpBar;
                }

                BossSquirrel boss = go.GetComponent<BossSquirrel>();
                if (boss != null) boss.StartBossFight();   // Boss 有登场动画；小怪（Minion）没有启动方法，生成即自动追

                // 小怪要统计数量（全灭才亮门），Boss 不用（死了直接亮门）
                if (!data.isBoss) remainingMinions++;
            }
        }
    }

    // 在随机区域内取一个随机位置（XZ 平面随机，Y 用中心点的高度，保证贴地）
    Vector3 RandomMinionPosition()
    {
        if (minionSpawnCenter == null) return Vector3.zero;

        Vector2 circle = Random.insideUnitCircle * minionSpawnRadius;   // 单位圆内随机点 × 半径
        return minionSpawnCenter.position + new Vector3(circle.x, 0f, circle.y);
    }

    // 每死一个小怪，数减一；全死光 → 发 DiedEvent 亮门（复用 DoorMap，不用改门）
    void OnMinionDied(MinionDiedEvent e)
    {
        remainingMinions--;
        if (remainingMinions <= 0)
        {
            EventBus.Publish(new DiedEvent { isPlayer = false });
        }
    }
}
