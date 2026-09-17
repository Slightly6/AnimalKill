using UnityEngine;

/// <summary>
/// 商店道具效果的抽象基类（策略模式）。
/// 每个具体效果是一个子类 ScriptableObject，重写 Apply() 实现效果。
///
/// 要加新效果：
///   1. 继承 ItemEffectSO，写一个新的 .cs
///   2. 加 [CreateAssetMenu] 特性
///   3. 在 Project 窗口右键创建对应 .asset
///   4. 把 .asset 拖到 ShopItemDataSO 的 effect 字段
/// 完全不用改 GameManager / ShopItemDataSO。
/// </summary>
public abstract class ItemEffectSO : ScriptableObject
{
    /// <summary>
    /// 应用效果。可以自由访问 GameManager.Instance / BattleManager.Instance /
    /// BoardManager.Instance / GameProgress 等全局单例。
    /// </summary>
    public abstract void Apply();
}
