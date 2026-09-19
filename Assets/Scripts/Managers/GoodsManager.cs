using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GoodsManager : Singleton<GoodsManager>
{
    [Header("检测设置")]
    public LayerMask goodsLayer;        // ★ 只检测商品层
    public Camera rayCamera;            // ★ 从哪个相机发射线（不拖就用 Camera.main）
    public float rayDistance = 100f;    // 射线距离

    private Goods currentHover;         // 当前悬停的商品
    private bool isFlying = false;
    public Transform[] dropTargets = new Transform[4];

    private const int MAX_ITEMS = 4;    // 最多只能拿 4 个道具
    void Start()
    {
        if (rayCamera == null) rayCamera = Camera.main;
    }

    void Update()
    {
        // 只在商店节点检测商品：战斗关/卷轴打开/切关过渡时一律不能买
        if (GameProgress.currentNodeType != NodeType.Shop) return;

        if (GameProgress.InputLocked)   // 卷轴地图打开/切关过渡时：商店悬停/购买射线全部停掉
        {
            if (currentHover != null) { OnHoverExit(currentHover); currentHover = null; }
            return;
        }

        // 每帧发射线，检测鼠标下面的商品（悬停用）
        Ray ray = rayCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, rayDistance, goodsLayer))
        {
            Goods goods = hit.collider.GetComponent<Goods>();
            print("meeidao");
            if (goods != null && goods != currentHover)
            {
                // 换了新商品，处理悬停
                currentHover = goods;
                OnHoverEnter(goods);
            }
        }
        else
        {
            // 没打到商品
            if (currentHover != null)
            {
                OnHoverExit(currentHover);
                currentHover = null;
            }
        }

        // 点击
        if (Input.GetMouseButtonDown(0))
        {
            if (currentHover != null)
            {
                OnClick(currentHover);
            }
        }
    }

    void OnHoverEnter(Goods goods)
    {
        Debug.Log("悬停: " + goods.name);
        // 悬停高亮、放大、显示提示
    }

    void OnHoverExit(Goods goods)
    {
        Debug.Log("离开: " + goods.name);
        // 取消高亮
    }

    void OnClick(Goods goods)
    {
        if (isFlying) return;

        // 持有数 = 桌上实际存在的 TableItem 实体数（不再记 GameProgress.ownedItems）
        TableItem[] tableItems = FindObjectsOfType<TableItem>();
        if (tableItems.Length >= MAX_ITEMS)
        {
            Debug.Log("[商店] 桌上已有 " + MAX_ITEMS + " 个道具，买不了更多");
            return;
        }

        if (goods.itemData == null)
        {
            Debug.LogWarning("Goods " + goods.name + " 没设置 itemData，购买不生效");
            return;
        }

        isFlying = true;
        Debug.Log("[商店] 买了 " + goods.itemData.itemName
            + "（桌上 " + (tableItems.Length + 1) + "/" + MAX_ITEMS + "）");

        // 飞到空落点；协程挂在 Manager 上，这样落地销毁 Goods 组件不会中断协程。
        // 包一层：飞行动画（含落地 2 秒）走完再解锁，否则下一家商店 isFlying 一直是 true 买不了。
        Transform target = PickEmptyDropTarget(tableItems);
        StartCoroutine(PurchaseAndUnlock(goods, target.position));
    }

    IEnumerator PurchaseAndUnlock(Goods goods, Vector3 targetPos)
    {
        yield return goods.FlyToTarget(targetPos);
        isFlying = false;
    }

    // 找一个没放道具的落点（4 个槽位按距离判定占用），全满时兜底随机
    Transform PickEmptyDropTarget(TableItem[] tableItems)
    {
        for (int i = 0; i < dropTargets.Length; i++)
        {
            Transform t = dropTargets[i];
            if (t == null) continue;

            bool occupied = false;
            for (int j = 0; j < tableItems.Length; j++)
            {
                if (tableItems[j] == null) continue;
                if (Vector3.Distance(tableItems[j].transform.position, t.position) < 0.6f)
                {
                    occupied = true;
                    break;
                }
            }
            if (!occupied) return t;
        }
        return dropTargets[Random.Range(0, dropTargets.Length)];
    }

}