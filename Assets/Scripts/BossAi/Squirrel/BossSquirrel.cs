using System.Collections;
using UnityEngine;

/// <summary>
/// Boss 松鼠 AI。
///
/// 行为：
///   登场怒吼（SIT → IDLE → ROAR）→ 前后左右随机走 + 一直面向玩家；
///   玩家进入视野、进入某个攻击的距离范围、且该攻击 CD 好了 → 按距离放 4 个攻击之一；
///   攻击播完 → 切回 idle 转向玩家 → 继续随机走。
///
/// 走路用代码驱动（applyRootMotion = false），动画只当「走路姿势」。
/// 攻击则临时打开 root motion，把动画自带的前冲+跳起转发到根节点（BossRootMotionForwarder）。
///
/// Animator 参数：int 参数 State
///   0=左横移 1=右横移 2=前进走 4=坐 5=待机 6=怒吼 7=镜像前进
///   攻击动画的 State 值在 AttackConfig 里配（stateValue）
/// </summary>
public class BossSquirrel : MonoBehaviour
{
    public enum BossState
    {
        Intro,   // 登场怒吼
        Move,    // 前后左右随机移动
        Attack,  // 放攻击动画（root motion 驱动位移）
        Turn     // 转向（原地慢慢面向玩家）
    }

    [Header("攻击配置（近→远排）")]
    public AttackConfig[] attacks;         // 4 个攻击，每个带自己的距离/CD
    private float[] cooldownTimers;        // 每个攻击自己的 CD 倒计时

    [Header("CD 联动")]
    public float farExtraCd = 3f;          // 放完近/中攻击，给「远组」多加的 CD（秒）
    public float nearMidExtraCd = 2f;      // 放完远攻击，给「近/中组」多加的 CD（秒）

    [Header("目标")]
    public Transform player;               // 玩家；不拖会自动找

    [Header("移动")]
    public float moveSpeed = 2f;           // 移动速度（米/秒），调成和走路动画匹配，避免脚底打滑
    public float moveDurationMin = 1.5f;   // 每次朝一个方向走多久（最小，秒）
    public float moveDurationMax = 3f;     // 每次朝一个方向走多久（最大，秒）

    [Header("转身")]
    public float turnSpeed = 3f;           // 面向玩家的转身速度

    [Header("攻击")]
    [Range(0f, 180f)]
    public float fovAngle = 160f;          // 视野角（全角）：玩家在这个角度内才触发攻击

    [Header("Animator 参数")]
    public string stateParam = "State";

    [Header("是否进入 Boss 战")]
    public bool isPhaseTwo = false;

    private const int STRAFE_LEFT = 0;     // 左横移
    private const int STRAFE_RIGHT = 1;    // 右横移
    private const int STRUT = 2;           // 前进走
    private const int STRUT_MIRROR = 7;    // 镜像前进（当后退用）
    private const int SIT = 4;             // 坐
    private const int IDLE = 5;            // 待机
    private const int ROAR = 6;            // 怒吼

    private Animator anim;
    private BossState state = BossState.Intro;

    private float moveTimer = 0f;          // 当前方向段剩余时间
    private int moveType = 0;              // 当前移动方向：0=前 1=后 2=左 3=右

    private Floor floor;                   // 地面检测器：走路贴地，攻击时关掉不限制 Y
    private float attackStartY = 0f;       // 攻击前记录的世界 Y 坐标，攻击后复位用

    [Header("攻击偏移")]
    public float attackOffset = 1f;        // 攻击时朝玩家右边偏多少米

    void Start()
    {
        attackStartY = transform.position.y;           // 记录攻击前 Y，攻击后复位用
        anim = GetComponentInChildren<Animator>();

        // 初始化每个攻击的 CD 倒计时（0 = 一进场就能打）
        cooldownTimers = new float[attacks.Length];
        for (int i = 0; i < cooldownTimers.Length; i++)
        {
            cooldownTimers[i] = 0f;
        }

        if (player == null)
        {
            HeroController hero = FindObjectOfType<HeroController>();
            if (hero != null) player = hero.transform;
        }

        if (anim != null)
        {
            anim.applyRootMotion = false;   // 默认关：走路用代码驱动

            // 攻击时要靠动画自带的根位移（前冲+跳起），
            // 给 Animator 所在物体挂个转发器，把根位移转到根节点（HitBox 一起跟着走）
            BossRootMotionForwarder forwarder = anim.GetComponent<BossRootMotionForwarder>();
            if (forwarder == null)
            {
                forwarder = anim.gameObject.AddComponent<BossRootMotionForwarder>();
            }
            forwarder.root = transform;
        }

        SetupCollision();

        floor = GetComponent<Floor>();   // 地面检测器：走路贴地，攻击时关掉

        if (player != null) SnapFacePlayer();

        SetState(SIT);
    }

