using UnityEngine;

public class HeroController : MonoBehaviour
{
    [Header("移动")]
    public float walkSpeed = 3f;
    public float runSpeed = 7f;
    public bool isMove=false;
    [Header("体力")]
    public Stamina stamina;

    [Header("鼠标视角")]
    public float lookSpeed = 2f;
    public float maxLookUp = 80f;

    [Header("眼睛")]
    public Camera playerCamera;

    [Header("动画")]
    public Animator anim;
    public float blendSmooth = 8f;
    private float currentBlend = 0f;

    [Header("相机跟头 / 藏头")]
    public Transform headBone;
    public float cameraSmooth = 0f;

    [Header("攻击（Root Motion 开关）")]
    public bool isAttacking = false;      // 攻击中：开 Root Motion，禁用移动输入
    public float attackScale = 1f;        // 攻击位移放大倍数

    [Header("第三人称视角")]
    public bool isThirdPerson = false;           // 当前是不是第三人称
    public Camera thirdPersonCamera;             // 第三人称相机（拖进来）
    public float thirdPersonDistance = 3f;       // 镜头离角色多远（米）
    public float thirdPersonHeight = 1.5f;       // 镜头相对角色的基础高度
    public float thirdPersonLookHeight = 1.5f;   // 镜头看向角色身上多高（胸口/头）
    public float thirdPersonTurnSpeed = 10f;     // 第三人称角色面向镜头前方的转身速度（越大越快）

    private float pitch = 0f;
    private float cameraYaw = 0f;                // 第三人称相机绕角色的水平角度（360 环绕）
    private AnimationEventRelay forwarder;
    [Header("撞墙检测")]
    public LayerMask wallLayer;           // 哪些层算墙
    public float wallCheckRadius = 0.5f;  // 角色半径
    public float wallCheckHeight = 2f;    // 角色高度

    void Start()
    {
        if (anim == null) anim = GetComponentInChildren<Animator>();

        if (anim != null)
        {
             anim.applyRootMotion = false;   // 默认关：走路用代码驱动

            // 给 Animator 所在物体挂一个转发器，把攻击时的根位移转到根节点
            forwarder = anim.GetComponent<AnimationEventRelay>();
            if (forwarder == null)
                forwarder = anim.gameObject.AddComponent<AnimationEventRelay>();

            forwarder.scale = attackScale;
        }

        if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        ApplyCameraMode();   // 按初始视角（第一人称）开关相机
    }

    void Update()
    {
        if (GameProgress.currentStage != GameStage.FirstPerson) return;

        // 攻击中：不接受移动输入（位移交给动画），但可以转视角
        Look();
        if (!isAttacking) Move();

        // V 键：第一/第三人称来回切换
        if (Input.GetKeyDown(KeyCode.V))
        {
            isThirdPerson = !isThirdPerson;
            if (isThirdPerson)
            {
                // 切到第三人称时，让相机先转到角色当前身后，避免镜头乱跳
                cameraYaw = transform.eulerAngles.y;
            }
            ApplyCameraMode();
        }
    }

    void LateUpdate()
    {
        bool firstPerson = GameProgress.currentStage == GameStage.FirstPerson;
        if (headBone == null) return;

        if (firstPerson && !isThirdPerson)
        {
            // 第一人称：藏头，相机贴头
            headBone.localScale = Vector3.zero;

            if (playerCamera != null)
            {
                if (cameraSmooth > 0f)
                {
                    Vector3 targetPos = headBone.position + headBone.forward * 0.5f;
                    playerCamera.transform.position = Vector3.Lerp(
                        playerCamera.transform.position, targetPos, cameraSmooth * Time.deltaTime);
                }
                else
                {
                    playerCamera.transform.position = headBone.position;
                }
            }
        }
        else
        {
            // 第三人称（或非第一人称阶段）：露头
            headBone.localScale = Vector3.one;

            // 第一人称阶段 + 第三人称视角 → 更新环绕相机
            if (firstPerson && isThirdPerson)
            {
                UpdateThirdPersonCamera();
            }
        }
    }

