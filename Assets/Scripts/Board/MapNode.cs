using UnityEngine;
using UnityEngine.SceneManagement;

  /// <summary>
  /// 地图节点（挂在 Map 场景里手动摆的节点上，带 BoxCollider2D）。
  /// 点击 → 记录选的关卡 → 跳回战斗场景。
  /// </summary>
  [RequireComponent(typeof(BoxCollider2D))]
  public class MapNode : MonoBehaviour
  {
      public int levelIndex = 0;                      // 对应第几关（0~51）
      public string battleSceneName = "SampleScene";  // 战斗场景名

      void Awake()
      {
          BoxCollider2D col = GetComponent<BoxCollider2D>();
          col.isTrigger = true;
      }

      void OnMouseDown()
      {
          GameProgress.currentLevel = levelIndex;   // 记录选的关
          SceneManager.LoadScene(battleSceneName);  // 跳回战斗场景
      }
  }