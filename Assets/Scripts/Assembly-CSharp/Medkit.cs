using Mirror;
using System;
using UnityEngine;
using UnityEngine.Networking;

public class Medkit : NetworkBehaviour
{
	[Serializable]
	public struct MedkitInstance
	{
		public string Label;

		public int InventoryID;

		public int MinimumHealthRegeneration;

		public int MaximumHealthRegeneration;
	}

	public MedkitInstance[] Medkits;

	private Inventory inv;

	private PlayerStats ps;

	private KeyCode fireCode;

	private void Start()
	{
		inv = GetComponent<Inventory>();
		ps = GetComponent<PlayerStats>();
		fireCode = NewInput.GetKey("Shoot");
	}

	private void Update()
	{
		if (!Input.GetKeyDown(fireCode) || Cursor.visible || !(Inventory.inventoryCooldown < 0f) || ps.Health >= ps.maxHP)
		{
			return;
		}
		for (int i = 0; i < Medkits.Length; i++)
		{
			if (Medkits[i].InventoryID == inv.curItem)
			{
				inv.SetCurItem(-1);
				CmdUseMedkit(i);
				break;
			}
		}
	}

	[Command(channel = 2)]
	private void CmdUseMedkit(int id)
	{
		foreach (Inventory.SyncItemInfo item in inv.items)
		{
			if (item.id == Medkits[id].InventoryID)
			{
				ps.Health = Mathf.Clamp(ps.Health + UnityEngine.Random.Range(Medkits[id].MinimumHealthRegeneration, Medkits[id].MaximumHealthRegeneration), 0, ps.ccm.klasy[ps.ccm.curClass].maxHP);
				inv.items.Remove(item);
				break;
			}
		}
	}
}
