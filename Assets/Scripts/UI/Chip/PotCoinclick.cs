using UnityEngine;

public class PotCoinClick : MonoBehaviour
{
    private PotArea potArea;
    private Chips chips;
    private GameObject coinRoot;

    // 主池筹码用
    public void Initialize(PotArea owner)
    {
        chips=null;
        potArea = owner;
        coinRoot = gameObject;
        
    }

    // 玩家筹码用（root 传筹码根物体，防止 Collider 在子物体时点错对象）
    public void Initialize(Chips owner, GameObject root)
    {
        potArea = null;      // 清掉旧归属
        chips = owner;
        coinRoot = root;
    }

    private void OnMouseDown()
    {
        if (coinRoot == null)
            return;

        if (potArea != null)
            potArea.OnCoinClicked(coinRoot);
        else if (chips != null)
            chips.OnCoinClicked(coinRoot);
    }
}
