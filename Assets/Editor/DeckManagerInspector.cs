using UnityEditor;

/// <summary>
/// DeckManager 自定义 Inspector：
/// 原「自动填入52张卡」按钮已移除——牌组数量由 Inspector 的 deckCards 列表手动决定，几张都行。
/// </summary>
[CustomEditor(typeof(DeckManager))]
public class DeckManagerInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
    }
}
