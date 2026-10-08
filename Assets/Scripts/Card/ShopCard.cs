using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class ShopCard : MonoBehaviour
{
    public Transform targetPosition;   
    
    void Start()
    {
        targetPosition = DeckPile.Instance.transform;
    }
    public void OnClick()
    {
     
        Card card = GetComponent<Card>();
        if (card == null || card.Data == null) return;

        DeckManager.Instance.AddToDeck(card.Data);
        Debug.Log("已购买卡牌：" + card.Data.name);
        
        StartCoroutine(FlyStraight(card.transform, targetPosition.position, 0.5f));
        ShopManager.Instance.shopCards.Remove(card.transform);
        ShopManager.Instance.Cards.Remove(card.Data);
        
    }
    public IEnumerator FlyStraight(Transform card, Vector3 targetPos, float duration = 0.2f)
    {
        Vector3 startPos = card.position;
        Quaternion startRot = card.rotation;
        Quaternion endRot = Quaternion.Euler(-90f, 90f, -90f);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float p = Mathf.Clamp01(t);

            // 位置：直线插值
            card.position = Vector3.Lerp(startPos, targetPos, p);

            // 旋转：翻 180°
            card.rotation = Quaternion.Slerp(startRot, endRot, p);

            yield return null;
        }

        card.position = targetPos;
        card.rotation = endRot;
        Destroy(card.gameObject);
    }
}