    // 只把 HitBox 配成触发器（玩家打中判定用）。
    // 不再加 CharacterController：移动改成代码驱动，直接用 transform 位移。
    void SetupCollision()
    {
        Rigidbody hitRb = GetComponentInChildren<Rigidbody>();
        if (hitRb != null)
        {
            BoxCollider hitBox = hitRb.GetComponent<BoxCollider>();
            if (hitBox != null) hitBox.isTrigger = true;
            hitRb.isKinematic = true;
        }
    }

    public void StartBossFight()
    {
        isPhaseTwo = true;
        StartCoroutine(PlayIntro());
    }

    void Update()
    {
        if (player == null || anim == null) return;
        if (!isPhaseTwo) return;

        switch (state)
        {
            case BossState.Intro: break;
            case BossState.Move: UpdateMove(); break;
            case BossState.Attack: UpdateAttack(); break;
            case BossState.Turn: UpdateTurn(); break;
        }
    }

    // ========== 登场怒吼 ==========
    IEnumerator PlayIntro()
    {
        state = BossState.Intro;
        SetAnimSpeed(1f);

        SetState(SIT);
        yield return new WaitForSeconds(2f);

        SetState(IDLE);
        yield return new WaitForSeconds(1f);

        SetState(ROAR);
        yield return new WaitForSeconds(3f);

        EnterMove();
    }

    // ========== 前后左右随机移动 ==========
    void EnterMove()
    {
        state = BossState.Move;
        moveTimer = 0f;   // 下一帧立即随机选一次方向
        if (anim != null) anim.applyRootMotion = false;   // 回走路：关掉 root motion，代码驱动
    }

    void UpdateMove()
    {
        // 1. 一直面向玩家（平滑转身，玩家基本始终在视野内）
        FacePlayer();

        // 2. CD 全体倒计时（只在 CD 中才减，减到 0 就停，别减成负数，
        //    否则后面「+N 秒 CD」的跨组惩罚加到负数上会失效）
        for (int i = 0; i < cooldownTimers.Length; i++)
        {
            if (cooldownTimers[i] > 0f)
            {
                cooldownTimers[i] -= Time.deltaTime;
            }
        }

        // 3. 玩家在视野内 + 距离匹配某个攻击 + 该攻击 CD 好了 → 攻击
        if (IsPlayerInFOV())
        {
            // 只算水平距离（忽略 Y），不然 Boss 和玩家高度不一样会把距离算大
            Vector3 toPlayer = player.position - transform.position;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;

            int idx = SelectAttack(distance);
            if (idx >= 0)
            {
                EnterAttack(idx);
                return;
            }
        }

        // 4. 这一段方向走完 → 随机换方向
        moveTimer -= Time.deltaTime;
        if (moveTimer <= 0f)
        {
            PickRandomMove();
        }

        // 5. 代码移动（方向相对 Boss 当前朝向，所以面向玩家后左右 = 绕玩家转圈）
        transform.position += GetMoveDir() * moveSpeed * Time.deltaTime;
    }

    // 随机选一个方向（前/后/左/右）并切对应动画
    void PickRandomMove()
    {
        moveTimer = Random.Range(moveDurationMin, moveDurationMax);
        moveType = Random.Range(0, 4);   // 0=前 1=后 2=左 3=右

        if (moveType == 1)               // 后：用镜像前进动画（State=7），边面朝玩家边后撤
        {
            SetAnimSpeed(1f);
            SetState(STRUT_MIRROR);
        }
        else if (moveType == 2)          // 左
        {
            SetAnimSpeed(1f);
            SetState(STRAFE_LEFT);
        }
        else if (moveType == 3)          // 右
        {
            SetAnimSpeed(1f);
            SetState(STRAFE_RIGHT);
        }
        else                             // 前
        {
            SetAnimSpeed(1f);
            SetState(STRUT);
        }
    }

    // 根据当前朝向算移动方向（每帧现算，保证「左右」相对玩家方向不变）
    Vector3 GetMoveDir()
    {
        if (moveType == 0) return transform.forward;     // 前：朝玩家
        if (moveType == 1) return -transform.forward;    // 后：远离玩家
        if (moveType == 2) return -transform.right;      // 左
        return transform.right;                          // 右
    }

