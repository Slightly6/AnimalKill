using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;   // 要用 Image 做体力条

public class Stamina : MonoBehaviour
{
    [Header("体力")]
    public float maxStamina = 100f; // 最大体力值
    [SerializeField]
    private float currentStamina; // 当前体力值
    public float staminaDecreaseRate = 10f; // 体力消耗速率
    public float staminaRecoveryRate = 5f; // 体力恢复速率
    [Header("体力条恢复延迟时间")]
    public float staminaRecoveryDelay = 2f; // 体力恢复延迟时间
    public float staminaRunThreshold = 20f; // 体力回到多少才能再跑
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
        if (isRunning&&GameProgress.currentStage == GameStage.FirstPerson)
        {
            // 在跑：每帧记下时间 + 扣体力
            lastRunTime = Time.time;

            currentStamina -= staminaDecreaseRate * Time.deltaTime;
            if (currentStamina <= 0f)
            {
                currentStamina = 0f;
                isStaminaDepleted = true;
                canRun = false;   // 体力空了，跑不动
            }
        }
        else
        {
            // 没在跑：停跑后要过 staminaRecoveryDelay 秒才开始回
            if (Time.time - lastRunTime >= staminaRecoveryDelay)
            {
                currentStamina += staminaRecoveryRate * Time.deltaTime;
                if (currentStamina >= maxStamina) currentStamina = maxStamina;   // 别超过上限

                // 回到阈值就能再跑
                if (currentStamina >= staminaRunThreshold)
                {
                    isStaminaDepleted = false;
                    canRun = true;
                }
            }
        }

        UpdateStaminaBar();
    }

    // 体力条填充 = 当前体力 / 最大体力（跟血条一样）
    void UpdateStaminaBar()
    {
        if (staminaBar != null)
        {
            staminaBar.fillAmount = currentStamina / maxStamina;
        }
    }
}
