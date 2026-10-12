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
    private bool heightDetected;

    void DetectCoinHeight()
    {
        if (coinPrefab == null || heightDetected) return;
        MeshRenderer mr = coinPrefab.GetComponentInChildren<MeshRenderer>();
        if (mr != null)
        {
            coinHeight = mr.bounds.size.y;
            heightDetected = true;
        }
    }

    [Header("选牌预览浮起")]
    public float liftHeight = 0.15f;       // 浮起高度
    public float liftLerpSpeed = 12f;      // 平滑速度
    private int liftCount;                 // 当前应浮起的金币数
    private bool liftDirty;
    void Start()
    {
        DetectCoinHeight();
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
        UpdateLift();
    }

    /// <summary>选牌时让最顶上 count 枚金币浮起一点（预览消耗）</summary>
    public void LiftTop(int count)
    {
        liftCount = Mathf.Max(0, count);
        liftDirty = true;
    }

    private void UpdateLift()
    {
        liftDirty = false;

        // 最顶上 liftCount 枚浮起
        for (int i = 0; i < potCoins.Count; i++)
        {
            GameObject coin = potCoins[i];
            if (coin == null) continue;

            // 列表顺序：先加入的在底部，后加入的在顶部
            // 最顶上 liftCount 枚 = 索引 [Count - liftCount, Count)
            bool shouldLift = liftCount > 0 && i >= potCoins.Count - liftCount;

            Vector3 basePos = new Vector3(
                transform.position.x,
                transform.position.y + i * coinHeight,
                transform.position.z);
            Vector3 target = shouldLift
                ? basePos + Vector3.up * liftHeight
                : basePos;

            coin.transform.position = Vector3.Lerp(
                coin.transform.position,
                target,
                liftLerpSpeed * Time.deltaTime);
        }
    }
    void CoinsCountChange()
    {
        while (CoinsCount < currentCount)
        {
            GameObject coin = Instantiate(coinPrefab, 
            new Vector3(transform.position.x, transform.position.y+CoinsCount*coinHeight, transform.position.z), 
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
