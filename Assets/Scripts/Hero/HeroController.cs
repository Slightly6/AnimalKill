using UnityEngine;

  /// <summary>
  /// 第一人称控制器：WASD 走路 + 鼠标转视角（身体+眼睛分开的版本）。
  /// 挂在自己的"身体"（Cube）上，相机是它的子物体当眼睛。
  /// 左右转 = 转身体；上下看 = 只转相机。
  /// </summary>
  public class HeroController : MonoBehaviour
  {
      [Header("移动")]
      public float moveSpeed = 5f;        // 走路速度
      public float gravity = -9.8f;       // 重力（不浮空）

      [Header("鼠标视角")]
      public float lookSpeed = 2f;        // 鼠标灵敏度
      public float maxLookUp = 80f;       // 最多抬头/低头多少度

      [Header("眼睛（第一人称相机，是身体的子物体）")]
      public Camera playerCamera;         // 拖进来；不拖就自动找子物体里的相机

      private CharacterController controller;   // 身体碰撞
      private float pitch = 0f;                 // 抬头低头角度（只作用于相机）
      private float verticalSpeed = 0f;         // 垂直速度（重力累积）

      void Start()
      {
          controller = GetComponent<CharacterController>();

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
        if (GameProgress.currentStage != GameStage.FirstPerson)
        {
            // 过场/打牌阶段：关掉身体碰撞，让 Timeline 能自由控制 Hero 的位置
            if (controller.enabled) controller.enabled = false;
            return;
        }

        // 回到第一人称：重新开身体碰撞
        if (!controller.enabled) controller.enabled = true;

          Look();
          Move();
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

      // WASD 走路
      void Move()
      {
          float h = Input.GetAxis("Horizontal");   // A / D
          float v = Input.GetAxis("Vertical");     // W / S

          // 身体的 forward 已经是水平的（身体只有左右转），直接用，不用去掉俯仰
          Vector3 dir = transform.forward * v + transform.right * h;

          // 重力
          if (controller.isGrounded && verticalSpeed < 0f)
          {
              verticalSpeed = -2f;   // 贴地
          }
          else
          {
              verticalSpeed += gravity * Time.deltaTime;
          }

          Vector3 move = dir * moveSpeed * Time.deltaTime + Vector3.up * verticalSpeed *
  Time.deltaTime;
          controller.Move(move);
      }
  }