    void Look()
    {
        float mouseX = Input.GetAxis("Mouse X") * lookSpeed;
        float mouseY = Input.GetAxis("Mouse Y") * lookSpeed;

        if (isThirdPerson)
        {
            // 第三人称：鼠标左右转「相机」绕角色 360 转，上下调俯仰；
            // 角色本身不跟鼠标转（转角色交给移动时的转身）
            cameraYaw += mouseX;
            pitch -= mouseY;
            pitch = Mathf.Clamp(pitch, -maxLookUp, maxLookUp);
        }
        else
        {
            // 第一人称：鼠标左右转「角色」，上下调俯仰
            transform.Rotate(0f, mouseX, 0f);

            pitch -= mouseY;
            pitch = Mathf.Clamp(pitch, -maxLookUp, maxLookUp);
            playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }

    void Move()
    {
        
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        if(h>0&&v>0)
        {
            isMove=true;
        }else
        {
            isMove=false;
        }
        bool wantRun = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && (h != 0f || v != 0f);
        bool running = wantRun && (stamina == null || stamina.canRun);

        if (stamina != null) stamina.isRunning = running;

        if (anim != null)
        {
            anim.SetFloat("MoveX", h);
            anim.SetFloat("MoveY", v);

            float targetSpeed = 0f;
            if (h != 0f || v != 0f) targetSpeed = running ? 1f : 0.5f;
            currentBlend = Mathf.MoveTowards(currentBlend, targetSpeed, blendSmooth * Time.deltaTime);
            anim.SetFloat("Speed", currentBlend);
        }

        Vector3 dir;
        if (isThirdPerson && thirdPersonCamera != null)
        {
            // 第三人称：移动方向相对「镜头」，把镜头朝向投影到地面（去掉 Y）
            Vector3 camForward = thirdPersonCamera.transform.forward;
            camForward.y = 0f;
            camForward.Normalize();
            Vector3 camRight = thirdPersonCamera.transform.right;
            camRight.y = 0f;
            camRight.Normalize();
            dir = camForward * v + camRight * h;

            // 第三人称：角色始终面朝镜头前方（和第一人称一样，S 后退时角色不掉头转身，
            // 而是直接朝镜头后方倒退，靠 MoveY=-1 播后退动画）
            Quaternion targetRot = Quaternion.LookRotation(camForward);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, thirdPersonTurnSpeed * Time.deltaTime);
        }
        else
        {
            // 第一人称：移动方向相对角色朝向
            dir = transform.forward * v + transform.right * h;
        }
        if (dir.magnitude > 1f) dir.Normalize();

        float speed = running ? runSpeed : walkSpeed;

        Vector3 move = dir * speed * Time.deltaTime;
        move = ClampMoveByWall(move);   // ★ 撞墙检测
        transform.position += move;

    }

    // ========== 第一/第三人称切换 ==========

    // 按当前视角开关相机（第一人称关第三人称，反之亦然）
    void ApplyCameraMode()
    {
        if (playerCamera != null) playerCamera.enabled = !isThirdPerson;
        if (thirdPersonCamera != null) thirdPersonCamera.enabled = isThirdPerson;
    }

    // 第三人称环绕相机：以角色为圆心，按 cameraYaw（水平 360）+ pitch（上下）绕角色转
    void UpdateThirdPersonCamera()
    {
        if (thirdPersonCamera == null) return;

        // 环绕偏移：先往身后 -Z 拉 thirdPersonDistance，再抬高 thirdPersonHeight
        Vector3 offset = new Vector3(0f, thirdPersonHeight, -thirdPersonDistance);

        // 用 cameraYaw（水平 360）+ pitch（上下）旋转偏移，镜头绕角色任意转
        offset = Quaternion.Euler(pitch, cameraYaw, 0f) * offset;

        // 相机放到「角色 + 偏移」的世界位置
        thirdPersonCamera.transform.position = transform.position + offset;

        // 看向角色身上（胸口/头的高度）
        thirdPersonCamera.transform.LookAt(transform.position + Vector3.up * thirdPersonLookHeight);
    }

    // ========== 攻击相关：由动画事件或代码调用 ==========

    /// <summary>攻击开始：开 Root Motion，锁定移动输入</summary>
    public void StartAttack()
    {
        isAttacking = true;
        if (anim != null) anim.applyRootMotion = true;
    }

    /// <summary>攻击结束：关 Root Motion，恢复移动</summary>
    public void EndAttack()
    {
        isAttacking = false;
        if (anim != null) anim.applyRootMotion = false;
    }
    // 返回“经过撞墙检测后的实际位移”
    Vector3 ClampMoveByWall(Vector3 desiredMove)
    {
        if (desiredMove.magnitude < 0.001f) return desiredMove;

        Vector3 dir = desiredMove.normalized;
        float dist = desiredMove.magnitude;

        if (Physics.CapsuleCast(
            transform.position + Vector3.up * wallCheckRadius,
            transform.position + Vector3.up * (wallCheckHeight - wallCheckRadius),
            wallCheckRadius,
            dir,
            out RaycastHit hit,
            dist + 0.1f,
            wallLayer))
        {
            // 撞墙，砍到墙前面
            return dir * Mathf.Max(0f, hit.distance - 0.1f);
        }

        return desiredMove;   // 没撞墙，原样返回
    }
}