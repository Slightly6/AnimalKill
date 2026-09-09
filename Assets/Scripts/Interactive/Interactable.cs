using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 可交互物体的基类。凡是能"按 E 交互"的物体（门、物品、机关…）都继承它。
/// 子类只需要实现 Interact()，写清楚"自己被交互时要干什么"。
/// </summary>
public abstract class Interactive : MonoBehaviour
{
    [Header("交互设置")]
    public string interactText = "按 E 交互"; // 鼠标悬停时显示的提示文字

    public abstract void Interact(); // 交互事件：子类实现它，写清楚"自己被交互时要干什么"

}
