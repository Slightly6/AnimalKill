using System.Collections;
  using UnityEngine;
  using UnityEngine.SceneManagement;

  /// <summary>
  /// 关卡流程总管（战斗场景里）。
  /// 开局/切回：开始 GameProgress.currentLevel 关。
  /// 过关：进度 +1 → 切到地图场景选下一关。
  /// </summary>
  public class MapManager : Singleton<MapManager>
  {
      [Header("关卡数据库（52关）")]
      public LevelDatabase database;

      [Header("场景名（要和 Build Settings 里一致）")]
      public string mapSceneName = "Map";            // 地图场景
      public string battleSceneName = "SampleScene"; // 战斗场景

      void Start()
      {
          EventBus.Subscribe<LevelClearedEvent>(OnLevelCleared);
          StartCoroutine(BeginRun());
      }

      void OnDestroy()
      {
          EventBus.Unsubscribe<LevelClearedEvent>(OnLevelCleared);
      }

      // 延迟一帧，等所有 Manager 初始化，再开始当前关
      IEnumerator BeginRun()
      {
          yield return null;
          StartLevel(GameProgress.currentLevel);
      }

      // 开始一关
      void StartLevel(int index)
      {
          if (database == null)
          {
              Debug.LogError("MapManager 没设置 LevelDatabase！");
              return;
          }
          if (index >= database.levels.Count)
          {
              Debug.LogError("关卡索引 " + index + " 超出数据库范围（共 " + database.levels.Count + " 关）");
              return;
          }

          LevelConfig cfg = database.levels[index];

          GameManager.Instance.LoadLevel(cfg);       // 设敌人筹码、清战利品
          BoardManager.Instance.ResetLevel(cfg);     // 清空敌方、重摆敌人
          DeckManager.Instance.SetupLevel(cfg);      // 补手牌
          EventBus.Publish(new LevelStartedEvent { levelIndex = index, isBoss = cfg.isBoss });
          BattleManager.Instance.StartLevel(cfg);    // 开打
      }

      // 过关：最终 Boss（最后一关）→ 胜利；否则回地图继续往上
      void OnLevelCleared(LevelClearedEvent e)
      {
          if (GameProgress.currentLevel >= database.levels.Count - 1)
          {
              GameManager.Instance.WinGame();
          }
          else
          {
              SceneManager.LoadScene(mapSceneName);
          }
      }
  }