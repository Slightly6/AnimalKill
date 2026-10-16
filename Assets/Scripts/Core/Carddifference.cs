using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[RequireComponent(typeof(Card))]
public class CardDifference : MonoBehaviour
{
    private CardDisplay battleHandler;
    private ShopCard shopHandler;

    void Awake()
    {
        battleHandler = GetComponent<CardDisplay>();
        shopHandler = GetComponent<ShopCard>();
    }

    void OnMouseDown()
    {
        if (GameProgress.InputLocked) return;
        if (EnhanceManager.Choosing) return;   // 强化三选一：点击只走 EnhanceCardClick，别碰出牌/购买分流
        if (TryGetComponent<EnhanceCardClick>(out _)) return;   // 候选卡替身：一辈子只走强化，任何时候都不做出牌/购买（防同帧 Choosing 已被置 false 的竞态）

        if (GameProgress.currentNodeType == MapNodeType.Shop)
        {
            if (shopHandler != null) shopHandler.OnClick();
        }
        else
        {
            if (battleHandler != null) battleHandler.OnClick();
        }
    }
}