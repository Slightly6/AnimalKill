using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 技能测试菜单（只在 Play 模式 + 勾选 GameManager 的开挂模式 Cheat Mode 时可用）。
/// 用法：
///   1. GameManager 勾上 cheatMode（开挂模式）
///   2. 进 Play 模式，Project 窗口点选一张卡（Assets/Data/Cards/ 里的 .asset）
///   3. 菜单 Tools/技能测试 → 觉醒选中的卡
///   4. 再点"把选中的卡塞到手牌"，立刻能打出去试技能
/// 开挂没开时菜单整组灰掉、快捷键失效，防止正式游玩误触。
/// </summary>
public static class AbilityDebugMenu
{
    const string MENU = "Tools/技能测试/";
    const string CARD_PATH = "Assets/Data/Cards";
    const string PLAY_TIP = "请先进入 Play 模式再测试技能。";
    const string CHEAT_TIP = "请先勾选 GameManager 物体上的「开挂模式 Cheat Mode」，再按 Play。";

    static bool Playing { get { return Application.isPlaying; } }
    static bool CheatOn { get { return Playing && GameProgress.cheatMode; } }

    [MenuItem(MENU + "① 觉醒选中的卡  _F5", priority = 1)]
    static void AwakenSelected()
    {
        if (!CheatOn) { WarnGate(); return; }
        CardDataSO data = Selection.activeObject as CardDataSO;
        if (data == null) { WarnSelectCard(); return; }

        GameProgress.AwakenCard(data);
        Debug.Log("[测试] 已觉醒：" + data.animalName +
                  "（已在手牌/场上的旧牌不会补技能，用③重新塞一张）");
    }

    [MenuItem(MENU + "② 觉醒牌组全部  _F6", priority = 2)]
    static void AwakenAll()
    {
        if (!CheatOn) { WarnGate(); return; }
        int n = 0;
        foreach (CardDataSO d in GameProgress.playerDeck)
        {
            if (d != null && GameProgress.AwakenCard(d)) n++;
        }
        Debug.Log("[测试] 牌组全部觉醒，新觉醒 " + n + " 张（下一张抽到/生成的牌即带技能）");
    }

    [MenuItem(MENU + "③ 把选中的卡塞到手牌  _F7", priority = 3)]
    static void SpawnSelectedToHand()
    {
        if (!CheatOn) { WarnGate(); return; }
        CardDataSO data = Selection.activeObject as CardDataSO;
        if (data == null) { WarnSelectCard(); return; }
        if (DeckManager.Instance == null) { Debug.LogWarning("[测试] 场景里没有 DeckManager"); return; }

        DeckManager.Instance.DebugAddToHand(data);
        Debug.Log("[测试] 塞入手牌：" + data.animalName +
                  (GameProgress.IsCardAwakened(data) ? "（已觉醒，带技能）" : "（未觉醒，白板）"));
    }

    [MenuItem(MENU + "④ 清空全部觉醒（模拟重开）  _F8", priority = 4)]
    static void ClearAwakening()
    {
        if (!CheatOn) { WarnGate(); return; }
        GameProgress.awakenedCardNames.Clear();
        Debug.Log("[测试] 觉醒名单已清空");
    }

    [MenuItem(MENU + "⑤ 52张卡一键换入抽牌堆  _F9", priority = 5)]
    static void ReplaceDeckWithAll()
    {
        if (!CheatOn) { WarnGate(); return; }
        if (DeckManager.Instance == null) { Debug.LogWarning("[测试] 场景里没有 DeckManager"); return; }

        var cards = LoadAllCards();
        if (cards.Count == 0)
        {
            EditorUtility.DisplayDialog("技能测试",
                CARD_PATH + " 里没有卡牌资产，请先跑 Tools → 一键生成卡牌+觉醒技能。", "好");
            return;
        }
        DeckManager.Instance.DebugReplaceDeck(cards);
    }

    // 读 Assets/Data/Cards 下全部卡，按 ♠♥♦♣、A→K 排序
    static List<CardDataSO> LoadAllCards()
    {
        var result = new List<CardDataSO>();
        string[] guids = AssetDatabase.FindAssets("t:CardDataSO", new[] { CARD_PATH });
        foreach (string g in guids)
        {
            var c = AssetDatabase.LoadAssetAtPath<CardDataSO>(AssetDatabase.GUIDToAssetPath(g));
            if (c != null) result.Add(c);
        }
        result.Sort((a, b) =>
        {
            int s = a.suit.CompareTo(b.suit);
            return s != 0 ? s : a.rank.CompareTo(b.rank);
        });
        return result;
    }

    // ---- 菜单可用性：没进 Play 模式、或没开挂时灰掉（快捷键同样失效）----
    [MenuItem(MENU + "① 觉醒选中的卡  _F5", true)]
    [MenuItem(MENU + "② 觉醒牌组全部  _F6", true)]
    [MenuItem(MENU + "③ 把选中的卡塞到手牌  _F7", true)]
    [MenuItem(MENU + "④ 清空全部觉醒（模拟重开）  _F8", true)]
    [MenuItem(MENU + "⑤ 52张卡一键换入抽牌堆  _F9", true)]
    static bool ValidateCheatPlaying()
    {
        return CheatOn;
    }

    static void WarnGate()
    {
        // 开挂标志由 GameManager.Start 同步，没勾就是 false
        EditorUtility.DisplayDialog("技能测试", Playing ? CHEAT_TIP : PLAY_TIP, "好");
    }

    static void WarnSelectCard()
    {
        EditorUtility.DisplayDialog("技能测试",
            "请先在 Project 窗口点选一张卡牌资产（Assets/Data/Cards/ 里的 .asset）。", "好");
    }
}
