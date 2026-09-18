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

        // 持有数已满 → 不让买
        if (GameProgress.ownedItems.Count >= MAX_ITEMS)
        {
            Debug.Log("[商店] 已持有 " + MAX_ITEMS + " 个道具，买不了更多");
            return;
        }

        isFlying = true;

        // 买下道具 → 直接存进 GameProgress，跨关保留
        if (goods.itemData != null)
        {
            GameProgress.ownedItems.Add(goods.itemData);
            Debug.Log("[商店] 买了 " + goods.itemData.itemName
                + "（持有 " + GameProgress.ownedItems.Count + "/" + MAX_ITEMS + "）");
        }
        else
        {
            Debug.LogWarning("Goods " + goods.name + " 没设置 itemData，购买不生效");
        }

        Transform target = dropTargets[Random.Range(0, dropTargets.Length)];
        goods.StartCoroutine(goods.FlyToTarget(target.position));
    }
    
}