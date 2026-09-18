using UnityEngine;

/// <summary>
/// 桌子右侧的已购道具实体。
/// 挂在商店买的、掉落到你桌子右侧的那个道具物体上。
/// 点击 → 发布 ItemActivatedEvent → GameManager 应用效果（解耦）。
/// consumable=true 的道具用完就删掉。
///
/// 用法：
///   1. 把这个脚本挂到桌子右侧道具物体上（需要有 Collider 才能点）
///   2. 生成这个物体时调用 Setup(itemData) 把道具数据塞进来
///      （或者在 Inspector 直接拖 itemData）
/// </summary>
[RequireComponent(typeof(Collider))]
public class TableItem : MonoBehaviour
{
    public ShopItemDataSO itemData;   // 道具数据（运行时由生成方 Setup，或 Inspector 拖）

    // 生成方调用：注入道具数据
    public void Setup(ShopItemDataSO data)
    {
        itemData = data;
    }

    void OnMouseDown()
    {
        if (GameProgress.InputLocked) return;   // 卷轴地图打开/切关过渡时禁用一切战斗点击

        if (itemData == null)
        {
            Debug.LogWarning("桌子上的道具没设置 itemData");
            return;
        }

        // 发布激活事件，GameManager 会听到并应用效果
        EventBus.Publish(new ItemActivatedEvent { item = itemData });
        Debug.Log("[道具] 点击使用 " + itemData.itemName);

        // 消耗品：从 GameProgress 里移除，并销毁自己
        if (itemData.consumable)
        {
            GameProgress.ownedItems.Remove(itemData);
            Destroy(gameObject);
        }
    }
}
