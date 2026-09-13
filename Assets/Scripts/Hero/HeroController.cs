using UnityEngine;

  /// <summary>
  /// 第一人称控制器：WASD 走路 + 鼠标转视角（身体+眼睛分开的版本）。
  /// 挂在自己的"身体"（Cube）上，相机是它的子物体当眼睛。
  /// 左右转 = 转身体；上下看 = 只转相机。
  /// 移动用 transform 直接位移（不用 CharacterController），跟 Boss 一致。
  /// </summary>
  public class HeroController : MonoBehaviour
  {
      [Header("移动")]
      public float walkSpeed = 3f;        // 走路速度
      public float runSpeed = 7f;         // 跑步速度（按 Shift）

      [Header("体力")]
      public Stamina stamina;              // 体力脚本（挂同一物体，拖进来；不拖也能跑）

      [Header("鼠标视角")]
      public float lookSpeed = 2f;        // 鼠标灵敏度
      public float maxLookUp = 80f;       // 最多抬头/低头多少度

      [Header("眼睛（第一人称相机，是身体的子物体）")]
      public Camera playerCamera;         // 拖进来；不拖就自动找子物体里的相机

      [Header("动画")]
      public Animator anim;               // 拖进来；不拖自动找子物体里的 Animator
      public float blendSmooth = 8f;      // 走/跑切换的平滑速度（越大越跟手）
      private float currentBlend = 0f;    // 当前 Speed 参数（0待机 0.5走 1跑）

      [Header("相机跟头 / 藏头（第一人称）")]
      public Transform headBone;           // 拖 mixamorig:Head
      public float cameraSmooth = 0f;      // 0=完全贴头(稳)，设5~10=更平但略延迟

      private float pitch = 0f;           // 抬头低头角度（只作用于相机）

      void Start()
      {
          // 没手动拖 Animator，就自动找子物体里的
          if (anim == null)
          {
              anim = GetComponentInChildren<Animator>();
              anim.applyRootMotion = false;  
          }

          // 没手动拖相机，就自动找子物体里的 Camera
          if (playerCamera == null)
          {
              playerCamera = GetComponentInChildren<Camera>();
          }

          // 锁鼠标
          Cursor.lockState = CursorLockMode.Locked;
          Cursor.visible = false;
      }

      void Update()
      {
          // 只在第一人称能操作；过场(Cutscene)/打牌(Playing)时锁住，不能动不能转视角
          if (GameProgress.currentStage != GameStage.FirstPerson) return;

          Look();
          Move();
      }

      // 相机贴头（防晃）+ 藏头/恢复头：第一人称藏头贴头，切第三人称恢复
      void LateUpdate()
      {
          bool firstPerson = GameProgress.currentStage == GameStage.FirstPerson;

          if (headBone == null) return;   // 没拖头骨就不处理（相机停在原地）

          if (firstPerson)
          {
              // 第一人称：把头骨压成0，头这张皮塌进脖子顶看不见，身体(胸/手/腿)还在
              headBone.localScale = Vector3.zero;

              // 相机只跟头的位置、不跟头的旋转，在眼睛高度又不会被头骨动画带得乱晃
              if (playerCamera != null)
              {
                  if (cameraSmooth > 0f)
                  {
                      Vector3 targetPos = headBone.position + headBone.forward * 0.5f;

                        playerCamera.transform.position = Vector3.Lerp(
                            playerCamera.transform.position,
                            targetPos,
                            cameraSmooth * Time.deltaTime
                        );
                  }
                  else
                  {
                      playerCamera.transform.position = headBone.position;
                  }
              }
          }
          else
          {
              // 第三人称/过场：把头恢复正常大小
              headBone.localScale = Vector3.one;
          }
      }

      // 鼠标转视角
      void Look()
      {
          float mouseX = Input.GetAxis("Mouse X") * lookSpeed;
          float mouseY = Input.GetAxis("Mouse Y") * lookSpeed;

          // 左右转：转整个身体（身体 + 眼睛一起转）
          transform.Rotate(0f, mouseX, 0f);

          // 上下看：只转相机（身体不动，所以不会翻跟头）
          pitch -= mouseY;
          pitch = Mathf.Clamp(pitch, -maxLookUp, maxLookUp);
          playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
      }

      // WASD 走路 / Shift 跑
      void Move()
      {
          float h = Input.GetAxis("Horizontal");   // A / D
          float v = Input.GetAxis("Vertical");     // W / S

          // 想跑 = 按了 Shift 且真的在动；能不能跑 = 还有体力
          bool wantRun = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && (h != 0f || v != 0f);
          bool running = wantRun && (stamina == null || stamina.canRun);   // 没挂体力脚本就永远能跑

          // 告诉体力脚本现在在不在跑（它靠这个扣/回体力）
          if (stamina != null) stamina.isRunning = running;

          // 混合树参数：MoveX=左右(-1左/+1右)  MoveY=前后(+1前/-1后)
          if (anim != null)
          {
              anim.SetFloat("MoveX", h);
              anim.SetFloat("MoveY", v);
              // Speed：0=待机 0.5=走 1=跑，平滑过渡别一帧硬跳
              float targetSpeed = 0f;
              if (h != 0f || v != 0f) targetSpeed = running ? 1f : 0.5f;
              currentBlend = Mathf.MoveTowards(currentBlend, targetSpeed, blendSmooth * Time.deltaTime);
              anim.SetFloat("Speed", currentBlend);
          }

          // 身体的 forward 已经是水平的（身体只有左右转），直接用，不用去掉俯仰
          Vector3 dir = transform.forward * v + transform.right * h;
          if (dir.magnitude > 1f) dir.Normalize();   // 斜着走别变快

          // 实际移动速度：跑快走慢
          float speed = running ? runSpeed : walkSpeed;

          // 直接用 transform 位移（不用 CharacterController）：只动 XZ，Y 保持不动
          transform.position += dir * speed * Time.deltaTime;
      }
  }
