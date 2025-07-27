using AntiFaker;
using MEC; // Assuming MEC (More Effective Coroutines) is still used
using Mirror; // Using Mirror namespace
using System;
using System.Collections.Generic;
using UnityEngine;

// ���������, ��� ���� ������ ��������� �� ������� ������� � NetworkIdentity
public class PlyMovementSync : NetworkBehaviour
{
    // ====================================================================================
    // --- ��������� ���� � �������� ---
    // ====================================================================================

    // ������� �������, ������������������ � ��������
    public Vector3 CurrentPosition;

    // ������� �������� �� ��� Y, ������������������ � ��������
    public float CurrentRotationY;

    // �������� ������ �� ��� X (������������), ������������������ ����� SyncVar
    [SyncVar(hook = nameof(OnRotXChanged))] // ���������� hook ��� ���������� ��������� �����
    public float rotX;

    // ������������ ��� ��������� �������� SyncVar � ������ ��������, ���� ����������
    // � ������ ������, Mirror �������� ��������� rotX ����� SyncVar.
    // ���� ��� ����� ���� �� ��������� �������������� ������ ��� ���������,
    // ����� ���� �� ������������ ��� property � ��������������� ����������.
    public float NetworkRotX
    {
        get => rotX;
        set => rotX = value; // Mirror ��� ������� ��� OnRotXChanged ��� ��������� SyncVar
    }

    // ====================================================================================
    // --- ��������� ���� ---
    // ====================================================================================

    // ��������� ���������� ��� ������������ ������ ��� �������� ���������
    private Vector3 _lastSyncedPos;
    private float _lastSyncedRotY;
    private float _lastSyncedRotX; // ��� �������� ������ X

    // ��������� ���� ��� �������� �������� �������� ������ (������������ ������ ��������)
    private float _localPlayerRotationY;

    // ������ �� ��������� CharacterClassManager
    [HideInInspector]
    public CharacterClassManager characterClassManager;

    // ������ �� ������ ����������� ����������
    private AntiFakeCommands _antiFakeCommands;
    private Scp106PlayerScript _scp106Script;
    private Transform _playerCameraTransform; // ������������ ������ �� transform ������
    private FootstepSync _footstepSync; // ���� ���� ��������� ������������� ������������
    private FallDamage _fallDamage;

    // ����� ���������
    public bool IsGrounded;
    private bool _isInputAllowed;
    private bool _unstuckNeeded;
    private bool _wasUsingPortal;

    // ���������� ��� ������������ ������/�������
    public float FlyTime;
    public float GroundedYPosition; // Y-���������� ��������� ����� �����������

    // ��� ������������/����������� ������ ��������
    private Vector3 _serverTeleportTargetPosition;

    // ====================================================================================
    // --- ������ UNITY LIFECYCLE ---
    // ====================================================================================

    private void Start()
    {
        // ����������� ������ �� ����������
        _playerCameraTransform = GetComponent<Scp049PlayerScript>()?.PlayerCameraGameObject.transform;
        _antiFakeCommands = GetComponent<AntiFakeCommands>();
        characterClassManager = GetComponent<CharacterClassManager>();
        _scp106Script = GetComponent<Scp106PlayerScript>();
        _fallDamage = GetComponent<FallDamage>();

        FlyTime = 0f;
        _serverTeleportTargetPosition = Vector3.zero;
        _isInputAllowed = true;

        // ��������� �������� �������� ����������� ������ �� ������� � � ������ ������
        if (isServer && RoundStart.RoundJustStarted)
        {
            Timing.RunCoroutine(UnstuckCheckRoutine(), Segment.Update);
        }
    }

    private void FixedUpdate()
    {
        // ������ ��������� ����� ���������� ���� ������ �� ������
        if (isLocalPlayer)
        {
            _localPlayerRotationY = transform.rotation.eulerAngles.y;
            TransmitMovementData();
        }
    }

    [ClientCallback]
    private void TransmitMovementData()
    {
        if (isLocalPlayer)
        {
            CmdSyncData(_localPlayerRotationY, transform.position, GetComponent<PlayerInteract>().playerCamera.transform.localRotation.eulerAngles.x);
        }
    }

