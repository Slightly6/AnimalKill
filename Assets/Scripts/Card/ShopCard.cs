using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class ShopCard : MonoBehaviour
{
    void Start()
    {
        
    }
    public void OnClick()
    {
     
        Card card = GetComponent<Card>();
        if (card == null || card.Data == null) return;

        DeckManager.Instance.AddToDeck(card.Data);
        Debug.Log("已购买卡牌：" + card.Data.name);
        Destroy(gameObject);
    }
}
