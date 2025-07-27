using Mirror;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Networking;

public class ChopperAutostart : NetworkBehaviour
{
	[SyncVar(hook = nameof(SetState))]
	public bool isLanded = true;

    private void SetState(bool oldValue, bool newValue)
    {
        isLanded = newValue;
        RefreshState();
    }
    private void Start()
	{
		RefreshState();
	}

	private void RefreshState()
	{
		GetComponent<Animator>().SetBool("IsLanded", isLanded);
	}
}
