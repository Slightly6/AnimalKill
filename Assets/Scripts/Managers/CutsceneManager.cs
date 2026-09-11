using UnityEngine;
using UnityEngine.Playables;

  /// <summary>
  /// 过场管理器：管「第一人称 → 开门过场（Hero 走过去坐下）→ 打牌」三个阶段切换。
  /// 挂在 CutsceneDirector 上（和 PlayableDirector 同一个物体）。
  /// </summary>


public class CutsceneManager : Singleton<CutsceneManager>
  {
      [Header("两个相机（拖进来）")]
      public Camera firstPersonCamera;   // 第一人称相机（Hero 的眼睛，子物体）
      public Camera tableCamera;         // 桌面相机 Main Camera（打牌用）

      private PlayableDirector director;

      void Start()
      {
          director = GetComponent<PlayableDirector>();

          // Timeline 播完会触发 stopped 事件，在那一刻切到打牌
          director.stopped += OnCutsceneFinished;

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
          // 不切相机：第一人称相机是 Hero 的子物体，眼睛跟着 Hero 走过去坐下
          director.Play();
      }

      // 过场播完：切到打牌
      void OnCutsceneFinished(PlayableDirector d)
      {
          GameProgress.currentStage = GameStage.Playing;

          SetCamera(true);   // 关第一人称，开桌面（俯视打牌）

          // 解锁鼠标（打牌要点牌）
          Cursor.lockState = CursorLockMode.None;
          Cursor.visible = true;

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
      // 切相机：useTableCamera = true 用桌面相机，false 用第一人称相机
      void SetCamera(bool useTableCamera)
      {
          firstPersonCamera.enabled = !useTableCamera;
          tableCamera.enabled = useTableCamera;
      }

      void OnDestroy()
      {
          if (director != null) director.stopped -= OnCutsceneFinished;
      }
  }