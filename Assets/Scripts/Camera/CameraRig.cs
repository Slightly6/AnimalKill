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

      [Header("震屏")]
      public float shakeMaxOffset = 0.12f;   // 满 trauma 时的最大位移
      public float shakeMaxRoll = 1.5f;      // 满 trauma 时的最大 Z 旋转（度）
      public float shakeDecay = 1.8f;       // trauma 衰减速度（越大停得越快）

      private Camera cam;
      private int currentIndex = 0;     // 当前是第几个机位
      private float trauma;             // 震屏强度 0~1，命中时叠加，自动衰减

      /// <summary>命中时调用：0.3 小震，0.7 击杀大震</summary>
      public void AddShake(float amount)
      {
          trauma = Mathf.Clamp01(trauma + amount);
      }

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
          if (GameProgress.InputLocked) return;   // 卷轴地图打开/切关过渡：滚轮归 ScrollRect，禁止切机位
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
          // trauma 用真实时间衰减，这样命中停顿（timeScale≈0）期间震动不会卡住
          if (trauma > 0f)
              trauma = Mathf.Max(0f, trauma - Time.unscaledDeltaTime * shakeDecay);

          if (positions == null || positions.Length == 0) return;

          Vector3 targetPos;

          if (GameProgress.mapOpen)
          {
              // 卷轴地图打开：强制降到玩家第一视角机位（positions[0]：低位、靠后、坐在桌前），
              // currentIndex 不动 → 关图后镜头自然 lerp 回原机位
              targetPos = positions[0] + Vector3.up * boardHeight;
          }
          else
          {
              if (GameProgress.currentNodeType == NodeType.Upgrade) return;   // 非战斗节点不动相机
              targetPos = positions[currentIndex] + Vector3.up * boardHeight;
          }

          // 位置往目标机位靠
          transform.position = Vector3.Lerp(transform.position, targetPos, lerpSpeed * Time.deltaTime);

          // 始终看向桌面中心
          transform.LookAt(focusPoint + Vector3.up * boardHeight, Vector3.up);

          // 震屏：trauma² 让小震动克制、大震动猛烈。Perlin 噪声比随机抖动平滑。
          // 地图打开时不叠震屏，保持看地图时镜头稳定。
          if (trauma > 0.001f && !GameProgress.mapOpen)
          {
              float t = Time.unscaledTime * 25f;
              float mag = trauma * trauma;
              float ox = (Mathf.PerlinNoise(t, 0f) - 0.5f) * 2f * mag * shakeMaxOffset;
              float oy = (Mathf.PerlinNoise(0f, t) - 0.5f) * 2f * mag * shakeMaxOffset;
              float roll = (Mathf.PerlinNoise(t, 10f) - 0.5f) * 2f * mag * shakeMaxRoll;
              transform.position += new Vector3(ox, oy, 0f);
              transform.Rotate(Vector3.forward, roll);
          }
      }
  }