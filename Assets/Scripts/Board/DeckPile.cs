using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[RequireComponent(typeof(BoxCollider2D))]
public class DeckPile : MonoBehaviour
{
    void Awake()
    {
        BoxCollider2D collider = GetComponent<BoxCollider2D>(); 
        collider.isTrigger = true;
    }
    void OnMouseDown()
    {
        DeckManager dm = DeckManager.Instance;
        if (dm == null) return;

        dm.TryDrawOne();
    }
}
