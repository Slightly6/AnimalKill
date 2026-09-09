using UnityEngine;

  /// <summary>
  /// 桌面摄像机：透视 + 滚轮在多个预设机位之间循环切换。
  /// 挂在 Main Camera 上。
  /// 滚轮向上 → 下一个机位；向下 → 上一个机位（循环）。
  /// </summary>
  public class CameraRig : Singleton<CameraRig>
  {
      [Header("多个预设机位（世界坐标，想加几个加几个）")]
      public Vector3[] positions = new Vector3[]
      {
          new Vector3(0f, 3.2f, 6.5f),   // 机位1：低、靠后，看远端
          new Vector3(0f, 8.0f, 3.0f)    // 机位2：高、靠中，俯瞰全桌
      };

      [Header("看向桌面中心")]
      public Vector3 focusPoint = new Vector3(0f, 0f, -1f);

      [Header("镜头参数")]
      public float fieldOfView = 55f;   // 透视视野
      public float lerpSpeed = 6f;      // 机位切换速度（越大越快）

      [Header("桌面高度（和 BoardManager 保持一致）")]
      public float boardHeight = 10f;

      private Camera cam;
      private int currentIndex = 0;     // 当前是第几个机位

      void Start()
      {
          cam = GetComponent<Camera>();
          cam.orthographic = false;     // 强制透视
          cam.fieldOfView = fieldOfView;
          if (positions != null && positions.Length > 0)
          {
              transform.position = positions[0] + Vector3.up * boardHeight;   // 一上来待在第一个机位
          }
      }

      void Update()
      {
          if (GameProgress.currentNodeType == NodeType.Upgrade) return;   // 非战斗节点不动相机
          if (positions == null || positions.Length == 0) return;

          float wheel = Input.mouseScrollDelta.y;

          // 滚轮向上 → 下一个机位；向下 → 上一个机位（循环）
          if (wheel > 0f)
          {
              currentIndex++;
              if (currentIndex >= positions.Length) currentIndex = 0;
          }
          else if (wheel < 0f)
          {
              currentIndex--;
              if (currentIndex < 0) currentIndex = positions.Length - 1;
          }
      }

      void LateUpdate()
      {
          if (GameProgress.currentNodeType == NodeType.Upgrade) return;   // 非战斗节点不动相机
          if (positions == null || positions.Length == 0) return;

          // 位置往当前机位靠（帧率相关平滑，平民写法）
          Vector3 targetPos = positions[currentIndex] + Vector3.up * boardHeight;
          transform.position = Vector3.Lerp(transform.position, targetPos, lerpSpeed * Time.deltaTime);

          // 始终看向桌面中心
          transform.LookAt(focusPoint + Vector3.up * boardHeight, Vector3.up);
      }
  }