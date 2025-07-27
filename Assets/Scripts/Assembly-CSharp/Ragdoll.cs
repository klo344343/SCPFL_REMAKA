using Mirror;
using System;
using System.Runtime.InteropServices;
using Unity;
using UnityEngine;
using UnityEngine.Networking;

public class Ragdoll : NetworkBehaviour
{
	[Serializable]
	public struct Info
	{
		public string ownerHLAPI_id;

		public string steamClientName;

		public PlayerStats.HitInfo deathCause;

		public int charclass;

		public int PlayerId;

		public Info(string owner, string nick, PlayerStats.HitInfo info, int cc, int playerId)
		{
			ownerHLAPI_id = owner;
			steamClientName = nick;
			charclass = cc;
			deathCause = info;
			PlayerId = playerId;
		}
	}

    [SyncVar(hook = nameof(SetOwner))]
    public Info owner;

    private void SetOwner(Info oldValue, Info newValue)
    {
        owner = newValue;
    }

    [SyncVar(hook = nameof(SetRecall))]
    public bool allowRecall;

    private void SetRecall(bool oldValue, bool newValue)
    {
        allowRecall = newValue;
    }

    private void Start()
	{
		Invoke(nameof(Unfr), 0.1f);
		Invoke(nameof(Refreeze), 7f);
	}

	private void Refreeze()
	{
		CharacterJoint[] componentsInChildren = GetComponentsInChildren<CharacterJoint>();
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			UnityEngine.Object.Destroy(componentsInChildren[i]);
		}
		Rigidbody[] componentsInChildren2 = GetComponentsInChildren<Rigidbody>();
		for (int j = 0; j < componentsInChildren2.Length; j++)
		{
			UnityEngine.Object.Destroy(componentsInChildren2[j]);
		}
	}

	private void Unfr()
	{
		Rigidbody[] componentsInChildren = GetComponentsInChildren<Rigidbody>();
		foreach (Rigidbody rigidbody in componentsInChildren)
		{
			rigidbody.isKinematic = false;
		}
		Collider[] componentsInChildren2 = GetComponentsInChildren<Collider>();
		foreach (Collider collider in componentsInChildren2)
		{
			collider.enabled = true;
		}
	}
}
