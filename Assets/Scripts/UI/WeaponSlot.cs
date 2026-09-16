using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 武器格子：挂在背包里的每个武器格子上（一把武器一格）。
/// 显示武器（图标+名字+伤害），点它装到对应类型的装备格。
/// </summary>
public class WeaponSlot : MonoBehaviour
{
    [Header("图标（武器图）")]
    public Image icon;

    [Header("武器名文本")]
    public TextMeshProUGUI nameText;

    [Header("伤害文本")]
    public TextMeshProUGUI damageText;

    [Header("背景（已装备的武器高亮）")]
    public Image background;

    private InventoryManager inventoryManager;   // 点谁装武器
    private int weaponIndex;                     // 这把武器在武器池的下标

    // 初始化格子：显示哪把武器，点它装到装备格
    public void Setup(InventoryManager manager, WeaponData data, int index)
    {
        inventoryManager = manager;
        weaponIndex = index;

        // 填图标 / 名字 / 伤害
        if (icon != null) icon.sprite = data.Picture;
        if (nameText != null) nameText.text = data.weaponName;
        if (damageText != null) damageText.text = "Demage： " + data.damage;

        // 已装备的武器金色高亮，一眼看出现在装的是哪把
        if (background != null)
        {
            bool equipped = manager.IsEquipped(index);
            if (equipped) background.color = new Color(1f, 0.8f, 0.3f, 1f);   // 金色
            else background.color = new Color(1f, 1f, 1f, 1f);                // 白色
        }

        // 自己绑点击
        Button btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(OnClick);
        }
    }

    // 被点击 → 装到对应装备格
    public void OnClick()
    {
        if (inventoryManager != null)
        {
            inventoryManager.EquipToSlot(weaponIndex);
        }
    }
}
