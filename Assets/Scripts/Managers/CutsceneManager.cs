using UnityEngine;
using UnityEngine.Playables;
using System.Collections;   // 用 IEnumerator / StartCoroutine 做过场淡入淡出

  /// <summary>
  /// 过场管理器：管「第一人称 → 开门过场（Hero 走过去坐下）→ 打牌」三个阶段切换。
  /// 挂在一个常驻的管理器物体上（比如 CutSceneManager 空物体），导演手动拖 TimeCamera 上的 PlayableDirector。
  /// </summary>


public class CutsceneManager : Singleton<CutsceneManager>
  {
      [Header("两个相机（拖进来）")]
      public Camera firstPersonCamera;   // 第一人称相机（Hero 的眼睛，子物体）
      public Camera tableCamera;         // 桌面相机 Main Camera（打牌用）

      [Header("过场相机 + 真假 Hero")]
      public PlayableDirector director;  // 拖 TimeCamera 上的 PlayableDirector（不拖就找同物体）
      public Camera timelineCamera;      // 第三个相机 TimeCamera，专门拍 Timeline（过场时激活）
      public GameObject fakeHero;        // 假 Hero 替身（Timeline 里走路那个，播完失活）
      public GameObject player;          // 真玩家（挂 HeroController 的物体，过场时失活，播完激活）

      [Header("过场播多久后切到打牌（秒）")]
      public float cutsceneDuration = 15f;   // 猫步走到桌前(14秒)+坐下，想提前/延后切就调这个

      void Start()
      {
          // 没手动拖导演，就尝试在同物体上找（推荐：手动把 TimeCamera 上的 PlayableDirector 拖进 director 字段）
          if (director == null) director = GetComponent<PlayableDirector>();

          if (director == null)
          {
              Debug.LogError("CutsceneManager 没找到 PlayableDirector！请把 TimeCamera 上的 PlayableDirector 拖进 director 字段");
              return;
          }

          // 进场景先处于第一人称阶段
          GameProgress.currentStage = GameStage.FirstPerson;
          SetCamera(false);   // false = 用第一人称相机
      }

      // 开始过场（门开完后调用）
      public void StartCutscene()
      {
          if (director == null)
          {
              Debug.LogError("CutsceneManager 没找到 PlayableDirector！");
              return;
          }

          GameProgress.currentStage = GameStage.Cutscene;   // 进入过场阶段

          // 切到过场：开第三相机 + 假 Hero，关真玩家（真玩家已摆到指定位置，播完再激活）
          SetCutscene(true);

          director.Play();
          Debug.Log("[过场] StartCutscene：已调用 director.Play()，开始播 Timeline");

          // 不靠 director.stopped（Idle 剪辑是无限保持，永远不触发 stopped），
          // 改成计时器：播够 cutsceneDuration 秒就直接切到打牌
          StartCoroutine(WaitThenSwitch());
      }

      // 计时器到点后调用：切到打牌
      IEnumerator WaitThenSwitch()
      {
          yield return new WaitForSeconds(cutsceneDuration);
          SwitchToTable();
      }

      // 切到打牌：关过场（第三相机+假 Hero 失活），开真玩家 + 桌面相机，然后发牌
      void SwitchToTable()
      {
          Debug.Log("[过场] 过场播完（计时器到点），开始切换");

          // 打牌前，把真玩家摆到假 Hero 最后的位置（Timeline 播完假 Hero 正好走到桌前坐下）
          // 这样切到桌面相机时，真 Hero 就坐在桌前，不会还站在门口
          if (fakeHero != null && player != null)
          {
              player.transform.position = fakeHero.transform.position;
              player.transform.rotation = fakeHero.transform.rotation;
          }

          SetCutscene(false);   // 关第三相机 + 假 Hero，开真玩家（同时停掉 Timeline）
          GameProgress.currentStage = GameStage.Playing;
          SetCamera(true);      // 关第一人称，开桌面（俯视打牌）

          // 解锁鼠标（打牌要点牌）
          Cursor.lockState = CursorLockMode.None;
          Cursor.visible = true;

          // 发牌
          Debug.Log("[过场] 切换完成，调用 StartCurrentLevel 发牌");
          MapManager.Instance.StartCurrentLevel();
      }
        public void EnterBossFight()
    {
        GameProgress.currentStage = GameStage.FirstPerson;   // 恢复第一人称（HeroCtroller 重新接管）

        SetCamera(false);   // 关桌面相机，开第一人称相机

        // 锁鼠标（第一人称要鼠标视角）
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
      // 过场状态：on=true 进过场（第三相机+假Hero 开，真玩家关）；on=false 出过场
      void SetCutscene(bool on)
      {
          if (timelineCamera != null) timelineCamera.gameObject.SetActive(on);   // 整个 TimeCamera 激活/失活（它默认是失活的）
          if (fakeHero != null) fakeHero.SetActive(on);
          if (player != null) player.SetActive(!on);
      }

      // 切相机：useTableCamera = true 用桌面相机，false 用第一人称相机
      void SetCamera(bool useTableCamera)
      {
          if (firstPersonCamera != null) firstPersonCamera.enabled = !useTableCamera;
          if (tableCamera != null) tableCamera.enabled = useTableCamera;
      }

  }