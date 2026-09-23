using UnityEngine;

/// <summary>
/// 桌面锚点管理。槽位机制已删除（手牌制：拖牌上桌=打出）。
/// 目前只保留 boardHeight（拖拽射线平面高度，CardDisplay 在用）。
/// 后续桌中结算区 / 公共筹码区 / 双方弃牌堆的锚点都加在这里。
/// </summary>
public class BoardManager : Singleton<BoardManager>
{
    [Header("桌面高度（整张桌抬多高，Y=0 留给 3D 战斗地面）")]
    public float boardHeight = 10f;
}
