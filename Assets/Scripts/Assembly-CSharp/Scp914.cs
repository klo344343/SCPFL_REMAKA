using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using MEC;
using Mirror;
using RemoteAdmin;
using UnityEngine;

public class Scp914 : NetworkBehaviour
{
    [Serializable]
    public class Recipe
    {
        [Serializable]
        public class Output
        {
            public List<int> outputs = new List<int>();
        }

        public List<Output> outputs = new List<Output>();
    }

    public static Scp914 singleton;

    public Texture burntIcon;

    public AudioSource soundSource;

    public Transform doors;

    public Transform knob;

    public Transform intake_obj;

    public Transform output_obj;

    public float colliderSize;

    public Recipe[] recipes;

    [SyncVar(hook = nameof(SetStatus))]
    public int knobStatus;

    private int prevStatus = -1;

    private float cooldown;

    public bool working;

    private void Awake()
    {
        singleton = this;
    }

    private void SetStatus(int oldStatus, int newStatus)
    {
        knobStatus = newStatus;
    }

    public void ChangeKnobStatus()
    {
        if (!working && cooldown < 0f)
        {
            cooldown = 0.2f;
            int newStatus = knobStatus + 1;
            if (newStatus >= 5)
            {
                newStatus = 0;
            }
            CmdChangeKnobStatus(newStatus);
        }
    }

    [Command(requiresAuthority = false)]
    private void CmdChangeKnobStatus(int newStatus)
    {
        knobStatus = newStatus;
    }

    public void StartRefining()
    {
        if (!working)
        {
            CmdStartRefining();
        }
    }

    [Command(requiresAuthority = false)]
    private void CmdStartRefining()
    {
        if (!working)
        {
            working = true;
            Timing.RunCoroutine(_Animation(), Segment.Update);
        }
    }

    private void Update()
    {
        if (knobStatus != prevStatus)
        {
            knob.GetComponent<AudioSource>().Play();
            prevStatus = knobStatus;
        }
        if (cooldown >= 0f)
        {
            cooldown -= Time.deltaTime;
        }
        knob.transform.localRotation = Quaternion.Lerp(knob.transform.localRotation, Quaternion.Euler(Vector3.forward * Mathf.Lerp(-89f, 89f, (float)knobStatus / 4f)), Time.deltaTime * 4f);
    }

    private IEnumerator<float> _Animation()
    {
        soundSource.Play();
        yield return Timing.WaitForSeconds(1f);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 0.85f;
            doors.transform.localPosition = Vector3.right * Mathf.Lerp(1.74f, 0f, t);
            yield return 0f;
        }
        yield return Timing.WaitForSeconds(6.28f);
        UpgradeItems();
        yield return Timing.WaitForSeconds(5.5f);
        while (t > 0f)
        {
            t -= Time.deltaTime * 0.85f;
            SetDoorPos(t);
            yield return 0f;
        }
        yield return Timing.WaitForSeconds(1f);
        working = false;
    }

    [Server]
    private void UpgradeItems()
    {
        Collider[] array = Physics.OverlapBox(intake_obj.position, Vector3.one * colliderSize / 2f);
        foreach (Collider collider in array)
        {
            Pickup component = collider.GetComponent<Pickup>();
            PlayerStats componentInParent = collider.GetComponentInParent<PlayerStats>();
            if (component == null)
            {
                continue;
            }

            GameObject ownerPlayer = null;
            if (component.netIdentity != null && component.netIdentity.connectionToClient != null && component.netIdentity.connectionToClient.identity != null)
            {
                ownerPlayer = component.netIdentity.connectionToClient.identity.gameObject;
            }
            // Fallback for non-networked pickups or if ownerPlayerID is specifically for some custom logic
            if (ownerPlayer == null)
            {
                foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
                {
                    if (conn.identity != null && conn.identity.GetComponent<QueryProcessor>() != null && conn.identity.GetComponent<QueryProcessor>().PlayerId == component.info.ownerPlayerID)
                    {
                        ownerPlayer = conn.identity.gameObject;
                        break;
                    }
                }
            }


            component.transform.position = component.transform.position + (output_obj.position - intake_obj.position) + Vector3.up;

            if (component.info.itemId >= recipes.Length)
            {
                continue;
            }

            int[] array2 = recipes[component.info.itemId].outputs[knobStatus].outputs.ToArray();
            int num = array2[UnityEngine.Random.Range(0, array2.Length)];

            if (num < 0)
            {
                component.Delete();
                if (TutorialManager.status)
                {
                    TargetRpcCallTutorialKeycardBurnt(ownerPlayer?.GetComponent<NetworkIdentity>()?.connectionToClient);
                }
                continue;
            }

            if (num <= 11 && ownerPlayer != null && ownerPlayer.GetComponent<CharacterClassManager>().curClass == 6)
            {
                foreach (NetworkConnectionToClient clientConn in NetworkServer.connections.Values)
                {
                    if (clientConn.identity != null && clientConn.identity.GetComponent<CharacterClassManager>() != null && clientConn.identity.GetComponent<CharacterClassManager>().curClass == 1 && Vector3.Distance(clientConn.identity.transform.position, ownerPlayer.transform.position) < 10f)
                    {
                        PlayerStats playerStats = ownerPlayer.GetComponent<PlayerStats>();
                        if (playerStats != null && ownerPlayer.GetComponent<NetworkIdentity>()?.connectionToClient != null)
                        {
                            playerStats.TargetAchieve(ownerPlayer.GetComponent<NetworkIdentity>().connectionToClient, "friendship");
                        }
                    }
                }
            }

            Pickup.PickupInfo info = component.info;
            info.itemId = num;
            component.info = info;
            component.RefreshDurability();
        }
    }

    private void SetDoorPos(float t)
    {
        doors.transform.localPosition = Vector3.right * Mathf.Lerp(1.74f, 0f, t);
    }

    [TargetRpc]
    private void TargetRpcCallTutorialKeycardBurnt(NetworkConnection target)
    {
        if (target == null) return;
        if (TutorialManager.status)
        {
            UnityEngine.Object.FindObjectOfType<TutorialManager>().Tutorial3_KeycardBurnt();
        }
    }
}