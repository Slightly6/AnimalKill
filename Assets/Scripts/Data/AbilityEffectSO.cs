using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 技能种类。后期改技能不用写代码：在 AbilitySO 资产的 effect 上换 Kind、调数值即可。
/// 当前只保留黑桃 2~7 六个技能 + 3 个钩子联动备用项。
/// </summary>
public enum AbilityKind
{
    None = 0,

    // ---- ♠2~7 猎杀系 ----
    GrowAnyDeath,   // ♠2 秃鹫·食腐：场上每死一个单位（敌我都算）自身 +amount
    SkillImmune,    // ♠3 猫头鹰·鸮佑：不受敌方技能影响
    EvadeChance,    // ♠4 貂·闪避：被攻击时 amount% 概率扑空免受此次伤害（amount=30 即 30%）
    Colony,         // ♠5 狐獴·团居：扑击伤害 = 自身力量 × 同阵营在场数量
    DeathRoll,      // ♠6 鳄鱼·死亡翻滚：每轮随机攻击 1~5 次，每次都是当前力量的完整伤害
    DeepHunter,     // ♠7 鲨鱼·深海猎手：捡牙→满20潜水，伤害×牙数，首击无敌，每回合-2牙

    // ---- 钩子牌型联动（备用，目前没分配给卡）----
    HookGrow,           // 登场：钩子上每张牌自身 +amount
    HookHandBonus,      // 登场：按钩子牌型加力量（对子2/三条4/同花6/葫芦8/同花顺10）
    ChipInterest,       // 每回合开始：按当前筹码 5% 加力量
}

/// <summary>
/// 通用参数化技能效果。所有技能共用这一个类：
/// 选 Kind（做什么）+ amount/amount2（数值），触发时机在外层 AbilitySO.trigger 上配。
/// 六个战斗技能全是"登场挂标记，战斗流程读标记"，这里的 case 只负责挂标记。
/// </summary>
[CreateAssetMenu(fileName = "New Ability Effect", menuName = "Data/Ability Effect")]
public class AbilityEffectSO : ScriptableObject
{
    [Tooltip("技能种类（决定做什么）")]
    public AbilityKind kind = AbilityKind.None;
    [Tooltip("主数值：闪避概率/食腐加值等")]
    public int amount = 1;
    [Tooltip("副数值（备用）")]
    public int amount2 = 0;

    // 返回 true = 技能真的发动了（用于触发闪光特效）；false = 条件不满足/被免疫，没发动
    public bool Apply(Card self, Card target)
    {
        if (self == null || self.IsDead) return false;

        // 鸮佑：目标免疫来自敌方技能的影响（直接攻击伤害不走技能，不受影响）
        if (target != null && !target.IsDead
            && target.flagSkillImmune && target.IsPlayer != self.IsPlayer)
        {
            target.FlashSkill();   // 鸮佑生效→被免疫方闪一下
            return false;   // 被免疫了，攻击方技能不算发动
        }

        switch (kind)
        {
            // ---------- ♠2 秃鹫·食腐：挂标记，BoardManager 监听死亡事件给它加力量 ----------
            case AbilityKind.GrowAnyDeath:
                self.flagGrowAnyDeath = true;
                return true;

            // ---------- ♠3 猫头鹰·鸮佑 ----------
            case AbilityKind.SkillImmune:
                self.flagSkillImmune = true;
                return true;

            // ---------- ♠4 貂·闪避（amount=30 → 30%）----------
            case AbilityKind.EvadeChance:
                self.flagEvade = true;
                self.evadeChance = amount / 100f;
                return true;

            // ---------- ♠5 狐獴·团居 ----------
            case AbilityKind.Colony:
                self.flagColony = true;
                return true;

            // ---------- ♠6 鳄鱼·死亡翻滚 ----------
            case AbilityKind.DeathRoll:
                self.flagDeathRoll = true;
                return true;

            // ---------- ♠7 鲨鱼·深海猎手 ----------
            case AbilityKind.DeepHunter:
                self.flagDeepHunter = true;
                return true;

            // ---------- 钩子联动（备用）----------
            case AbilityKind.HookGrow:
            {
                int hung = GameManager.Instance.TrophyCount;
                if (hung > 0) self.AddPower(hung * amount);
                return true;
            }

            case AbilityKind.HookHandBonus:
            {
                List<CardDataSO> hung = GameManager.Instance.GetTrophyCards();
                if (hung != null && hung.Count > 0)
                {
                    HandType type = PokerHandEvaluator.Evaluate(hung);
                    int bonus = 0;
                    if (type == HandType.OnePair) bonus = 2;
                    else if (type == HandType.ThreeOfAKind) bonus = 4;
                    else if (type == HandType.Flush || type == HandType.Straight) bonus = 6;
                    else if (type == HandType.FullHouse) bonus = 8;
                    else if (type == HandType.StraightFlush || type == HandType.FourOfAKind) bonus = 10;
                    if (bonus > 0) self.AddPower(bonus);
                }
                return true;
            }

            case AbilityKind.ChipInterest:
            {
                int gain = Mathf.FloorToInt(GameManager.Instance.PlayerChips * 0.05f);
                if (gain > 0) self.AddPower(gain);
                return true;
            }
        }
        return false;
    }
}
