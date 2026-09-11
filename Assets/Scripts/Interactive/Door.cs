using System.Collections;
  using UnityEngine;

  public class Door : Interactive
  {
      [Header("开门设置")]
      public float openAngle = 240f;   // 开门绕 Y 轴转多少度
      public float openSpeed = 2f;     // 开门速度（角度/秒）

      private bool isOpen = false;                 // 门当前开还是关
      private bool isLocked = false;               // 锁住：按 E 没反应
      private bool hasTriggeredCutscene = false;   // 是否触发过过场（只触发一次）
      private Quaternion closedRotation;
      private Quaternion openRotation;
      private Quaternion targetRotation;
      private bool isRotating = false;// 正在转门：按 E 没反应

      void Start()
      {
          closedRotation = transform.localRotation;
          openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
      }

      // 开关门共用一个协程：转到 targetRotation
      IEnumerator RotateDoor()
      {
          isRotating = true;
          while (Quaternion.Angle(transform.localRotation, targetRotation) > 0.05f)
          {
              transform.localRotation = Quaternion.Lerp(transform.localRotation,
  targetRotation, Time.deltaTime * openSpeed);
              yield return null;
          }
          transform.localRotation = targetRotation;
          isRotating = false;

        // 开门到位后：第一次触发过场，然后自动关门 + 锁死
        if (isOpen)
        {
            CloseAndLock();
        }
      }

      // 关门 + 锁死（过场开始时调用，把玩家困在房里）
      public void CloseAndLock()
      {
          isLocked = true;              // 关键2：锁死
          isOpen = false;
          targetRotation = closedRotation;
          StartCoroutine(RotateDoor());
      }

      // 完成房间任务后调用：解锁，门又能开了
      public void Unlock()
      {
          isLocked = false;
      }

      public override void Interact()
      {
          if (isLocked) return;      // 锁死：按 E 没反应
          if (isRotating) return;    // 正在转：没反应

          if (!isOpen && !hasTriggeredCutscene)
          {
            hasTriggeredCutscene = true;              // 关键1：过场只触发一次
            CutsceneManager.Instance.StartCutscene();
          }
          isOpen = !isOpen;
          targetRotation = isOpen ? openRotation : closedRotation;
          StartCoroutine(RotateDoor());
      }
  }