using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 小丑管理器：只负责"持有"（购买后挂上钩子、换关通知）。
/// 战斗数值钩子已全部废弃（新体系：牌型固定伤害），等重做小丑时再加。
/// </summary>
public class JokerManager : Singleton<JokerManager>
{
    public int maxJokers = 5;                          // 小丑栏位上限（对应 HookDisplay 的 5 个挂点）
    public readonly List<JokerSO> jokers = new List<JokerSO>();

    private HookDisplay hookDisplay;                   // 懒找

    // 买小丑：栏没满就挂上，返回 true；飞行/挂载动画由 HookDisplay 接管
    public bool Add(JokerSO joker, Transform view)
    {
        if (joker == null || jokers.Count >= maxJokers) return false;

        jokers.Add(joker);
        joker.view = view;
        joker.OnAcquire();

        if (hookDisplay == null) hookDisplay = FindObjectOfType<HookDisplay>();
        if (hookDisplay != null)
            StartCoroutine(hookDisplay.FlyJokerToHook(view, jokers.Count - 1));

        return true;
    }

    // 换关：通知每个小丑清战斗内状态（MapManager 调）
    public void NotifyNewLevel()
    {
        foreach (JokerSO j in jokers)
            if (j != null) j.OnNewLevel();
    }
}
