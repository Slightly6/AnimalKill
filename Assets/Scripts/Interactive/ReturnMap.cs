using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
public class ReturnMap : MonoBehaviour
{

      [Header("切场景设置")]
      public string menuSceneName = "Map";   // 切到的场景名（记得加进 Build Settings）
    // Update is called once per frame
    void OnTriggerEnter(Collider other)
      {
          if (other.GetComponentInParent<HeroController>() == null) return;
          SceneManager.LoadScene(menuSceneName);
      }
}
