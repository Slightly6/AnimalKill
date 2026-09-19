using UnityEngine;

/// <summary>
/// 桌子右侧的已购道具实体。
/// 挂在商店买的、掉落到你桌子右侧的那个道具物体上。
/// 只能在战斗关的摸牌/出牌阶段（TurnPhase.Draw/Play），或非战斗节点（商店/升级/宝箱）点击；
/// 战斗结算 Battle、回合结束 End 阶段不可点击。点击 → 发布 ItemActivatedEvent → GameManager 应用效果。
/// 实体随存档保存（SaveManager 扫描实体存资产名，开局按名恢复）。
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
        if (GameProgress.InputLocked)   // 卷轴地图打开/切关过渡时禁用一切点击
        {
            Narrator.Say(SpeakTopic.ActionDuringMap);
            return;
        }

        // 非战斗节点（商店/升级/宝箱）没有回合阶段机，随时可点；
        // 战斗关（Battle/Boss/小关）只能在摸牌 Draw、出牌 Play 阶段点，战斗结算 Battle / 结束 End 不能点。
        bool nonBattleNode = GameProgress.IsNonBattleNode()
                          || GameProgress.currentNodeType == NodeType.Chest;
        if (!nonBattleNode)
        {
            if (BattleManager.Instance == null) return;
            TurnPhase p = BattleManager.Instance.CurrentPhase;
            if (p != TurnPhase.Draw && p != TurnPhase.Play)
            {
                Narrator.Say(SpeakTopic.WrongPhase_Item);
                return;
            }
        }

        if (itemData == null)
        {
            Debug.LogWarning("桌子上的道具没设置 itemData");
            return;
        }

        // 发布激活事件，GameManager 会听到并应用效果
        EventBus.Publish(new ItemActivatedEvent { item = itemData });
        Debug.Log("[道具] 点击使用 " + itemData.itemName);

        // 消耗品：用完销毁桌上实体（数据就在实体上，不维护任何持有列表）
        if (itemData.consumable)
        {
            Destroy(gameObject);
        }
    }
}
