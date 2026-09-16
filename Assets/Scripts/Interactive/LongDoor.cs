using UnityEngine;


  public class DoorMap : MonoBehaviour
  {

      public GameObject doorVisual;          // 门的样子（子物体），开局藏起来，Boss 死了才亮

      void Start()
      {
          EventBus.Subscribe<DiedEvent>(OnBossDied);   // 订阅死亡事件
      }

      // 谁死了都会广播 DiedEvent，这里只认 Boss（isPlayer = false）
      void OnBossDied(DiedEvent e)
      {
          if (e.isPlayer) return;                       // 英雄死：不显示门
          if (doorVisual != null) doorVisual.SetActive(true);
      }

      void OnDestroy()
      {
          EventBus.Unsubscribe<DiedEvent>(OnBossDied);  // 销毁时退订，别留脏订阅
      }

  }