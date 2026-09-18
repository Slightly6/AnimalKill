using UnityEngine;

/// <summary>
/// 宝箱奖励卡的点击转发：点这张卡时，交给宝箱处理（翻面/放大、再点选中）。
/// </summary>
public class ChestCardClick : MonoBehaviour
{
    public Chest chest;
    private Card card;

    void Awake()
    {
        card = GetComponent<Card>();
    }

    void OnMouseDown()
    {
        if (GameProgress.InputLocked) return;   // 卷轴地图打开/切关过渡时不能选宝箱卡

        if (chest != null && card != null)
            chest.OnRewardCardClicked(card);
    }
}
