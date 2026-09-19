using UnityEngine;

/// <summary>
/// 商店道具数据。每个道具是一个 .asset 文件。
/// 效果由一个独立的 ItemEffectSO 资产决定，要什么效果就拖哪个 effect。
///
/// 用法：右键 Project → Data/Shop Item 创建，
/// 在 effect 字段拖入具体的效果资产（如 AddChipsEffect）。
/// </summary>
[CreateAssetMenu(fileName = "NewShopItem", menuName = "Data/Shop Item")]
public class ShopItemDataSO : ScriptableObject
{
    [Header("基础信息")]
    public string itemName = "新道具";
    [TextArea] public string description = "";
    public Sprite icon;           // 关卡内 HUD 显示的图标（不用可空）

    [Header("效果（拖入一个效果资产）")]
    public ItemEffectSO effect;   // 这个道具触发时执行的效果

    [Header("消耗")]
    public bool consumable = true; // true=一次性，触发后销毁桌上实体；false=可重复点击
}
