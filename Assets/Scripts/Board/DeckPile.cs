using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class DeckPile : Singleton<DeckPile>
{
    protected override void Awake() 
    {
        base.Awake(); 
        BoxCollider collider = GetComponent<BoxCollider>();
        collider.isTrigger = true;
    }
    void OnMouseDown()
    {
        if (GameProgress.InputLocked)   // 卷轴地图打开/切关过渡时不能抽牌
        {
            Narrator.Say(SpeakTopic.ActionDuringMap);
            return;
        }

        DeckManager dm = DeckManager.Instance;
        if (dm == null) return;

    }
}
