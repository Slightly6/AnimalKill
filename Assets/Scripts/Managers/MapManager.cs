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
          // 商店/奖励关：不摆棋盘不抽手牌，直接进面板（面板下一步做）
          if (GameProgress.IsNonBattleNode())
          {
              EnterNonBattleNode();
              return;
          }

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

      // 过关给兽皮：小关(Extra) 33% 掉 1 / 大关(Battle必过关) 稳定 1 / Boss 3
      void AwardHide()
      {
          NodeType t = GameProgress.currentNodeType;
          if (t == NodeType.Boss)
          {
              GameProgress.hides += 3;
          }
          else if (t == NodeType.Extra)
          {
              if (Random.value < 0.33f) GameProgress.hides += 1;
          }
          else if (t == NodeType.Battle)
          {
              GameProgress.hides += 1;
          }
          Debug.Log("[兽皮] 现在共 " + GameProgress.hides + " 片");
      }

      // 商店/奖励关（非战斗节点）：面板下一步做，这里先占位
      void EnterNonBattleNode()
      {
          Debug.Log("[节点] 进入 " + GameProgress.currentNodeType + "（面板下一步做）");
      }

      // 过关：K（章节 Boss）→ 解锁下一章 / 胜利；普通关 → 回地图
      void OnLevelCleared(LevelClearedEvent e)
      {
          AwardHide();   // 按刚打完的节点给兽皮

          int rank = GameProgress.currentLevel % 13;   // 0=A ... 12=K

          if (rank == 12)   // 打的是 K = 章节 Boss
          {
              if (GameProgress.currentSuit >= 3)   // 最后一章（♣）→ 整局胜利
              {
                  GameManager.Instance.WinGame();
              }
              else
              {
                  GameProgress.currentSuit++;   // 解锁下一章
                  SceneManager.LoadScene(mapSceneName);
              }
          }
          else
          {
              SceneManager.LoadScene(mapSceneName);   // 普通关 → 回地图
          }
      }
  }