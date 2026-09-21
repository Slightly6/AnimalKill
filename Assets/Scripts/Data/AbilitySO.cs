using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum AbilityTrigger
{
    OnHit,          // 命中时
    OnKill,         // 击杀时
    OnDamaged,      // 受击时
    OnDeath,        // 死亡时
    OnTurnStart,    // 回合开始
    OnTurnEnd,      // 回合结束
    OnPlay,         // 打出时
    OnDraw,         // 抽到时
}
[CreateAssetMenu(fileName = "New Ability", menuName = "Data/Ability")]
public class AbilitySO : ScriptableObject
{
    public string abilityName;
    [TextArea] public string description;
    public Sprite icon;
    public AbilityTrigger trigger;      // 什么时候触发
    public AbilityEffectSO effect;      // 触发后做什么
}
