using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class AttackConfig
{
    [Header("显示")]
    public string name;           // 备注，方便认（如「近战挥砍」）

    [Header("动画器")]
    public int stateValue;        // State 参数值（切这个攻击动画）
    public string stateName;      // Animator 状态名（检测动画播完用）

    [Header("距离（米）")]
    public float minDistance;     // 玩家近到多少才用这个攻击
    public float maxDistance;     // 玩家远到多少改用更远的攻击

    [Header("CD 联动")]
    public int groupId;        // 分组：0=近 1=中 2=远；同 groupId 共享 CD（最远两个都填 2）

    [Header("节奏")]
    public float cooldown;        // 这个攻击自己的 CD（秒）
    public float recoveryTime;    // 后摇：动画播完停几秒，hero 抓后摇
}