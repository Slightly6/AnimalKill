using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 地图节点（由 MapGenerator 生成，带 BoxCollider2D）。
/// 只有"当前横排"的节点才能点。
/// 点战斗/Boss → 跳战斗场景；点分岔节点 → 占位。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class MapNode : MonoBehaviour
{
    public NodeType type = NodeType.Battle;
    public int levelIndex = 0;
    public int row = 0;

    public string battleSceneName = "SampleScene";
    public string mapSceneName = "Map";

    private SpriteRenderer spriteRenderer;

    void Awake()
    {
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    // 生成器调用：告诉这个节点它是谁
    public void Setup(MapNodeData data)
    {
        type = data.type;
        levelIndex = data.levelIndex;
        row = data.row;
        RefreshVisual();
    }

    // 按类型 + 是否可点，刷新颜色
    void RefreshVisual()
    {
        if (spriteRenderer == null) return;

        if (type == NodeType.Boss) spriteRenderer.color = Color.red;
        else if (type == NodeType.Shop) spriteRenderer.color = Color.yellow;
        else if (type == NodeType.Upgrade) spriteRenderer.color = Color.cyan;
        else if (type == NodeType.Extra) spriteRenderer.color = Color.green;
        else spriteRenderer.color = Color.white;

        // 不是当前横排，整体变暗（表示还不能选）
        if (row != GameProgress.mapRow)
        {
            Color c = spriteRenderer.color;
            c.r *= 0.4f; c.g *= 0.4f; c.b *= 0.4f;
            spriteRenderer.color = c;
        }
    }

    void OnMouseDown()
    {
        // 不是当前横排，不让点
        if (row != GameProgress.mapRow)
        {
            Debug.Log("这排还不能选");
            return;
        }

        // 前进到下一排
        GameProgress.mapRow = row + 1;

        if (type == NodeType.Battle || type == NodeType.Boss)
        {
            GameProgress.currentLevel = levelIndex;
            SceneManager.LoadScene(battleSceneName);
        }
        else
        {
            Debug.Log("分岔节点（占位）：" + type);
            SceneManager.LoadScene(mapSceneName);   // 重进地图刷新（占位做法）
        }
    }
}
