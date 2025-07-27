using Mirror;
using UnityEngine;

[RequireComponent(typeof(NetworkIdentity))]
public class BlastDoor : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnIsClosedChanged))]
    private bool isClosed;

    public bool IsClosed
    {
        get => isClosed;
        set
        {
            if (isServer)
            {
                isClosed = value;
            }
        }
    }

    private Animator _animator;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    private void OnIsClosedChanged(bool oldValue, bool newValue)
    {
        if (newValue)
            _animator.SetTrigger("Close");
        else
            _animator.SetTrigger("Open"); 
    }
}
