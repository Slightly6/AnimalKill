using System.Collections.Generic;
using System;
using UnityEngine;
public enum MapNodeType
{
    Monster,	//普通战斗
    Elite,	//精英战斗，难，奖励好
    Event,	//随机事件
    Rest,	//休息，回血
    Shop,	//商店，买道具
    Treasure,	//宝箱，开奖励
    Boss	//Boss 战
}

[System.Serializable]
public class MapNodeData
{
    public int id;
    public int floor;      // 第几行（0=起点行，最后一行=Boss）
    public int col;        // 在第几列网格（0~columnCount-1）
    public MapNodeType type;

    // 本章第几个战斗节点（Monster/Elite/Boss 从 0 递增，决定 LevelDatabase 索引）；非战斗节点为 -1
    public int levelIndex = -1;

    // 节点在地图物体局部坐标中的位置
    public Vector3 localPosition;

    public List<int> nextNodeIds = new List<int>();
    public List<int> previousNodeIds = new List<int>();

    public bool visited;

    // 运行时状态，不存档
    [NonSerialized] public bool available;
    [NonSerialized] public MapNode3D view;
}