    // �������, ������������ �������� �� ������
    [Command(channel = 5)] // ���������, ��� ����� 5 �������� ��� Unreliable/UnreliableSequenced � LiteNetLib4MirrorTransport
    private void CmdSyncData(float clientRotY, Vector3 clientPos, float clientRotX)
    {
        // ���������, ���������� �� ������ �����������, ����� �������� ������ ���������
        if (Mathf.Approximately(_lastSyncedRotY, clientRotY) &&
            Mathf.Approximately(_lastSyncedRotX, clientRotX) &&
            Mathf.Approximately(_lastSyncedPos.x, clientPos.x) &&
            Mathf.Approximately(_lastSyncedPos.y, clientPos.y) &&
            Mathf.Approximately(_lastSyncedPos.z, clientPos.z))
        {
            return; // ������ �� ���������� ��� ���������� �������������
        }

        _lastSyncedPos = clientPos;
        _lastSyncedRotY = clientRotY;
        _lastSyncedRotX = clientRotX;

        CurrentRotationY = clientRotY; // ��������� SyncVar (��� ������ ��������� ����)

        // ��������� ������������, �������������� ��������
        if (_serverTeleportTargetPosition != Vector3.zero)
        {
            CurrentPosition = _serverTeleportTargetPosition;
            _antiFakeCommands.SetPosition(_serverTeleportTargetPosition);
            transform.position = _serverTeleportTargetPosition;
            _serverTeleportTargetPosition = Vector3.zero; // ���������� ���� ������������
        }
        // ������� ��������� �������� � ��������� ��������
        else if (_isInputAllowed && _antiFakeCommands.CheckMovement(clientPos))
        {
            // ����������� ������ ��� ������ 2 (���� ��� �����, ������� ������ ��������� � ������������ �������)
            if (characterClassManager.curClass == 2)
            {
                clientPos = new Vector3(0f, 2048f, 0f);
            }

            _fallDamage.CalculateGround(); // ������������� ��������� �����
            IsGrounded = _fallDamage.isCloseToGround; // ��������� ��������� "�� �����"
            CheckGroundForAntiCheat(clientPos); // �������� ������� ��������
            CurrentPosition = clientPos; // ��������� ��������� ������� ������

            if (IsGrounded)
            {
                GroundedYPosition = clientPos.y; // ��������� Y-���������� �����������
            }
        }
        else
        {
            // ���� ������� �������� ��� ���� �� ��������, ���������� ������ �� ��������� ��������� ��������� �������
            TargetSetPosition(connectionToClient, CurrentPosition);
        }

        rotX = clientRotX; // ��������� SyncVar rotX, Mirror ����������� � ������������� � ���������
    }

    // TargetRpc: ���������� ������� ����������� ������� (��������� ����� �������)
    [TargetRpc]
    private void TargetSetPosition(NetworkConnection target, Vector3 pos)
    {
        transform.position = pos;
        CurrentPosition = pos; // ��������� ��������� ������� �������
    }

