using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInteractable : MonoBehaviour
{
    [Header("交互提示距离")]
    public float interactDistance = 3f; // 玩家与可交互物体的最大交互距离
    private Camera playerCamera; // 玩家摄像机
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
          // 屏幕中心（0.5, 0.5）发一条射线 = 第一人称"看哪指哪"
          Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
          RaycastHit hit;

          if (Physics.Raycast(ray, out hit, interactDistance))
          {
              // 关键：用 GetComponentInParent 向上找。
              // 因为射线可能打到门模型（子物体），而 Door 脚本在它父物体（DoorPivot/空物体）上。
              Interactive inter = hit.collider.GetComponentInParent<Interactive>();
              if (inter != null)
              {
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
