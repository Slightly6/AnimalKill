using UnityEngine;

/// <summary>
/// 桌面上的一个 3D 地图节点。
/// 预制体：根物体挂本脚本 + BoxCollider + SpriteRenderer（旋转 X=90 躺平）；
/// 或者 SpriteRenderer 挂在子物体 Icon 上也行，会自动找到。
/// 7 种节点的图片在 TableMapGenerator 的 Node Sprites 列表里拖。
/// </summary>
public class MapNode3D : MonoBehaviour
{
    [Header("状态颜色（染在图片上）")]
    [SerializeField] private Color lockedColor =
        new Color(0.2f, 0.2f, 0.2f, 0.55f);   // 不可进入：暗 + 半透明

    [SerializeField] private Color availableColor =
        Color.white;                          // 可进入：原色

    [SerializeField] private Color visitedColor =
        new Color(0.45f, 0.8f, 0.45f, 1f);    // 已走过：绿灰

    [SerializeField] private Color currentColor =
        new Color(1f, 0.8f, 0.2f, 1f);        // 当前所在：金色

    private SpriteRenderer iconRenderer;
    private MapNodeData data;
    private TableMapGenerator owner;

    public int NodeId => data != null ? data.id : -1;

    private void Awake()
    {
        CacheRenderer();
    }

    // 优先用子物体 Icon 上的渲染器，没有就用自己身上的
    private void CacheRenderer()
    {
        if (iconRenderer != null) return;

        Transform iconChild = transform.Find("Icon");
        if (iconChild != null)
            iconRenderer = iconChild.GetComponent<SpriteRenderer>();

        if (iconRenderer == null)
            iconRenderer = GetComponentInChildren<SpriteRenderer>(true);

        if (iconRenderer == null)
            iconRenderer = GetComponent<SpriteRenderer>();
    }

    // 生成器调用：icon = 该节点类型对应的图片
    public void Setup(
        MapNodeData nodeData,
        TableMapGenerator mapOwner,
        Sprite icon)
    {
        data = nodeData;
        owner = mapOwner;

        CacheRenderer();

        if (iconRenderer != null && icon != null)
            iconRenderer.sprite = icon;

        Refresh(false);
    }

    public void Refresh(bool isCurrent)
    {
        if (data == null)
            return;

        Color color;

        if (isCurrent)
            color = currentColor;
        else if (data.visited)
            color = visitedColor;
        else if (data.available)
            color = availableColor;
        else
            color = lockedColor;

        if (iconRenderer != null)
            iconRenderer.color = color;
    }

    private void OnMouseDown()
    {
        if (owner == null || data == null)
            return;

        owner.TrySelectNode(data.id);
    }
}
