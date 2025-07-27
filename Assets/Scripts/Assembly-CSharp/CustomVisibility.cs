using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// Кастомная система видимости по радиусу.
/// Назначается в NetworkManager > Interest Management.
/// </summary>
public class CustomVisibility : InterestManagement
{
    public float visRange = 30f;

    public override bool OnCheckObserver(NetworkIdentity identity, NetworkConnectionToClient newObserver)
    {
        if (newObserver.identity == null)
            return false;

        return Vector3.Distance(identity.transform.position, newObserver.identity.transform.position) <= visRange;
    }

    public override void OnRebuildObservers(NetworkIdentity identity, HashSet<NetworkConnectionToClient> newObservers)
    {
        foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
        {
            if (conn.identity == null)
                continue;

            if (Vector3.Distance(identity.transform.position, conn.identity.transform.position) <= visRange)
            {
                newObservers.Add(conn);
            }
        }
    }
}
