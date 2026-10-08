using System.Collections.Generic;
using UnityEngine;

public class PotArea : MonoBehaviour
{
    private const int StacksPerPattern = 5;

    [Header("Coin layout")]
    public float coinHeight = 0.02f;
    public int coinsPerStack = 10;

    [Header("Five-stack pattern")]
    public float patternSpacing = 0.55f;
    public int groupsPerRow = 3;
    public float groupSpacing = 1.8f;
    public float groupRowSpacing = 1.8f;

    [Header("Before rearrange")]
    public float scatterRange = 0.2f;
    public ChipPool pool;

    private void Awake()
    {
        if (pool == null)
            pool = FindObjectOfType<ChipPool>();   // 场景里只有一个 ChipPool，自动捡
    }
    private readonly List<GameObject> potCoins =
        new List<GameObject>();

    public void ReceiveCoin(GameObject coin)
    {
        if (coin == null)
            return;

        if (!potCoins.Contains(coin))
            potCoins.Add(coin);

        coin.transform.SetParent(transform, true);

        Rigidbody rb = coin.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // 确保这枚筹码可以被单独点击
        PotCoinClick click = coin.GetComponent<PotCoinClick>();

        if (click == null)
            click = coin.AddComponent<PotCoinClick>();

        click.Initialize(this);
    }

    public void OnCoinClicked(GameObject clickedCoin)
    {
        if (clickedCoin == null)
            return;

        if (!potCoins.Contains(clickedCoin))
            return;

        Rearrange();
    }

    public void Rearrange()
    {
        coinsPerStack = Mathf.Max(1, coinsPerStack);
        groupsPerRow = Mathf.Max(1, groupsPerRow);

        for (int i = 0; i < potCoins.Count; i++)
        {
            GameObject coin = potCoins[i];

            if (coin == null)
                continue;

            int stackIndex = i / coinsPerStack;
            int level = i % coinsPerStack;

            int groupIndex = stackIndex / StacksPerPattern;
            int slotIndex = stackIndex % StacksPerPattern;

            Vector3 localPosition =
                GetLocalPosition(groupIndex, slotIndex, level);

            Quaternion localRotation = Quaternion.identity;

            Rigidbody rb = coin.GetComponent<Rigidbody>();

            coin.transform.SetParent(transform, false);

            if (rb != null)
            {
                rb.isKinematic = true;

                rb.position = transform.TransformPoint(localPosition);
                rb.rotation = transform.rotation;
            }
            else
            {
                coin.transform.localPosition = localPosition;
                coin.transform.localRotation = localRotation;
            }
        }
    }

    private Vector3 GetLocalPosition(
        int groupIndex,
        int slotIndex,
        int level)
    {
        Vector3 slotOffset = GetSlotOffset(slotIndex);

        int groupColumn = groupIndex % groupsPerRow;
        int groupRow = groupIndex / groupsPerRow;

        Vector3 groupOffset = new Vector3(
            groupColumn * groupSpacing,
            0f,
            groupRow * groupRowSpacing
        );

        return groupOffset
             + slotOffset
             + Vector3.up * (level * coinHeight);
    }

    private Vector3 GetSlotOffset(int slotIndex)
    {
        float s = patternSpacing;

        switch (slotIndex)
        {
            case 0:
                return new Vector3(-s, 0f, -s);

            case 1:
                return new Vector3(s, 0f, -s);

            case 2:
                return Vector3.zero;

            case 3:
                return new Vector3(-s, 0f, s);

            case 4:
                return new Vector3(s, 0f, s);

            default:
                return Vector3.zero;
        }
    }

    public void ClearCoins()
    {
         if (pool != null)
        {
            foreach (GameObject coin in potCoins)
                if (coin != null) pool.Return(coin);
        }
        potCoins.Clear();
    }

    public int GetCoinCount()
    {
        return potCoins.Count;
    }
}