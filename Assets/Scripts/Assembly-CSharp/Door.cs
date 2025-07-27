// Door.cs
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Assets._Scripts.RemoteAdmin;
using MEC;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

public class Door : NetworkBehaviour, IComparable
{
    public AudioSource soundsource;
    public AudioClip sound_checkpointWarning;
    public AudioClip sound_denied;
    public MovingStatus moving;
    public GameObject destroyedPrefab;
    public Vector3 localPos;
    public Quaternion localRot;
    internal DoorRemoteAdminButton RemoteAdminButton;
    private SECTR_Portal _portal;
    public Animator[] parts;
    public AudioClip[] sound_open;
    public AudioClip[] sound_close;
    private Rigidbody[] _destoryedRb;
    public int doorType;
    public int status = -1;
    public float curCooldown;
    public float cooldown;
    public bool dontOpenOnWarhead;
    public bool blockAfterDetonation;
    public bool lockdown;
    public bool warheadlock;
    public bool commandlock;
    public bool decontlock;
    public bool GrenadesResistant;
    private bool _buffedStatus;
    private bool _wasLocked;
    private bool _prevDestroyed;
    private bool _deniedInProgress;
    public float scp079Lockdown;
    private bool isLockedBy079;
    public string DoorName;
    public string permissionLevel;
    [HideInInspector] public List<GameObject> buttons = new();

    [SyncVar(hook = nameof(OnDestroyedChanged))] private bool _destroyed;
    [SyncVar(hook = nameof(OnIsOpenChanged))] private bool _isOpen;
    [SyncVar(hook = nameof(OnLockedChanged))] private bool _locked;

    public bool Destroyed => _destroyed;
    public bool IsOpen => _isOpen;
    public bool Locked => _locked;

    [Server] public void SetDestroyed(bool value) => _destroyed = value;
    [Server] public void SetOpen(bool value) => _isOpen = value;
    [Server] public void SetLocked(bool value) => _locked = value;

    private void OnDestroyedChanged(bool oldVal, bool newVal) => HandleDestroyed(newVal);
    private void OnIsOpenChanged(bool oldVal, bool newVal) => HandleOpenStateChanged(newVal);
    private void OnLockedChanged(bool oldVal, bool newVal) => HandleLockStateChanged(newVal);

    private void HandleDestroyed(bool isDestroyed)
    {
        if (RemoteAdminButton != null)
            RemoteAdminButton.UpdateColor();
    }

    private void HandleOpenStateChanged(bool open)
    {
        ForceCooldown(cooldown);
        if (RemoteAdminButton != null)
            RemoteAdminButton.UpdateColor();
    }

    private void HandleLockStateChanged(bool locked)
    {
        if (RemoteAdminButton != null)
            RemoteAdminButton.UpdateColor();
    }

    private void Start()
    {
        scp079Lockdown = -3f;
        Timing.RunCoroutine(_Start(), Segment.FixedUpdate);
    }

    public void UpdateLock()
    {
        if (!isServer) return;
        SetLocked(permissionLevel != "UNACCESSIBLE" && (commandlock || lockdown || warheadlock || decontlock || scp079Lockdown > 0f || isLockedBy079));
    }

    public void LockBy079()
    {
        if (!isServer) return;
        isLockedBy079 = true;
        UpdateLock();
    }

    private void LateUpdate()
    {
        if (!isServer) return;

        if (isLockedBy079)
            Update079Lock();

        if (curCooldown >= 0f)
            curCooldown -= Time.deltaTime;

        if (scp079Lockdown >= -3f)
        {
            scp079Lockdown -= Time.deltaTime;
            UpdateLock();
        }

        if (_wasLocked && !_locked && doorType == 3)
        {
            SetOpen(false);
            RpcDoSound();
        }

        _wasLocked = _locked;
    }

    private void Update079Lock()
    {
        string name = transform.parent.name + "/" + transform.name;
        bool found = false;

        foreach (var instance in Scp079PlayerScript.instances)
            if (instance.lockedDoors.Contains(name))
                found = true;

        if (found != isLockedBy079)
        {
            isLockedBy079 = found;
            UpdateLock();
        }
    }

    public int CompareTo(object obj) => string.CompareOrdinal(DoorName, ((Door)obj).DoorName);

