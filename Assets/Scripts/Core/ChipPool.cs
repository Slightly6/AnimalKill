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
        // 分帧预热，避免一帧内生成太多导致卡顿
        StartCoroutine(WarmupRoutine());
    }

    private System.Collections.IEnumerator WarmupRoutine()
    {
        int perFrame = 2;   // 每帧生成数量，越小越平滑
        for (int i = 0; i < initialSize; i += perFrame)
        {
            for (int j = 0; j < perFrame && i + j < initialSize; j++)
            {
                GameObject coin = Instantiate(coinPrefab, transform);
                coin.SetActive(false);
                pool.Enqueue(coin);
            }
            yield return null;   // 等下一帧再继续
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
        // 池子自己正在销毁时别再挂父（Unity 禁止，且会打断还池流程），反正马上一起销毁
        if (!destroying)
            coin.transform.SetParent(transform, true);   // 收回池子名下，别留在敌人堆/主池下面
        inUse.Remove(coin);
        pool.Enqueue(coin);
    }

    private bool destroying;

    void OnDestroy()
    {
        destroying = true;
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