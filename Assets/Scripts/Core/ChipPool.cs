using System.Collections.Generic;
using UnityEngine;

public class ChipPool : MonoBehaviour
{
    public GameObject coinPrefab;
    public int initialSize = 100;

    private Queue<GameObject> pool = new Queue<GameObject>();
    private HashSet<GameObject> inUse=new HashSet<GameObject>();

    void Start()
    {
        // 预热
        for (int i = 0; i < initialSize; i++)
        {
            GameObject coin = Instantiate(coinPrefab, transform);
            coin.SetActive(false);
            pool.Enqueue(coin);
        }
    }

    // 取
    public GameObject Get()
    {
        GameObject coin;
        if (pool.Count > 0) 
        {
            coin = pool.Dequeue();
        }
        else {
            coin = Instantiate(coinPrefab, transform);
        }
        coin.SetActive(true);
        inUse.Add(coin);   
        return coin;
    }

    // 还
    public void Return(GameObject coin)
    {
        coin.SetActive(false);
        inUse.Remove(coin);
        pool.Enqueue(coin);
    }
    void OnDestroy()
    {
        ReturnAll();
    }
    public void ReturnAll()
    {
        List<GameObject>list=new List<GameObject>(inUse);
        foreach (GameObject coin in list)
        {
            Return(coin);
        }
    }
}