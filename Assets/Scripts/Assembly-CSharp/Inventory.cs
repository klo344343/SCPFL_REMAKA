using System;
using System.Collections.Generic;
using Mirror;
using RemoteAdmin;
using UnityEngine;
using UnityEngine.UI;

public class Inventory : NetworkBehaviour
{
    [Serializable]
    public struct SyncItemInfo
    {
        public int id;
        public float durability;
        public int uniq;
        public int modSight;
        public int modBarrel;
        public int modOther;
    }

    public class SyncListItemInfo : SyncList<SyncItemInfo>
    {
        public void ModifyDuration(int index, float value)
        {
            SyncItemInfo item = this[index];
            item.durability = value;
            this[index] = item;
        }
        public void ModifyAttachments(int index, int s, int b, int o)
        {
            SyncItemInfo item = this[index];
            item.modSight = s;
            item.modBarrel = b;
            item.modOther = o;
            this[index] = item;
        }
    }

    public readonly SyncListItemInfo items = new SyncListItemInfo();
    public Item[] availableItems;
    private AnimationController ac;
    private WeaponManager weaponManager;
    public static float inventoryCooldown;

    [SyncVar(hook = nameof(OnCurItemChanged))]
    public int curItem;

    public GameObject camera;

    [SyncVar(hook = nameof(OnUniqChanged))]
    public int itemUniq;

    public GameObject pickupPrefab;
    private RawImage crosshair;
    private CharacterClassManager ccm;
    private static int uniqid;
    public static bool collectionModified;
    private int prevIt = -10;
    private float crosshairAlpha = 1f;
    public static float targetCrosshairAlpha;
    private bool gotO5;
    private float pickupanimation;

    private void Awake()
    {
        for (int i = 0; i < availableItems.Length; i++)
        {
            availableItems[i].id = i;
        }
    }

    private void OnCurItemChanged(int oldValue, int newValue)
    {
        if (!GetComponent<MicroHID_GFX>().onFire)
        {
            curItem = newValue;
        }
    }

    private void OnUniqChanged(int oldValue, int newValue)
    {
        itemUniq = newValue;
    }

    [Command]
    public void CmdSetUnic(int i)
    {
        itemUniq = i;
    }

    public void SetUniq(int i)
    {
        if (isServer)
        {
            itemUniq = i;
        }
        else
        {
            CmdSetUnic(i);
        }
    }

    public void SetCurItem(int ci)
    {
        if (!GetComponent<MicroHID_GFX>().onFire)
        {
            if (isServer)
            {
                curItem = ci;
            }
            else
            {
                CmdSetCurItem(ci);
            }
        }
    }

    [Command]
    private void CmdSetCurItem(int ci)
    {
        curItem = ci;
    }

    public SyncItemInfo GetItemInHand()
    {
        foreach (SyncItemInfo item in items)
        {
            if (item.uniq == itemUniq)
            {
                return item;
            }
        }
        return default;
    }

    private void Start()
    {
        weaponManager = GetComponent<WeaponManager>();
        ccm = GetComponent<CharacterClassManager>();
        crosshair = GameObject.Find("CrosshairImage").GetComponent<RawImage>();
        ac = GetComponent<AnimationController>();

        if (isLocalPlayer)
        {
            Pickup.inv = this;
            Pickup.instances = new List<Pickup>();
            UnityEngine.Object.FindObjectOfType<InventoryDisplay>().localplayer = this;
        }
    }

    private void RefreshModels()
    {
        for (int i = 0; i < availableItems.Length; i++)
        {
            try
            {
                availableItems[i].firstpersonModel.SetActive(isLocalPlayer && (i == curItem));
            }
            catch
            {
                // Ignore errors
            }
        }
    }

    public void DropItem(int id, int _s, int _b, int _o)
    {
        if (isLocalPlayer)
        {
            if (items[id].id == curItem)
            {
                SetCurItem(-1);
            }
            CmdDropItem(id, items[id].id, _s, _b, _o);
        }
    }

    [Command]
    private void CmdDropItem(int itemInventoryIndex, int itemId, int _s, int _b, int _o)
    {
        if (items[itemInventoryIndex].id == itemId)
        {
            SetPickup(itemId, items[itemInventoryIndex].durability, transform.position, camera.transform.rotation, _s, _b, _o);
            items.RemoveAt(itemInventoryIndex);
        }
    }

