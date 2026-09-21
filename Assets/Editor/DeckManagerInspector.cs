using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// DeckManager 自定义 Inspector：
/// 在默认属性上方加一个「自动填入52张卡」按钮——
/// 从 Assets/Data/Cards/ 一次性读入全部卡牌，按 ♠♥♦♣、A→K 排好序，
/// 不用一张张拖。填完自动标脏，保存场景即永久生效。
/// </summary>
[CustomEditor(typeof(DeckManager))]
public class DeckManagerInspector : Editor
{
    const string CARD_PATH = "Assets/Data/Cards";

    public override void OnInspectorGUI()
    {
        var dm = (DeckManager)target;

        GUILayout.Space(4);
        GUI.backgroundColor = new Color(0.65f, 0.85f, 0.65f);
        if (GUILayout.Button("自动填入 52 张卡（读取 " + CARD_PATH + "）", GUILayout.Height(30)))
        {
            FillDeck(dm);
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.HelpBox(
            "按钮只做一次性填充；以后改了卡牌资产，再点一次即可覆盖。\n" +
            "前提：先跑过 Tools → 一键生成卡牌+觉醒技能。",
            MessageType.Info);
        GUILayout.Space(4);

        DrawDefaultInspector();
    }

    static void FillDeck(DeckManager dm)
    {
        string[] guids = AssetDatabase.FindAssets("t:CardDataSO", new[] { CARD_PATH });
        if (guids.Length == 0)
        {
            EditorUtility.DisplayDialog("找不到卡牌",
                CARD_PATH + " 里没有卡牌资产。\n请先点菜单 Tools → 一键生成卡牌+觉醒技能。", "好");
            return;
        }

        var cards = new List<CardDataSO>();
        foreach (string g in guids)
        {
            var c = AssetDatabase.LoadAssetAtPath<CardDataSO>(AssetDatabase.GUIDToAssetPath(g));
            if (c != null) cards.Add(c);
        }

        // 按 ♠♥♦♣、点数 A→K 排序
        cards.Sort((a, b) =>
        {
            int s = a.suit.CompareTo(b.suit);
            return s != 0 ? s : a.rank.CompareTo(b.rank);
        });

        Undo.RecordObject(dm, "自动填入52张卡");
        dm.deckCards = cards;
        EditorUtility.SetDirty(dm);
        Debug.Log("[牌组] 已自动填入 " + cards.Count + " 张卡到 DeckManager.deckCards");
    }
}
