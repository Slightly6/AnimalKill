using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInteractable : MonoBehaviour
{
    [Header("交互提示距离")]
    public float interactDistance = 3f; // 玩家与可交互物体的最大交互距离
    [Header("射线从多高发出（眼睛高度）")]
    public float eyeHeight = 1.5f; // 从 Hero 眼睛高度朝前方发射线用
    private Camera playerCamera; // 玩家摄像机（只用来画提示文字）
    private Interactive currentInteractive; // 当前可交互物体
    private string promptText; // 当前交互提示文字
    private bool styleInitialized = false;// 是否已初始化提示文字样式
    private GUIStyle promptStyle; // 提示文字的样式
    void Start()
    {
        playerCamera = GetComponent<Camera>();
        if(playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
        }
        
    }
    void Update()
    {
        if (GameProgress.currentStage != GameStage.FirstPerson)
        {
            currentInteractive = null;
            promptText = "";
            return; 
        } // 只有第一人称探索阶段能交互
        FindInteractive();
        if(currentInteractive != null && Input.GetKeyDown(KeyCode.E))
        {
            currentInteractive.Interact();
        }
    }
    void FindInteractive()
      {
          // 从 Hero 自身（眼睛高度）朝前方发射线：不依赖相机，过场/切视角时相机被关也不报错
          Vector3 eyePos = transform.position + Vector3.up * eyeHeight;
          Ray ray = new Ray(eyePos, transform.forward);
          RaycastHit hit;

          if (Physics.Raycast(ray, out hit, interactDistance))
          {
              // 关键：用 GetComponentInParent 向上找。
              // 因为射线可能打到门模型（子物体），而 Door 脚本在它父物体（DoorPivot/空物体）上。
              Interactive inter = hit.collider.GetComponentInParent<Interactive>();
              if (inter != null)
              {
                  if (!inter.canInteract)   // 这个物体不参与按 E（比如触发器门）
                  {
                      currentInteractive = null;
                      promptText = "";
                      return;
                  }
                  currentInteractive = inter;
                  promptText = inter.interactText;
                  return;
              }
          }

          // 没命中，或命中的不是可交互物
          currentInteractive = null;
          promptText = "";
      }
    void OnGUI()
    {
        if (!styleInitialized)
        {
            promptStyle = new GUIStyle(GUI.skin.label);
            promptStyle.fontSize = 24;
            promptStyle.alignment = TextAnchor.MiddleCenter;
            promptStyle.normal.textColor = Color.white;
            styleInitialized = true;
        }

        if (!string.IsNullOrEmpty(promptText))
        {
            Vector3 screenPos = playerCamera.WorldToScreenPoint(playerCamera.transform.position + playerCamera.transform.forward * interactDistance);
            GUI.Label(new Rect(screenPos.x - 100, Screen.height - screenPos.y - 25, 200, 50), promptText, promptStyle);
        }
    }
}
