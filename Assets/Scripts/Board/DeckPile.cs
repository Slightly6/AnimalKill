using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[RequireComponent(typeof(BoxCollider2D))]
public class DeckPile : MonoBehaviour
{
    void Awake()
    {
        BoxCollider2D collider = GetComponent<BoxCollider2D>(); 
        collider.isTrigger = true;
    }
    void OnMouseDown()
    {
        DeckManager dm = DeckManager.Instance;
        if (dm == null) return;

        // 检查手牌上限
        if (dm.HandCards.Count >= dm.maxHandSize)
        {
            Debug.Log("手牌已满");
            return;
        }

        StartCoroutine(dm.DrawCards(1));
    }
}
