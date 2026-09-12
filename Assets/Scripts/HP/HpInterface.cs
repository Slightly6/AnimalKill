using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface HpInterface
{
    int MaxHp { get; }
    int CurrentHp { get; }

    void TakeDamage(float damage);
}
