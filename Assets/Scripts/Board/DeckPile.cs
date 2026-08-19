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
        DeckManager dm = DeckManager.Instance;
        if (dm == null) return;

        dm.TryDrawOne();
    }
}