    // 距离选攻击：在「CD 好了 + 距离落在范围内」的攻击里挑；同一段有多个就随机挑一个
    int SelectAttack(float distance)
    {
        int[] ready = new int[attacks.Length];
        int count = 0;
        for (int i = 0; i < attacks.Length; i++)
        {
            AttackConfig a = attacks[i];
            if (cooldownTimers[i] > 0f) continue;                        // CD 没好
            if (distance < a.minDistance || distance > a.maxDistance) continue; // 距离不对
            ready[count] = i;
            count++;
        }
        if (count == 0) return -1;             // 没有能放的
        return ready[Random.Range(0, count)];  // 多个随机挑一个
    }

    // ========== 攻击 ==========
    void EnterAttack(int idx)
    {
        state = BossState.Attack;

        AttackConfig cast = attacks[idx];
        int castGroup = cast.groupId;

        // 1. 共享 CD：同 groupId 的攻击一起进 CD（最远两个 groupId 都是 2 → 共享一个 CD）
        for (int i = 0; i < attacks.Length; i++)
        {
            if (attacks[i].groupId == castGroup)
            {
                cooldownTimers[i] = cast.cooldown;
            }
        }

        // 2. 跨组惩罚
        if (castGroup == 2)   // 放的是最远攻击 → 近/中组各加 nearMidExtraCd
        {
            for (int i = 0; i < attacks.Length; i++)
            {
                if (attacks[i].groupId != 2)
                {
                    cooldownTimers[i] += nearMidExtraCd;
                }
            }
        }
        else                  // 放的是近/中攻击 → 远组加 farExtraCd
        {
            for (int i = 0; i < attacks.Length; i++)
            {
                if (attacks[i].groupId == 2)
                {
                    cooldownTimers[i] += farExtraCd;
                }
            }
        }


        if (cast.useOffset)
        {
            SnapFacePlayerWithOffset();   // 旋风劈：朝玩家右侧偏移 attackOffset 米出招
        }
        else
        {
            SnapFacePlayer();             // 其他三个：正对玩家当前位置出招
        }

        if (floor != null) floor.enabled = false;      // 攻击时不限制 Y，跳劈自由跳起+落地
        SetAnimSpeed(1f);
        if (anim != null) anim.applyRootMotion = true; // 攻击时开 root motion，靠动画自带位移
        SetState(cast.stateValue);                     // 切这个攻击动画
    }

    void UpdateAttack()
    {
        // 攻击动画播完（非循环动画 normalizedTime 会停在 1）→ 切回 idle 转向玩家。
        // 注意：攻击状态必须是「非循环」且「没有自动 exit 到 idle 的转换」，
        //       否则播不到最后就切走了，这里永远检测不到。
        AnimatorStateInfo info = anim.GetCurrentAnimatorStateInfo(0);
        bool animDone = info.normalizedTime >= 0.99f && !anim.IsInTransition(0);

        if (animDone)
        {
            // 攻击后世界 Y 复位 + 重新贴地
            Vector3 pos = transform.position;
            pos.y = attackStartY;
            transform.position = pos;
            if (floor != null) floor.enabled = true;

            EnterTurn();   // 直接切 idle 转向玩家（不再后摇顿住）
        }
    }

    // ========== 转向（切 idle，原地慢慢面向玩家，进了视野就停） ==========
    void EnterTurn()
    {
        state = BossState.Turn;
        if (anim != null) anim.applyRootMotion = false;   // 攻击结束，关 root motion，回代码驱动
        SetState(IDLE);   // 切回 idle
    }

    void UpdateTurn()
    {
        FacePlayer();                 // 慢慢转向
        if (IsPlayerInFOV())          // 玩家进视野了 → 开始移动
        {
            EnterMove();
        }
    }

    // 攻击专用：朝玩家偏右一点
    void SnapFacePlayerWithOffset()
    {
        Vector3 offset = player.right * attackOffset;
        Vector3 targetPos = player.position + offset;

        Vector3 toPlayer = targetPos - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.magnitude < 0.001f) return;
        transform.rotation = Quaternion.LookRotation(toPlayer);
    }

    // ========== 小工具 ==========
    void SetState(int value)
    {
        if (anim == null) return;
        anim.SetInteger(stateParam, value);
    }

    void SetAnimSpeed(float s)
    {
        if (anim != null) anim.speed = s;
    }

    // 玩家是否在 Boss 正面 fovAngle 度视野内（fovAngle 是全角，取一半算左右各多少度）
    bool IsPlayerInFOV()
    {
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.magnitude < 0.001f) return true;

        float angle = Vector3.Angle(transform.forward, toPlayer);
        return angle <= fovAngle * 0.5f;
    }

    void FacePlayer()
    {
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.magnitude < 0.1f) return;

        Quaternion targetRot = Quaternion.LookRotation(toPlayer);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
    }

    void SnapFacePlayer()
    {
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.magnitude < 0.1f) return;
        transform.rotation = Quaternion.LookRotation(toPlayer);
    }
}