    // TargetRpc: ���������� ������� ����������� ������� ��� ��������� ��������
    [TargetRpc]
    private void TargetSetRotation(NetworkConnection target, float rotY)
    {
        _localPlayerRotationY = rotY;
        CurrentRotationY = rotY; // ��������� ��������� ���������� � ��������� SyncVar (���� �� ��� ����)
        transform.rotation = Quaternion.Euler(0f, rotY, 0f);

        // ������� �������� �������� � FirstPersonController, ���� �� ����
        try
        {
            FirstPersonController component = GetComponent<FirstPersonController>();
            if (component != null)
            {
                // ��������������, ��� m_MouseLook.SetRotation ��������� �������� �� Y
                // ���� ���, ��������, ����������� ������������
                component.m_MouseLook.SetRotation(rotY);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error setting FirstPersonController rotation: {ex.Message}");
            // �� "�������" ����������, � ����������� ��� ��� �������
        }
    }

    // ��� ��� SyncVar rotX: ���������� �� ���� �������� (������� ���������) ��� ��������� rotX
    private void OnRotXChanged(float oldRotX, float newRotX)
    {
        // ��������� �������� ������ �� ���� �������� (���� ������ �� ��������� �����,
        // �� ��� �������� ������ ������� ������)
        if (_playerCameraTransform != null)
        {
            _playerCameraTransform.localRotation = Quaternion.Euler(newRotX, 0f, 0f);
        }
    }

    // ====================================================================================
    // --- ������ ���������� ��������/�������� ---
    // ====================================================================================

    // ���������� ����� ��� ��������� ��������
    [Client]
    public void ClientSetRotation(float rotY)
    {
        // isClient - ��� ����� ������ ��������, ��� NetworkClient.active ��� �������,
        // ������� ����� ���� ������� ������ �� �������.
        if (!isClient)
        {
            Debug.LogWarning("[Client] function 'ClientSetRotation' called on server");
        }
        else
        {
            _localPlayerRotationY = rotY;
            // Note: ����� �� ���������� ���������� NetworkRotationY ��� ����������,
            // ��� ������ ��������� ��������� ����������.
            // ���� ����� ���������� ���������� �� �������, ��� ������ ���� ������� �����.
        }
    }

    // ��������� ����� ��� ��������� ������� ������ (��������, ��� ������������)
    [Server]
    public void SetPosition(Vector3 newPos)
    {
        if (!isServer)
        {
            Debug.LogWarning("[Server] function 'SetPosition' called on client");
            return;
        }

        _serverTeleportTargetPosition = newPos; // ������������� ������� ������� ��� ������������
        CurrentPosition = newPos; // ��������� ��������� �������
        transform.position = newPos; // ���������� ���������� ��������� ������
        _antiFakeCommands.SetPosition(newPos); // ��������� ������� � ��������

        // ���������� ��������� ������� ��� ������������
        _fallDamage.isGrounded = true;
        _fallDamage.isCloseToGround = true;
        _fallDamage.previousHeight = newPos.y;

        // ���������� TargetRpc, ����� ��������� ��������� ������� � ����� �������
        TargetSetPosition(connectionToClient, newPos);
    }

    // ��������� ����� ��� ��������� �������� ������
    [Server]
    public void SetRotation(float rotY)
    {
        if (!isServer)
        {
            Debug.LogWarning("[Server] function 'SetRotation' called on client");
            return;
        }

        CurrentRotationY = rotY; // ��������� ��������� ��������
        _localPlayerRotationY = rotY; // ��������� ��������� �������� �� �������

        // ���������� TargetRpc, ����� ��������� ��������� ������� � ����� ��������
        TargetSetRotation(connectionToClient, rotY);
    }

    // ��������� ����� ��� ����������/������� ����� ������
    [Server]
    public void SetAllowInput(bool allow)
    {
        if (!isServer)
        {
            Debug.LogWarning("[Server] function 'SetAllowInput' called on client");
        }
        else
        {
            _isInputAllowed = allow;
        }
    }

    // ====================================================================================
    // --- ������� � ��������������� ������ ---
    // ====================================================================================

    // �������� �� ���������� � ������� (�������)
    private void CheckGroundForAntiCheat(Vector3 currentPos)
    {
        // ���� ����� �������� SCP-106 ��� ������ ����������� �������, ��� ��� �� �����, �������� ������� ������
        if (characterClassManager.curClass == 2 || characterClassManager.curClass == -1 || IsGrounded || characterClassManager.curClass == 7)
        {
            FlyTime = 0f;
            _wasUsingPortal = false;
            GroundedYPosition = currentPos.y;
            return;
        }

        FlyTime += Time.deltaTime;

        // ����������� ������ ��� SCP-106 ��� ������������� ��������
        if (_scp106Script.iAm106 && (_scp106Script.goingViaThePortal || _wasUsingPortal))
        {
            _wasUsingPortal = true;
            if (FlyTime < 4.5f) // ����� � ������� ����� �������
            {
                return;
            }
        }

        // ���� �� �� �����, ��������� �������/�����
        if (!IsGrounded)
        {
            // ���� ����� ���� ������� ������
            if (GroundedYPosition < currentPos.y - 3f)
            {
                HandleFlyingViolation("*Killed by anticheat for flying (code: 1.3).");
                return;
            }
            // ��������� ����� ������ ����� Y �� ����� �������
            if (GroundedYPosition > currentPos.y)
            {
                GroundedYPosition = currentPos.y;
            }
        }

        // �������������� �������� �� ���������� ����� ��� ������� (Linecast/OverlapBox)
        Vector3 raycastOrigin = currentPos;
        raycastOrigin.y -= 50f; // ��� ����

        if (!Physics.Linecast(currentPos, raycastOrigin, _antiFakeCommands.mask))
        {
            Vector3 overlapBoxCenter = currentPos;
            overlapBoxCenter.y += 23.8f; // ����� ��� OverlapBox

            // �������� OverlapBox ��� ����������� ����� � ������������ �������
            if (Physics.OverlapBox(overlapBoxCenter, new Vector3(0.5f, 25f, 0.5f), Quaternion.identity, _antiFakeCommands.mask).Length == 0)
            {
                HandleFlyingViolation("*Killed by anticheat for flying (code: 1.2).");
                return;
            }
        }

        // ����� �������� �� ������� ������ ���������� � �������
        if (!(FlyTime < 2.2f)) // ������������ ����������� ����� ������
        {
            HandleFlyingViolation("*Killed by anticheat for flying (code: 1.1).");
        }
    }

    // ��������������� ����� ��� ��������� ��������� ������
    private void HandleFlyingViolation(string message)
    {
        if (!RoundStart.RoundJustStarted)
        {
            characterClassManager.GetComponent<PlayerStats>().HurtPlayer(
                new PlayerStats.HitInfo(2000000f, message, DamageTypes.Flying, 0), gameObject);
        }
        else
        {
            _unstuckNeeded = true;
        }
    }

    // �������� ��� �������� ����������� ������ (����������� ������ �� �������)
    private IEnumerator<float> UnstuckCheckRoutine()
    {
        while (RoundStart.RoundJustStarted)
        {
            if (_unstuckNeeded)
            {
                Unstuck();
                _unstuckNeeded = false;
            }
            yield return Timing.WaitForSeconds(1f);
            CheckGroundForAntiCheat(transform.position); // ��������� ������� �� �������
        }
    }

    // ���������� ����� ��� "�������������" ������ (���������)
    [Server]
    internal void Unstuck()
    {
        if (!isServer)
        {
            Debug.LogWarning("[Server] function 'Unstuck' called on client");
            return;
        }

        Vector3 respawnPoint = NonFacilityCompatibility.currentSceneSettings.constantRespawnPoint;
        if (respawnPoint == Vector3.zero)
        {
            // ���� ��� ������������� ����� �����������, ���������� ���������
            SetPosition(UnityEngine.Object.FindObjectOfType<SpawnpointManager>().GetRandomPosition(characterClassManager.curClass).transform.position);
        }
        else
        {
            SetPosition(respawnPoint); // ���������� ������������� �����
        }
    }
}