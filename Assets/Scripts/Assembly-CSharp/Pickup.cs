using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity;
using UnityEngine;
using UnityEngine.Networking;

public class Pickup : NetworkBehaviour
{
	[Serializable]
	public struct PickupInfo
	{
		public Vector3 position;

		public Quaternion rotation;

		public int itemId;

		public float durability;

		public int ownerPlayerID;

		public int[] weaponMods;
	}

	public float searchTime;

	public static Inventory inv;

	public static List<Pickup> instances;

	private int previousId = -1;

	private GameObject model;

    [SyncVar(hook = nameof(SyncPickup))]
    public PickupInfo info;

    private void SyncPickup(PickupInfo oldValue, PickupInfo newValue)
    {
        info = newValue;
    }

    public void SetupPickup(PickupInfo pickupInfo)
	{
		info = pickupInfo;
		base.transform.SetPositionAndRotation(info.position, info.rotation);
        RefreshModel();
		UpdatePosition();
	}

	[ServerCallback]
	private void UpdatePosition()
	{
		if (NetworkServer.active && (!(info.position == base.transform.position) || !(info.rotation == base.transform.rotation)))
		{
			PickupInfo pickupInfo = info;
			pickupInfo.position = base.transform.position;
			pickupInfo.rotation = base.transform.rotation;
            info = pickupInfo;
		}
	}

	public void CheckForRefresh()
	{
		UpdatePosition();
		if (previousId != info.itemId || !(model != null))
		{
			previousId = info.itemId;
			RefreshModel();
		}
	}

	private void RefreshModel()
	{
		if (model != null)
		{
			UnityEngine.Object.Destroy(model.gameObject);
		}
		model = UnityEngine.Object.Instantiate(inv.availableItems[info.itemId].prefab, base.transform);
		model.transform.localPosition = Vector3.zero;
		searchTime = inv.availableItems[info.itemId].pickingtime;
		base.transform.SetPositionAndRotation(info.position, info.rotation);
    }

	public void Delete()
	{
		NetworkServer.Destroy(base.gameObject);
	}

	private IEnumerator Start()
	{
		Inventory.collectionModified = true;
		if (!NetworkServer.active)
		{
			GetComponent<Rigidbody>().isKinematic = true;
		}
		yield return new WaitForEndOfFrame();
		if (instances == null)
		{
			instances = new List<Pickup>();
		}
		instances.Add(this);
	}

	private void OnDestroy()
	{
		if (instances != null)
		{
			instances.Remove(this);
			Inventory.collectionModified = true;
		}
	}

	public void RefreshDurability(bool allowAmmoRenew = false, bool setupAttachments = false)
	{
		if (!inv.availableItems[info.itemId].noEquipable || allowAmmoRenew)
		{
			info.durability = inv.availableItems[info.itemId].durability;
		}
		if (!setupAttachments)
		{
			return;
		}
		WeaponManager.Weapon[] weapons = inv.GetComponent<WeaponManager>().weapons;
		WeaponManager.Weapon[] array = weapons;
		foreach (WeaponManager.Weapon weapon in array)
		{
			if (weapon.inventoryID == info.itemId)
			{
				try
				{
					info.weaponMods = new int[3];
					info.weaponMods[0] = Mathf.Max(0, UnityEngine.Random.Range(-weapon.mod_sights.Length / 2, weapon.mod_sights.Length));
					info.weaponMods[1] = Mathf.Max(0, UnityEngine.Random.Range(-weapon.mod_barrels.Length / 2, weapon.mod_barrels.Length));
					info.weaponMods[2] = Mathf.Max(0, UnityEngine.Random.Range(-weapon.mod_others.Length / 2, weapon.mod_others.Length));
				}
				catch (Exception ex)
				{
					Debug.Log(ex.StackTrace);
				}
			}
		}
	}
}
