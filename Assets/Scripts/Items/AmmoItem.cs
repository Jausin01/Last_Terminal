using UnityEngine;

// One asset per ammo type (Handgun Ammo, Shotgun Shells...).
// Weapons reference the AmmoItem they use and spend it from the Inventory when reloading.
[CreateAssetMenu(menuName = "Last Terminal/Items/Ammo", fileName = "NewAmmo")]
public class AmmoItem : ItemDefinition
{
}
