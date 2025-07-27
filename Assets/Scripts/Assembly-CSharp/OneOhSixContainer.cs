using Mirror;
using System.Runtime.InteropServices;

public class OneOhSixContainer : NetworkBehaviour
{
    [SyncVar(hook = nameof(SetState))]
    public bool used;

    private void SetState(bool oldValue, bool newValue)
    {
        used = newValue;
    }
}
