using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class DeckPile : MonoBehaviour
{
    void Awake()
    {
        BoxCollider collider = GetComponent<BoxCollider>();
        collider.isTrigger = true;
    }
    void OnMouseDown()
    {
        if (GameProgress.InputLocked) return;   // 卷轴地图打开/切关过渡时不能抽牌

        DeckManager dm = DeckManager.Instance;
        if (dm == null) return;

        dm.TryDrawOne();
    }
}
