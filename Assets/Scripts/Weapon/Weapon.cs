using UnityEngine;

/// <summary>
/// 武器：挂在每个武器预制体根上。
/// 每个武器预制体 = 模型 + 自己的 Animator + 这个 Weapon 组件。
/// 每把武器有自己的收纳节点（背/腰间），拔刀挂手，收刀收回收纳节点。
/// </summary>
public class Weapon : MonoBehaviour
{
    [Header("武器名")]
    public string weaponName = "武器";

    [Header("这把武器收刀时挂哪（它自己的背后/腰间节点）")]
    public Transform backNode;   // 每把武器自己的收纳位置

    void Start()
    {
        // 一开始先挂到自己的收纳节点
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

    // 收刀：挂回自己的收纳节点
    public void Sheathe()
    {
        if (backNode != null) Attach(backNode);
    }

    // 拔刀：挂到手上节点
    public void Draw(Transform handNode)
    {
        if (handNode != null) Attach(handNode);
    }

    // 攻击：播放这个武器自己的攻击动画
    public void Attack()
    {
    }
}
