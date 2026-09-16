using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 管「武器池 + 拥有哪些武器 + 把武器装到装备格」。
/// 装备格固定 3 个（在 WeaponManager 里）：格0 = 近战，格1 = 远程，格2 = 空着。
/// 背包全显示拥有的武器，点一把就自动装到它对应类型的装备格。
/// </summary>
public class InventoryManager : MonoBehaviour
{
    [Header("武器管理器（不拖自动找，和本脚本挂同一物体）")]
    public WeaponManager weaponManager;

    [Header("武器池（所有武器数据，中刀/狼牙锤/弓箭都拖这）")]
    public WeaponData[] weaponPool;

    [Header("初始近战武器（武器池下标，0 = 中刀）")]
    public int startingMeleeIndex = 0;

    [Header("初始远程武器（武器池下标，弓箭；没有就 -1）")]
    public int startingRangedIndex = -1;

    [Header("Boss 掉落的武器（武器池下标，1 = 狼牙锤）")]
    public int bossDropWeaponIndex = 1;

    private List<int> ownedIndices = new List<int>();   // 拥有的武器池下标
    private bool bossWeaponCollected = false;           // Boss 武器拿到没

    void Start()
    {
        // 没手动拖就试着在同物体上找。
        // 注意：现在本脚本单独挂一个物体，WeaponManager 在 Hero 上，不在同一物体，
        // 所以这里 GetComponent 一定找不到，必须手动把 Hero 的 WeaponManager 拖进来。
        if (weaponManager == null) weaponManager = GetComponent<WeaponManager>();

        // 找不到武器管理器，后面装备会空引用报错，这里先拦住并提示
        if (weaponManager == null)
        {
            Debug.LogError("InventoryManager 找不到 WeaponManager！请把 Hero 上的 WeaponManager 拖进 weaponManager 字段");
            return;
        }

        // 初始武器直接加（不弹「获得」提示）
        if (!ownedIndices.Contains(startingMeleeIndex)) ownedIndices.Add(startingMeleeIndex);
        if (startingRangedIndex >= 0 && !ownedIndices.Contains(startingRangedIndex)) ownedIndices.Add(startingRangedIndex);

        // 初始武器装到对应装备格
        EquipToSlot(startingMeleeIndex);
        if (startingRangedIndex >= 0) EquipToSlot(startingRangedIndex);

        EventBus.Subscribe<DiedEvent>(OnBossDied);
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<DiedEvent>(OnBossDied);
    }

    // Boss 死 → 掉落武器进背包
    void OnBossDied(DiedEvent e)
    {
        if (e.isPlayer) return;
        if (bossWeaponCollected) return;

        bossWeaponCollected = true;
        AddWeapon(bossDropWeaponIndex);
    }

    // 加入背包（弹提示）
    public void AddWeapon(int index)
    {
        if (ownedIndices.Contains(index)) return;

        ownedIndices.Add(index);

        string name = GetWeaponName(index);
        EventBus.Publish(new WeaponGetEvent { weaponName = name });
        EventBus.Publish(new InventoryChangedEvent());
    }

    // 背包点武器 → 装到对应类型装备格（近战→格0，远程→格1）
    public void EquipToSlot(int weaponIndex)
    {
        WeaponData data = GetPoolWeapon(weaponIndex);
        if (data == null) return;

        int slot = -1;
        if (data.weaponType == WeaponType.Melee) slot = 0;
        else if (data.weaponType == WeaponType.Ranged) slot = 1;
        if (slot < 0) return;

        weaponManager.SetSlotData(slot, data);   // 装进装备格
        weaponManager.Equip(slot);               // 切过去，让玩家看到刚装的武器
        weaponManager.DrawWeapon();              // 拔刀

        EventBus.Publish(new InventoryChangedEvent());
    }

    // 判断某武器是不是已装在装备格（背包格子高亮用）
    public bool IsEquipped(int weaponIndex)
    {
        WeaponData data = GetPoolWeapon(weaponIndex);
        if (data == null) return false;
        if (data.weaponType == WeaponType.Melee) return weaponManager.GetSlotData(0) == data;
        return weaponManager.GetSlotData(1) == data;
    }

    // 武器池按下标拿数据（可能 null）
    public WeaponData GetPoolWeapon(int index)
    {
        if (weaponPool == null) return null;
        if (index < 0 || index >= weaponPool.Length) return null;
        return weaponPool[index];
    }

    // 拥有的武器下标（UI 遍历）
    public List<int> GetOwnedIndices()
    {
        return ownedIndices;
    }

    // 按下标拿武器名
    public string GetWeaponName(int index)
    {
        WeaponData data = GetPoolWeapon(index);
        if (data == null) return "";
        return data.weaponName;
    }
}
