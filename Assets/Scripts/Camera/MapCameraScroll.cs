 using UnityEngine;

  /// <summary>
  /// 地图镜头滚动（挂在 Map 场景 Main Camera 上）。
  /// 进场先自动跳到「当前关卡」那一排（强制跟随），
  /// 鼠标一滚就解除跟随，转成手动滚。
  /// </summary>
  public class MapCameraScroll : MonoBehaviour
  {
      [Header("滚动速度")]
      public float scrollSpeed = 2f;

      [Header("镜头上下范围（世界 y，按地图长短调）")]
      public float minY = -10f;
      public float maxY = 10f;

      private bool follow = true;    // 是否还在强制跟随当前关卡
      private bool snapped = false;  // 是否已经跳过第一次（等节点生成完）

      void Update()
      {
          // 第一次进来：等 MapGenerator 把节点生成好（所有 Start 跑完后第一帧），
          // 找到「当前关卡」那一排的节点，把镜头跳过去。
          if (!snapped)
          {
              snapped = true;
              SnapToCurrentRow();
          }

          float wheel = Input.mouseScrollDelta.y;

          // 鼠标滚动了 → 解除强制跟随，转成手动
          if (wheel != 0f)
          {
              follow = false;
          }

          // 还在跟随 → 这一帧不动镜头
          if (follow) return;

          // 手动模式：滚轮上下移，夹在范围内
          Vector3 pos = transform.position;
          pos.y += wheel * scrollSpeed;
          pos.y = Mathf.Clamp(pos.y, minY, maxY);
          transform.position = pos;
      }

      // 找「当前关卡」那一排的节点，把镜头 Y 跳过去
      void SnapToCurrentRow()
      {
          MapNode[] nodes = FindObjectsOfType<MapNode>();
          for (int i = 0; i < nodes.Length; i++)
          {
              if (nodes[i].row == GameProgress.mapRow)
              {
                  Vector3 pos = transform.position;
                  pos.y = nodes[i].transform.position.y;
                  transform.position = pos;
                  break;
              }
          }
      }
  }