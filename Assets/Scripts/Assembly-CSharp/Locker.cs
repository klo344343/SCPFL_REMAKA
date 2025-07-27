using Mirror;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity;
using UnityEngine;
using UnityEngine.Networking;

public class Locker : NetworkBehaviour
{
	[Serializable]
	public class LockerDrop
	{
		public int[] itemId;
	}

	[SyncVar]
	private Offset localPos;

	public List<Transform> spawnLocations;

	public LockerDrop[] drops;

	[SyncVar(hook = nameof(SetOpen))]
	public bool isOpen;

	public Animator[] anims;

	private bool prevOpen;

	public void SetOpen(bool oldValue, bool newValue)
	{
		isOpen = newValue;
	}

	private void Awake()
	{
		localPos = new Offset
		{
			position = base.transform.localPosition,
			rotation = base.transform.localRotation.eulerAngles
		};
	}

	[ServerCallback]
	public void GetReady()
	{
		if (!NetworkServer.active)
		{
			return;
		}
		isOpen = false;
		bool flag = false;
		while (!flag)
		{
			LockerDrop[] array = drops;
			foreach (LockerDrop lockerDrop in array)
			{
				if (spawnLocations.Count > 0)
				{
					int index = UnityEngine.Random.Range(0, spawnLocations.Count);
					int num = UnityEngine.Random.Range(0, lockerDrop.itemId.Length);
					if (num >= 0)
					{
						PlayerManager.localPlayer.GetComponent<Inventory>().SetPickup(lockerDrop.itemId[num], -4.6566467E+11f, spawnLocations[index].transform.position, spawnLocations[index].transform.rotation, 0, 0, 0);
						flag = true;
						spawnLocations.RemoveAt(index);
					}
				}
			}
		}
	}

	[ServerCallback]
	public void Open()
	{
		if (NetworkServer.active)
		{
			isOpen = true;
		}
	}

	public void Update()
	{
		if (prevOpen != isOpen)
		{
			prevOpen = isOpen;
			Animator[] array = anims;
			foreach (Animator animator in array)
			{
				animator.SetBool("isopen", isOpen);
			}
			GetComponent<AudioSource>().Play();
		}
		base.transform.SetLocalPositionAndRotation(localPos.position, Quaternion.Euler(localPos.rotation));
    }
}
