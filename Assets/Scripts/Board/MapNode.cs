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

    // 类型靠"换图"区分，这里只做：不是当前横排就变暗
    void RefreshVisual()
    {
        if (spriteRenderer == null) return;

        if (row == GameProgress.mapRow)
        {
            spriteRenderer.color = Color.white;                      // 可点：正常亮
        }
        else
        {
            spriteRenderer.color = new Color(0.4f, 0.4f, 0.4f, 1f); // 不可点：变暗
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
