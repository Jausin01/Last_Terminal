using UnityEngine;

// A weapon as an item on the floor / in the inventory. Its id is the name of the weapon prefab in her loadout
// (e.g. Weapon_KitchenKnife, Weapon_Pistol1911): with WeaponHolder.Require Items on, she can only draw the weapons
// whose item she carries ("pick up the knife" first, then "draw the knife").
[CreateAssetMenu(menuName = "Last Terminal/Items/Weapon Item", fileName = "NewWeaponItem")]
public class WeaponItem : ItemDefinition
{
}
