using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// UI 地图节点（Button）。替代 3D 的 MapNode，用在卷轴 UI 里。
/// 点击逻辑和 MapNode.OnMouseDown 完全一致。
/// </summary>
[RequireComponent(typeof(Image))]
[RequireComponent(typeof(Button))]
public class UIMapNode : MonoBehaviour, IPointerClickHandler
{
    public MapNodeData data;

    [Header("各类型节点图标（可不拖，用颜色区分）")]
    public Sprite battleSprite;
    public Sprite bossSprite;
    public Sprite shopSprite;
    public Sprite upgradeSprite;
    public Sprite extraSprite;
    public Sprite chestSprite;
    public Sprite eliteSprite;
    public Sprite altarSprite;
    public Sprite eventSprite;
    public Sprite restSprite;

    private Image icon;
    private Button button;

    void Awake()
    {
        icon = GetComponent<Image>();
        button = GetComponent<Button>();
    }

    public void Setup(MapNodeData nodeData)
    {
        data = nodeData;
        RefreshVisual();
    }

    public void RefreshVisual()
    {
        if (icon == null) icon = GetComponent<Image>();

        // 按类型换图
        Sprite s = SpriteFor(data.type);
        if (s != null) icon.sprite = s;

        // 可选/不可选：颜色明暗
        bool selectable = GameProgress.cheatMode || GameProgress.CanSelectNode(data.row, data.col);

        // 新类型没配图标时用类型色兜底（精英红/祭坛紫/事件青/篝火绿）
        Color typeColor = TypeTint(data.type);
        if (selectable)
            icon.color = typeColor;
        else
            icon.color = new Color(typeColor.r * 0.35f, typeColor.g * 0.35f, typeColor.b * 0.35f, 1f);

        if (button != null)
            button.interactable = selectable;
    }

    Color TypeTint(NodeType type)
    {
        switch (type)
        {
            default: return Color.white;
        }
    }

    Sprite SpriteFor(NodeType type)
    {
        switch (type)
        {
            case NodeType.Boss: return bossSprite;
            case NodeType.Shop: return shopSprite;
            case NodeType.Upgrade: return upgradeSprite;
            case NodeType.Extra: return extraSprite;
            case NodeType.Chest: return chestSprite;
            default: return battleSprite;
        }
    }

    // 用 IPointerClickHandler 而不是 Button.onClick，逻辑和旧 MapNode 保持一致
    public void OnPointerClick(PointerEventData eventData)
    {
        if (data == null) return;

        // 开挂模式随便点；否则只能点当前横排、且上一排选的线能走到
        if (!GameProgress.cheatMode && !GameProgress.CanSelectNode(data.row, data.col))
        {
            Narrator.Say(SpeakTopic.NodeLocked);   // "这条线走不通"
            return;
        }

        // 选关瞬间上锁：卷轴飞出 + 新关布阵期间禁止一切战斗交互。
        // 此时 mapOpen 仍为 true（卷轴还在），飞出动画结束它变 false，transitioning 接力；
        // 战斗关由 BattleManager 进入出牌阶段解锁，非战斗节点在 SelectNode 里立即解锁。
        GameProgress.transitioning = true;

        // 同场景选关：不切场景，MapManager 直接摆下一关/商店/宝箱
        if (MapManager.Instance != null)
            MapManager.Instance.SelectNode(data);
        else
            Debug.LogError("[卷轴地图] 场景里没有 MapManager，无法进入节点");

        // 卷轴向上飞走（新关在卷轴遮挡下完成布阵/发牌）
        var ui = FindObjectOfType<MapScrollUI>();
        if (ui != null) ui.CloseMap();
    }
}
