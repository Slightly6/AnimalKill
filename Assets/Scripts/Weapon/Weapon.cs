using UnityEngine;

/// <summary>
/// 武器：挂在每个武器预制体根上。
/// 每把武器自己设置两个节点：收纳节点（收刀挂哪）+ 手上节点（拿刀挂哪）。
/// 拔刀挂手上节点，收刀挂回收纳节点。
/// 数值（伤害、攻速等）放在 WeaponData（ScriptableObject）里，用 data 引用。
/// </summary>
public class Weapon : MonoBehaviour
{
    [Header("武器数据（伤害、攻速等，拖一个 WeaponData 资源进来）")]
    public WeaponData data;

    [Header("这把武器收刀时挂哪（背后/腰间节点）")]
    public Transform backNode;

    [Header("这把武器拿刀时挂哪（手上节点）")]
    public Transform handNode;

    void Start()
    {
        // 一开始先挂到收纳节点
        if (backNode != null) Attach(backNode);
    }

    // 换父：挂到某个节点下，归零本地坐标
    public void Attach(Transform node)
    {
        if (node == null) return;
        transform.SetParent(node);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }

    // 收刀：挂回收纳节点
    public void Sheathe()
    {
        if (backNode != null) Attach(backNode);
    }

    // 拿刀：挂到手上节点
    public void Draw()
    {
        if (handNode != null) Attach(handNode);
    }
}
