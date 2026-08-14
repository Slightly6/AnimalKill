using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 52 关的大数组。Unity 里 Create → Data → 关卡数据库 建一个，
/// 展开 levels 逐关填，拖到 MapManager 上。
/// </summary>
[CreateAssetMenu(fileName = "LevelDatabase", menuName = "Data/关卡数据库")]
public class LevelDatabase : ScriptableObject
{
    public List<LevelConfig> levels = new List<LevelConfig>();   // 52 关
}
