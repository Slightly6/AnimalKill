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

      [Header("过场相机 + 主角")]
      public PlayableDirector director;  // 拖 TimeCamera 上的 PlayableDirector（不拖就找同物体）
      public Camera timelineCamera;      // 第三个相机 TimeCamera，专门拍 Timeline（过场时激活）
      public GameObject player;          // 真玩家（挂 HeroController 的物体，Timeline 直接驱动它走到桌前）

      [Header("过场播多久后切到打牌（秒）")]
      public float cutsceneDuration = 15f;   // 猫步走到桌前(14秒)+坐下，想提前/延后切就调这个

      [Header("主角走到桌前（代码走，不走 Timeline）")]
      public Transform tableSeat;    // 桌前站位点：放一个空物体，标记主角要走到哪、面向哪
      public float walkSpeed = 5f; // 过场走路速度（米/秒），和走路动画匹配，别太快
      public float stopBlend = 2f;   // 走到后「走路→待机」过渡快慢，值越大停得越快

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

          // 第一人称阶段锁鼠标（转视角用）。打牌阶段在 SkipToTable / SwitchToTable
          // 里设回 None+visible=true；Boss 战在 EnterBossFight 里设 Locked+visible=false。
          // 之前这部分在 HeroController.Start 里设，会跟 SkipToTable 抢着设鼠标，
          // 改成由 CutsceneManager 统一管，避免执行顺序问题导致鼠标不显示。
          Cursor.lockState = CursorLockMode.Locked;
          Cursor.visible = false;
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

          // 切到过场：开第三相机，主角保持激活（Timeline 直接驱动主角走到桌前）
          SetCutscene(true);

          director.Play();
          Debug.Log("[过场] StartCutscene：已调用 director.Play()，开始播 Timeline");

          // 主角用代码自己走过去（Timeline 只管门和相机）
          StartCoroutine(WalkToTable());

          // 不靠 director.stopped（Idle 剪辑是无限保持，永远不触发 stopped），
          // 改成计时器：播够 cutsceneDuration 秒就直接切到打牌
          StartCoroutine(WaitThenSwitch());
      }

      // 计时器到点后调用：切到打牌
        IEnumerator WaitThenSwitch()
    {
        float timer = 0f;

        // 每帧检测：时间到 或 按空格，就跳出
        while (timer < cutsceneDuration)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                Debug.Log("[过场] 按空格跳过");
                if (director != null) director.Stop();   // 停掉 Timeline 和音效
                break;
            }

            timer += Time.deltaTime;
            yield return null;
        }

        SwitchToTable();
    }

      // ========== 主角用代码走到桌前（Timeline 只拍门和相机） ==========
      // 过场一开始就调：主角边播走路动画边朝 tableSeat 走，走到就站定。
      // 过场阶段 HeroController 已经暂停（stage=Cutscene 不驱动移动），这里手动控制主角。
      IEnumerator WalkToTable()
      {
          // 没拖站位点或主角，就直接跳过（不走了）
          if (player == null || tableSeat == null) yield break;

          HeroController hero = player.GetComponent<HeroController>();
          Animator anim = hero != null ? hero.anim : null;

          // 目标点只取水平坐标，主角保持自己原来的高度（贴地）
          Vector3 target = tableSeat.position;
          target.y = player.transform.position.y;

          // 先算一次到目标的方向和距离
          Vector3 dir = target - player.transform.position;
          dir.y = 0f;

          // 走到离目标多远就算到（留一点点，别贴得太死）
          float arriveDistance = 0.2f;

          // 播走路动画（复用主角自己的混合树：MoveY=前进，Speed=0.5 走路）
          if (anim != null)
          {
              anim.SetFloat("MoveY", 1f);
              anim.SetFloat("MoveX", 0f);
              anim.SetFloat("Speed", 0.5f);
          }

          // 一路走到目标点
          while (dir.magnitude > arriveDistance)
          {
              // 朝目标平滑转身（边走边转，像平时走路那样）
              if (dir.magnitude > 0.01f)
              {
                  Quaternion targetRot = Quaternion.LookRotation(dir);
                  player.transform.rotation = Quaternion.Slerp(player.transform.rotation, targetRot, 8f * Time.deltaTime);
              }

              // 用代码把主角往前挪（和 HeroController.Move 一样直接动位置）
              player.transform.position += player.transform.forward * walkSpeed * Time.deltaTime;

              // 每帧重新算方向距离
              dir = target - player.transform.position;
              dir.y = 0f;

              yield return null;
          }

          // 走到后：站定在目标点，朝向站位点标记的方向
          player.transform.position = new Vector3(target.x, player.transform.position.y, target.z);
          if (tableSeat != null)
          {
              Vector3 euler = tableSeat.eulerAngles;
              player.transform.rotation = Quaternion.Euler(0f, euler.y, 0f);
          }

          // 先停住脚（不再往前走），Speed 先别急着归零
          if (anim != null)
          {
              anim.SetFloat("MoveY", 0f);
              anim.SetFloat("MoveX", 0f);
          }

          // 平滑过渡：Speed 从 0.5（走路）慢慢降到 0（待机），
          // 混合树会自然从走路姿势过渡到待机，不会瞬间变
          float speed = 0.5f;
          while (speed > 0.01f && anim != null)
          {
              speed = Mathf.MoveTowards(speed, 0f, stopBlend * Time.deltaTime);
              anim.SetFloat("Speed", speed);
              yield return null;
          }
          if (anim != null) anim.SetFloat("Speed", 0f);

          Debug.Log("[过场] 主角已走到桌前站位点");
      }

      // 切到打牌：关过场（第三相机+假 Hero 失活），开真玩家 + 桌面相机，然后发牌
      void SwitchToTable()
      {
          Debug.Log("[过场] 过场播完（计时器到点），开始切换");

          // 主角已经由 Timeline 驱动走到桌前，不用再摆位置，直接切桌面相机
          SetCutscene(false);   // 关第三相机（Timeline 停止）
          GameProgress.currentStage = GameStage.Playing;
          SetCamera(true);      // 关第一人称，开桌面（俯视打牌）

          // 解锁鼠标（打牌要点牌）
          Cursor.lockState = CursorLockMode.None;
          Cursor.visible = true;

          // 发牌
          Debug.Log("[过场] 切换完成，调用 StartCurrentLevel 发牌");
          MapManager.Instance.StartCurrentLevel();
      }

        // 非第一关：跳过走门和过场，主角直接坐到桌前开始打牌（MapManager.BeginRun 调用）
        public void SkipToTable()
        {
            // 主角直接放到桌前（第一关是走门 → 过场走过去；之后每关直接传送到位，这就是这关的出生点）
            if (player != null && tableSeat != null)
            {
                player.transform.position = tableSeat.position;
                player.transform.rotation = tableSeat.rotation;
            }

            GameProgress.currentStage = GameStage.Playing;   // 直接进打牌阶段
            SetCamera(true);                                 // 关第一人称，开桌面俯视
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            MapManager.Instance.StartCurrentLevel();         // 发牌
        }

        public void EnterBossFight()
    {
        GameProgress.currentStage = GameStage.FirstPerson;   // 恢复第一人称（HeroCtroller 重新接管）

        SetCamera(false);   // 关桌面相机，开第一人称相机

        // 锁鼠标（第一人称要鼠标视角）
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
      // 过场状态：on=true 进过场（开第三相机，主角保持激活给 Timeline 驱动）；on=false 出过场
      void SetCutscene(bool on)
      {
          if (timelineCamera != null) timelineCamera.gameObject.SetActive(on);   // 整个 TimeCamera 激活/失活（它默认是失活的）

          // 进过场时把主角的相机都关掉，只留 Timeline 相机
          // （主角现在不失活了，第一/第三人称相机会跟着主角一直开着，不关会跟过场相机打架）
          if (on)
          {
              if (firstPersonCamera != null) firstPersonCamera.enabled = false;

              HeroController hero = player != null ? player.GetComponent<HeroController>() : null;
              if (hero != null && hero.thirdPersonCamera != null)
              {
                  hero.thirdPersonCamera.enabled = false;
              }
          }
      }

      // 切相机：useTableCamera = true 用桌面相机，false 用第一/第三人称相机（按玩家当前视角）
      void SetCamera(bool useTableCamera)
      {
          // 玩家当前是不是第三人称（打牌时不管视角，都只看桌面；回第一人称才按它恢复）
          HeroController hero = player != null ? player.GetComponent<HeroController>() : null;
          bool useThird = hero != null && hero.isThirdPerson;

          if (firstPersonCamera != null) firstPersonCamera.enabled = !useTableCamera && !useThird;
          if (tableCamera != null) tableCamera.enabled = useTableCamera;
          if (hero != null && hero.thirdPersonCamera != null)
          {
              hero.thirdPersonCamera.enabled = !useTableCamera && useThird;
          }
      }

  }