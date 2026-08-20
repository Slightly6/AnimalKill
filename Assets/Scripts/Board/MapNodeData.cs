using System.Collections.Generic;

/// <summary>
/// 地图上一个节点的数据（纯数据，不挂场景）。
/// 存进 GameProgress 跨场景保留。[System.Serializable] 让 JsonUtility 能存它。
/// </summary>
[System.Serializable]
public class MapNodeData
{
    public NodeType type;      // 节点类型
    public int levelIndex;     // 战斗/Boss 打第几关；其他 = -1
    public int row;            // 第几横排（0 = 最下面 A，往上递增）
    public int col;            // 这一排第几个（从左往右）
    public List<int> nextCols = new List<int>();   // 下一排（row+1）能走到的列（进地图时算好，点击判定用）
}
