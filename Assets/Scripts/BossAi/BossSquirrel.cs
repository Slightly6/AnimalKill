using System.Collections;
using UnityEngine;

/// <summary>
/// Boss 松鼠 AI。
///
/// 行为：
///   登场怒吼（SIT → IDLE → ROAR）→ 前后左右随机走 + 一直面向玩家；
///   玩家出现在 Boss 正面视野角内且技能 CD 好了 → 放完整技能（固定时长，不能打断）；
///   技能放完 → 转向玩家 → 继续随机走。
///
/// 走路用代码驱动（applyRootMotion = false），动画只当「走路姿势」，
/// 所以前/后/左/右四个方向都能走（后退用镜像前进 State=7）。
/// 攻击则临时打开 root motion，把动画自带的前冲+跳起转发到根节点（BossRootMotionForwarder）。
///
/// Animator 参数：int 参数 State
///   0=左横移 1=右横移 2=前进走 3=攻击 4=坐 5=待机 6=怒吼 7=镜像前进
/// </summary>
public class BossSquirrel : MonoBehaviour
{
    public enum BossState
    {
        Intro,   // 登场怒吼
        Move,    // 前后左右随机移动
        Attack   // 技能（完整播放，不能打断）
    }

    [Header("目标")]
    public Transform player;                  // 玩家；不拖会自动找

    [Header("移动")]
    public float moveSpeed = 2f;              // 移动速度（米/秒），调成和走路动画匹配，避免脚底打滑
    public float moveDurationMin = 1.5f;      // 每次朝一个方向走多久（最小，秒）
    public float moveDurationMax = 3f;        // 每次朝一个方向走多久（最大，秒）

    [Header("转身")]
    public float turnSpeed = 6f;              // 面向玩家的转身速度

    [Header("攻击")]
    [Range(0f, 180f)]
    public float fovAngle = 160f;             // 视野角（全角）：玩家在这个角度内才触发攻击
    public float attackCooldown = 3f;         // 两次攻击的间隔（秒）
    public float attackDuration = 2f;         // 技能固定时长（秒），必须放完（含后摇，给玩家抓后摇）
    public float attackRootMotionScale = 5f;  // 攻击根位移放大倍数：动画自带位移太小，放大到合适的前冲+跳起

    [Header("Animator 参数")]
    public string stateParam = "State";

    [Header("是否进入 Boss 战")]
    public bool isPhaseTwo = false;

    private const int STRAFE_LEFT = 0;
    private const int STRAFE_RIGHT = 1;
    private const int STRUT = 2;              // 前进走路
    private const int STRUT_MIRROR = 7;       // 镜像前进（你新加的，当后退用）
    private const int ATTACK = 3;
    private const int SIT = 4;
    private const int IDLE = 5;
    private const int ROAR = 6;

    private Animator anim;
    private BossState state = BossState.Intro;

    private float cooldownTimer = 0f;         // 攻击 CD 倒计时
    private float moveTimer = 0f;             // 当前方向段剩余时间
    private float attackTimer = 0f;           // 攻击剩余时间
    private int moveType = 0;                 // 当前移动方向：0=前 1=后 2=左 3=右

    private Floor floor;                      // 地面检测器：走路时贴地，攻击时关掉不限制 Y
    private float attackStartY = 0f;          // 攻击前记录的世界 Y 坐标，攻击后复位用

    [Header("攻击偏移")]
    public float attackOffset = 1f;   // 攻击时朝玩家右边偏多少米
    public string attackStateName = "Great Sword Jump Attack";
 
    void Start()
    {
        anim = GetComponentInChildren<Animator>();
        attackStartY = transform.position.y;
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
        cooldownTimer = attackCooldown;
        moveTimer = 0f;   // 下一帧立即随机选一次方向
        if (anim != null) anim.applyRootMotion = false;   // 回走路：关掉 root motion，代码驱动
    }

    void UpdateMove()
    {
        // 1. 一直面向玩家（平滑转身，玩家基本始终在视野内）
        FacePlayer();

        // 2. CD 好了 + 玩家在视野角内 → 攻击
        cooldownTimer -= Time.deltaTime;
        if (cooldownTimer <= 0f && IsPlayerInFOV())
        {
            EnterAttack();
            return;
        }

        // 3. 这一段方向走完 → 随机换方向
        moveTimer -= Time.deltaTime;
        if (moveTimer <= 0f)
        {
            PickRandomMove();
        }

        // 4. 代码移动（方向相对 Boss 当前朝向，所以面向玩家后左右 = 绕玩家转圈）
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

    // ========== 攻击 ==========
    void EnterAttack()
    {
        state = BossState.Attack;
        attackTimer = attackDuration;
        SnapFacePlayerWithOffset();     // 出招瞬间锁定玩家位置
               // 记录攻击前世界 Y，攻击后复位用
        if (floor != null) floor.enabled = false;  // 攻击时不限制 Y，跳劈自由跳起+落地
        SetAnimSpeed(1f);
        if (anim != null) anim.applyRootMotion = true;   // 攻击时开 root motion，靠动画自带的前冲+跳起
        SetState(ATTACK);
    }
    
        void UpdateAttack()
    {
        attackTimer -= Time.deltaTime;
        
        AnimatorStateInfo info = anim.GetCurrentAnimatorStateInfo(0);
        bool animDone = info.normalizedTime >= 0.95f && !anim.IsInTransition(0);
        bool timeUp = attackTimer <= 0f;
        
        // 动画播完 或 超时 → 强制切
        if (animDone || timeUp)
        {
            Vector3 pos = transform.position;
            pos.y = attackStartY;
            transform.position = pos;
            
            if (floor != null) floor.enabled = true;
            
            EnterMove();
        }
    }

        // 攻击专用：朝玩家偏右一点
    void SnapFacePlayerWithOffset()
    {
        // 玩家位置 + 玩家自己的右边偏移
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
        if (toPlayer.magnitude < 0.001f) return;

        Quaternion targetRot = Quaternion.LookRotation(toPlayer);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
    }

    void SnapFacePlayer()
    {
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.magnitude < 0.001f) return;
        transform.rotation = Quaternion.LookRotation(toPlayer);
    }
}
