using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShopManager : Singleton<ShopManager>
{
    public List<HookCardDataSO> HookCards = new List<HookCardDataSO>();
    public List<CardDataSO> Cards = new List<CardDataSO>();
    public GameObject cardPrefab;
    // public GameObject hookCardPrefab;
    public int maxShopSize = 3;
    public float spacing = 1.5f;
    public Transform Anchor; 
    void Start()
    {
    //   CardPoolInit();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    void OnDestroy()
    {
        
    }
    public void CardPoolInit()
    {
        
        Shuffle(Cards);
        int shopSize = Mathf.Min(maxShopSize, Cards.Count);
        
        GetPlayPositions(shopSize);
    }
    private void GetPlayPositions(int count)
    {
        List<Vector3> list = new List<Vector3>();
        Vector3 anchorPos = Vector3.zero;
        if (Anchor != null) anchorPos = Anchor.position;
        else if (BoardManager.Instance != null) anchorPos = BoardManager.Instance.transform.position;
        Quaternion rotation = Quaternion.Euler(90, 0, 0);
        Vector3 center = new Vector3(anchorPos.x, 0f, anchorPos.z);
        for (int i = 0; i < count; i++)
        {
            float x = (i - (count - 1) / 2f) * spacing;
            GameObject cardObj = Instantiate(cardPrefab, center + new Vector3(x, 2f, 0f), rotation);
            Card card = cardObj.GetComponent<Card>();
            card.Init(Cards[i], true);  
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