    public void SetPickup(int dropedItemID, float dur, Vector3 pos, Quaternion rot, int _s, int _b, int _o)
    {
        if (dropedItemID >= 0)
        {
            GameObject go = Instantiate(pickupPrefab);
            NetworkServer.Spawn(go);

            if (dur == -4.6566467E+11f)
            {
                dur = availableItems[dropedItemID].durability;
            }

            go.GetComponent<Pickup>().SetupPickup(new Pickup.PickupInfo
            {
                position = pos,
                rotation = rot,
                itemId = dropedItemID,
                durability = dur,
                weaponMods = new int[3] { _s, _b, _o },
                ownerPlayerID = GetComponent<QueryProcessor>().PlayerId
            });
        }
    }

    [Command]
    private void CmdSyncItem(int i)
    {
        foreach (SyncItemInfo item in items)
        {
            if (item.id == i)
            {
                curItem = i;
                return;
            }
        }
        curItem = -1;
    }

    private void Update()
    {
        if (isLocalPlayer)
        {
            if (pickupanimation > 0f)
            {
                pickupanimation -= Time.deltaTime;
            }

            if (!gotO5 && curItem == 11)
            {
                gotO5 = true;
                AchievementManager.Achieve("power");
            }

            inventoryCooldown -= Time.deltaTime;
            CmdSyncItem(curItem);

            int num = Mathf.Clamp(curItem, 0, availableItems.Length - 1);
            if (ccm.curClass >= 0 && ccm.klasy[ccm.curClass].forcedCrosshair != -1)
            {
                num = ccm.klasy[ccm.curClass].forcedCrosshair;
            }

            crosshairAlpha = Mathf.Lerp(crosshairAlpha, targetCrosshairAlpha, Time.deltaTime * 5f);
            crosshair.texture = availableItems[num].crosshair;
            crosshair.color = Color.Lerp(Color.clear, availableItems[num].crosshairColor, crosshairAlpha);
        }

        if (prevIt != curItem)
        {
            RefreshModels();
            prevIt = curItem;

            if (isLocalPlayer)
            {
                foreach (WeaponManager.Weapon weapon in weaponManager.weapons)
                {
                    if (weapon.inventoryID == curItem)
                    {
                        if (weapon.useProceduralPickupAnimation)
                        {
                            weaponManager.weaponInventoryGroup.localPosition = Vector3.down * 0.4f;
                        }
                        pickupanimation = 4f;
                    }
                }
            }

            if (isServer)
            {
                RefreshWeapon();
            }
        }
    }

    [Server]
    private void RefreshWeapon()
    {
        int num = 0;
        int networkcurWeapon = -1;

        foreach (WeaponManager.Weapon weapon in weaponManager.weapons)
        {
            if (weapon.inventoryID == curItem)
            {
                networkcurWeapon = num;
            }
            num++;
        }

        weaponManager.curWeapon = networkcurWeapon;
    }

	public void ServerDropAll()
	{
		foreach (SyncItemInfo item in items)
		{
			SetPickup(item.id, item.durability, base.transform.position, camera.transform.rotation, item.modSight, item.modBarrel, item.modOther);
		}
		AmmoBox component = GetComponent<AmmoBox>();
		for (int i = 0; i < 3; i++)
		{
			if (component.GetAmmo(i) != 0)
			{
				SetPickup(component.types[i].inventoryID, component.GetAmmo(i), base.transform.position, camera.transform.rotation, 0, 0, 0);
			}
		}
		items.Clear();
		component.NetworkAmount = "0:0:0";
	}

	public void Clear()
	{
		items.Clear();
		GetComponent<AmmoBox>().NetworkAmount = "0:0:0";
	}

	public int GetItemIndex()
	{
		int num = 0;
		foreach (SyncItemInfo item in items)
		{
			if (itemUniq == item.uniq)
			{
				return num;
			}
			num++;
		}
		return -1;
	}

	public void AddNewItem(int id, float dur = -4.6566467E+11f, int _s = 0, int _b = 0, int _o = 0)
	{
		uniqid++;
		Item item = new(availableItems[id]);
		if (items.Count >= 8 && !item.noEquipable)
		{
			return;
		}
		SyncItemInfo item2 = new()
        {
			id = item.id,
			durability = item.durability,
			uniq = uniqid
		};
		if (dur != -4.6566467E+11f)
		{
			item2.durability = dur;
			item2.modSight = _s;
			item2.modBarrel = _b;
			item2.modOther = _o;
		}
		else
		{
			for (int i = 0; i < weaponManager.weapons.Length; i++)
			{
				if (weaponManager.weapons[i].inventoryID == id)
				{
					item2.modSight = weaponManager.modPreferences[i, 0];
					item2.modBarrel = weaponManager.modPreferences[i, 1];
					item2.modOther = weaponManager.modPreferences[i, 2];
				}
			}
		}
		items.Add(item2);
	}
	public bool WeaponReadyToInstantPickup()
	{
		return pickupanimation <= 0f;
	}
}
