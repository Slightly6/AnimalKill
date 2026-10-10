using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShopManager : Singleton<ShopManager>
{
    public List<JokerSO> jokerPool = new List<JokerSO>();
    public List<CardDataSO> CardsPool = new List<CardDataSO>();
    public GameObject cardPrefab;
    public GameObject hookCardPrefab; 
    // public GameObject hookCardPrefab;
    public int maxShopSize = 3;
    public float spacing = 1.5f;
    public Transform Anchor; 
    public List<Transform> shopCards=new List<Transform>();
    public void CardPoolInit()
    {
        
        Shuffle(CardsPool);
        int shopSize = Mathf.Min(maxShopSize, CardsPool.Count);
        
        GetPlayPositions(cardPrefab,shopSize,0);
    }
    public void JokerPoolInit()
    {
        Shuffle(jokerPool);
        int count = Mathf.Min(maxShopSize, jokerPool.Count);
        GetPlayPositions(hookCardPrefab,count,2.5f);
    }

    // 买小丑：成功（栏没满）返回 true，并把实体移出商店列表（不再被结算飞走）
    public bool BuyJoker(HookCard card)
    {
        bool ok = JokerManager.Instance.Add(card.Data, card.transform);   // 飞行/挂载动画由 HookDisplay 接管
        if (ok) shopCards.Remove(card.transform);
        return ok;
    }
    private void GetPlayPositions(GameObject cardPrefabrd, int count,float yPos=0f)
    {
        Vector3 anchorPos = Vector3.zero;
        if (Anchor != null) anchorPos = Anchor.position;
        else if (BoardManager.Instance != null) anchorPos = BoardManager.Instance.transform.position;
        Quaternion rotation = Quaternion.Euler(90, 0, 0);
        Vector3 center = new Vector3(anchorPos.x, 0f, anchorPos.z);
        for (int i = 0; i < count; i++)
        {
            float x = (i - (count - 1) / 2f) * spacing;
            GameObject cardObj = Instantiate(cardPrefabrd, center + new Vector3(x, 2f, yPos), rotation);
            if(cardPrefabrd==cardPrefab)
            {
                Card card = cardObj.GetComponent<Card>();
                card.Init(CardsPool[i], true); 
                card.transform.rotation = Quaternion.Euler(90f, 90f, -90f);
            }else
            {
                HookCard card = cardObj.GetComponent<HookCard>();
                card.Init(jokerPool[i]); 
                card.transform.rotation = Quaternion.Euler(90f, 90f, -90f);
            }
            
            shopCards.Add(cardObj.transform); 
        }
        
    }

    //洗牌
    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
}
