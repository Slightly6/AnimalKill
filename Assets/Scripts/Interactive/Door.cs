using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 门：继承 Interactable。被按 E 交互时，绕 Y 轴转 90 度打开/关上。
/// 注意：门的轴心点（pivot）必须在铰链那一边，否则会绕中心转，很怪。
/// </summary>
public class Door :Interactive
{
    [Header("开门设置")]
    public float openAngle = -120f; // 开门时绕 Y 轴转多少度
    public float openSpeed = 2f;  // 开门的速度（角度/秒）  

    private bool isOpen = false; // 门当前是开着还是关着    
    private float targetAngle=0f;    // 门要转到的目标角度

    void Start()
    {
        
    }

    void Update()
    {
        Quaternion target = Quaternion.Euler(0f, targetAngle, 0f);
        transform.localRotation = Quaternion.Lerp(transform.localRotation, target,
        Time.deltaTime * openSpeed);
    } 
    public override void Interact()
    {
        isOpen = !isOpen;                         // 开关状态反转
        targetAngle = isOpen ? openAngle : 0f;    // 决定转去开门角度还是关门角度
    }

}