    public void SetPortal(SECTR_Portal p) => _portal = p;

    public void SetLocalPos()
    {
        localPos = transform.localPosition;
        localRot = transform.localRotation;
    }

    public void UpdatePos()
    {
        if (localPos != Vector3.zero)
        {
            transform.localPosition = localPos;
            transform.localRotation = localRot;
        }
    }

    public void SetZero() => localPos = Vector3.zero;

    public void SetActiveStatus(int s)
    {
        if (status == s) return;
        status = s;
        foreach (var button in buttons)
        {
            var renderer = button.GetComponent<MeshRenderer>();
            var text = button.GetComponentInChildren<Text>();
            var img = button.GetComponentInChildren<Image>();
            if (renderer != null)
                renderer.material = ButtonStages.types[doorType].stages[s].mat;
            if (text != null)
                text.text = ButtonStages.types[doorType].stages[s].info;
            if (img != null)
            {
                img.color = ButtonStages.types[doorType].stages[s].texture != null ? Color.white : Color.clear;
                img.sprite = ButtonStages.types[doorType].stages[s].texture;
            }
        }
    }

    public void ForceCooldown(float cd)
    {
        curCooldown = cd;
        Timing.RunCoroutine(_UpdatePosition(), Segment.Update);
    }

    [Server]
    public void SetStateWithSound(bool open)
    {
        if (_isOpen != open)
            RpcDoSound(); 

        moving.moving = true; 
        SetOpen(open);
        ForceCooldown(cooldown);

        if (RemoteAdminButton != null)
            RemoteAdminButton.UpdateColor();
    }

    public bool ChangeState(bool force = false)
    {
        if (!isServer || curCooldown >= 0f || moving.moving || _deniedInProgress || (_locked && !force))
            return false;
        moving.moving = true;
        SetOpen(!_isOpen);
        RpcDoSound();
        return true;
    }

    public bool ChangeState079()
    {
        if (!isServer || curCooldown >= 0f || moving.moving || _deniedInProgress || (permissionLevel != "UNACCESSIBLE" && (commandlock || lockdown || warheadlock || decontlock)))
            return false;
        moving.moving = true;
        SetOpen(!_isOpen);
        RpcDoSound();
        return true;
    }

    public void OpenDecontamination()
    {
        if (!isServer || permissionLevel == "UNACCESSIBLE") return;
        decontlock = true;
        if (!_isOpen) RpcDoSound();
        moving.moving = true;
        SetOpen(true);
        UpdateLock();
    }

    public void CloseDecontamination()
    {
        if (!isServer || permissionLevel == "UNACCESSIBLE") return;
        decontlock = true;
        if (_isOpen) RpcDoSound();
        moving.moving = true;
        SetOpen(false);
        UpdateLock();
    }

    public void OpenWarhead(bool force, bool lockDoor)
    {
        if (!isServer || permissionLevel == "UNACCESSIBLE" || (dontOpenOnWarhead && !force)) return;
        if (lockDoor) warheadlock = true;
        if ((!_locked || force) && (force || permissionLevel != "CONT_LVL_3"))
        {
            if (!_isOpen) RpcDoSound();
            moving.moving = true;
            SetOpen(true);
            UpdateLock();
        }
    }

    [ClientRpc(channel = 14)]
    public void RpcDoSound()
    {
        if (_isOpen)
            soundsource.PlayOneShot(sound_open[UnityEngine.Random.Range(0, sound_open.Length)]);
        else
            soundsource.PlayOneShot(sound_close[UnityEngine.Random.Range(0, sound_close.Length)]);
    }

    private IEnumerator<float> _Start()
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
            if (r.CompareTag("DoorButton"))
                buttons.Add(r.gameObject);

        SetActiveStatus(0);
        float time = 0f;
        while (time < 10f)
        {
            time += 0.02f;
            if (_buffedStatus != _isOpen)
            {
                _buffedStatus = _isOpen;
                ForceCooldown(cooldown);
                break;
            }
            yield return 0f;
        }
    }

    public IEnumerator<float> _UpdatePosition()
    {
        foreach (var part in parts)
            part.SetBool("isOpen", _isOpen);
        yield break;
    }
}
