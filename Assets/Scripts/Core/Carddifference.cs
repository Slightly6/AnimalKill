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