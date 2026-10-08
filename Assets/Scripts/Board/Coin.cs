using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Coin : Singleton<Coin>
{
    List<GameObject> potCoins = new List<GameObject>();
    int CoinsCount = -1;
    public GameObject coinPrefab;
    int currentCount = 0;
    public bool initialized = false;
    public float coinHeight = 0.04f;
    Quaternion rotation = Quaternion.Euler(0, 0, 90);
    void Start()
    {
        
        EventBus.Subscribe<LevelClearedEvent>(OnPointsChanged);
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<LevelClearedEvent>(OnPointsChanged);
    }
    void OnPointsChanged(LevelClearedEvent e)
    {
        foreach (var coin in potCoins)
            Destroy(coin);

        potCoins.Clear();
        initialized = false;
    }
    // Update is called once per frame
    void Update()
    {
        CoinsCount= potCoins.Count;
        currentCount = BattleManager.Instance.PointsLeft;
        if (currentCount != CoinsCount&& initialized)
        {
            
            CoinsCountChange();
        }
    }
    void CoinsCountChange()
    {
        while (CoinsCount < currentCount)
        {
            GameObject coin = Instantiate(coinPrefab, 
            new Vector3(transform.position.x, transform.position.y+CoinsCount*coinHeight*5, transform.position.z), 
            rotation,
            transform);
            StartCoroutine(PopIn(coin.transform, 0.5f));
            potCoins.Add(coin);
            CoinsCount++;
        }
        while (CoinsCount > currentCount)
        {
            GameObject coin = potCoins[potCoins.Count - 1];
            potCoins.RemoveAt(potCoins.Count - 1);
            Destroy(coin);
            CoinsCount--;
        }
       
    }
    private IEnumerator PopIn(Transform t, float dur)
    {
        Vector3 baseScale = t.localScale;  
        t.localScale = Vector3.zero;
        float time = 0f;
        while (time < dur)
        {
            time += Time.deltaTime;
            float p= Mathf.Clamp01(time/dur);
            float s = p < 0.5f ? Mathf.Lerp(0f, 1.3f, p * 2f) : Mathf.Lerp(1.3f, 1f, (p - 0.5f) * 2f);
            t.localScale = baseScale * s;
            yield return null;
        }
        t.localScale = baseScale;
    }
}
