using UnityEngine;

/// <summary>
/// 单摆摆动：让牌像挂在钩子上一样来回荡。
/// 挂在「支点」物体上（牌是它的子物体，牌中心在支点正下方）。
/// 物理：重力往下拽 + 阻力衰减，越荡越小自然停；Bump() 踢一脚再晃。
/// idleSway：停稳后还一直轻轻晃，不会死鱼一样一动不动。
/// </summary>
public class HangingCard : MonoBehaviour
{
    [Header("摆动（数值边玩边调）")]
    public Vector3 swingAxis = Vector3.forward;   // 绕哪根轴摆（正面正对相机默认 Z，不对就试 X）
    public float gravity = 400f;    // 重力，越大荡得越快
    public float damping = 0.99f;   // 阻力，越小停越快（1 = 不衰减一直荡）

    [Header("一直轻轻晃")]
    public bool idleSway = true;    // 停稳后还轻轻晃
    public float idleAmp = 3f;      // 轻晃角度（度）
    public float idleSpeed = 1.2f;  // 轻晃快慢

    [Header("刚挂上时晃多狠")]
    public float spawnKick = 40f;   // 出生随机角速度范围（度/秒）

    float angle;        // 当前摆角（度）
    float angularVel;   // 角速度（度/秒）

    void Start()
    {
        angle = Random.Range(-idleAmp, idleAmp);
        angularVel = Random.Range(-spawnKick, spawnKick);   // 挂上来先荡一下
    }

    void Update()
    {
        // 单摆：角加速度 = -重力 * sin(角)，再乘阻力衰减
        angularVel += -gravity * Mathf.Sin(angle * Mathf.Deg2Rad) * Time.deltaTime;
        angularVel *= damping;
        angle += angularVel * Time.deltaTime;

        // 一直轻轻晃（叠加一个慢正弦，别完全停死）
        float finalAngle = angle;
        if (idleSway) finalAngle += Mathf.Sin(Time.time * idleSpeed) * idleAmp;

        transform.localRotation = Quaternion.Euler(swingAxis * finalAngle);
    }

    // 踢一脚：加角速度，让牌再晃起来（钩子被撞 / 别的牌挂上来时调）
    public void Bump(float kick)
    {
        angularVel += kick;
    }
}
