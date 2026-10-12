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