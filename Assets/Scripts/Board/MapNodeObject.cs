using UnityEngine;

/// <summary>
/// 地图节点。挂在节点 GameObject 上（带 BoxCollider2D）。
/// 点击后交给 MapManager 处理。
/// 节点视觉（长啥样、怎么摆）后期用户自己设计，这里只管数据 + 点击。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class MapNodeObject : MonoBehaviour
{
    public NodeType type;       // 节点类型
    public int levelIndex;      // 战斗节点对应第几关（升级节点填 -1）

    void OnMouseDown()
    {
        if (MapManager.Instance != null)
            MapManager.Instance.OnNodeClicked(this);
    }
}
