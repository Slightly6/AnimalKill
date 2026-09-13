using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;   // 要用 Image 做血条
using System;           // 要用 Action 做事件
public class Stamina : MonoBehaviour
{
    [Header("体力")]
    public float maxStamina = 100f; // 最大体力值
    public float currentStamina; // 当前体力值
    public float staminaDecreaseRate = 10f; // 体力消耗速率
    public float staminaRecoveryRate = 5f; // 体力恢复速率
    public float staminaRecoveryDelay = 2f; // 体力恢复延迟时间
    public bool isRunning = false; // 是否正在奔跑
    private float lastRunTime; // 上次奔跑的时间
    public bool canRun = true; // 是否可以奔跑
    public bool isStaminaDepleted = false; // 体力是否耗尽
    public Image staminaBar; // 体力条UI
    void Start()
    {
        currentStamina = maxStamina;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
