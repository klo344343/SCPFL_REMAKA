using Dissonance.Integrations.UNet_HLAPI; // ���� Dissonance HLAPI ��� ��� ������������
using Mirror;
using RemoteAdmin; // ��������, ������������ ��� ������� ��� ������, �� UnityEngine.Networking.NetworkClient - ��������
using UnityEngine;
using UnityEngine.UI;
using System; // ��� Math.Abs ��� Mathf.Approximately, ���� ��� �����

// ���������, ��� ���� ������ ��������� �� ������� ������� � NetworkIdentity
public class Scp049PlayerScript : NetworkBehaviour
{
    [Header("Player Properties")]
    public GameObject PlayerCameraGameObject;

    public bool IsScp049;

    public bool IsSameClassAsScp;

    public GameObject ScpInstance;

    [Header("Infection")]
    // ������� �������� ��������� ������ (����������� �� ��������)
    public float CurrentInfectionProgress;

    [Header("Attack & Recall")]
    // ������������ ��������� ��� ������� �����
    public float AttackDistance = 2.4f;

    // ������������ ��������� ��� ������ ���������� (Recall)
    public float RecallDistance = 3.5f;

    // �������� ���������� �����
    public float RecallProgress;

    // ���������� ���������� (���������������) �������
    public int CuredPlayersCount;

    // ====================================================================================
    // --- ��������� ���� (���������� ���������) ---
    // ====================================================================================

    // ������ (�����), ������� � ������ ������ ������������� (�� ������� �������)
    private GameObject _recallingPlayerObjectClient;

    // Ragdoll ������, ������� � ������ ������ ������������� (�� ������� �������)
    private Ragdoll _recallingRagdollClient;

    // ������ �� ���������� SCP (���������������� UI ��������)
    private ScpInterfaces _scpInterfaces;

    // UI ������� ��� ����������� ��������� ����������
    private Image _loadingCircleUI;

    // ������ �� ��������� FirstPersonController
    private FirstPersonController _firstPersonController;

    // ====================================================================================
    // --- ��������� ������ (������������ ������) ---
    // ====================================================================================

    [Header("Boosts")]
    // ������, ������������ ����� ���������� � ����������� �� �������� SCP-049
    public AnimationCurve BoostRecallTimeCurve;

    // ������, ������������ ����� ��������� � ����������� �� �������� SCP-049
    public AnimationCurve BoostInfectTimeCurve;

    // ====================================================================================
    // --- ��������� ��������� ��� ���������� (PRIVATE SYNCVARS �� �����, ��� ��������� ����������) ---
    // ====================================================================================

    // ������ ������, ������� ������������� �� �������
    private GameObject _serverRecallingObject;

    // �������� ���������� �� �������
    private float _serverRecallProgress;

    // ����, �����������, ���� �� ������� ���������� �� �������
    private bool _isServerRecallInProgress;

    // ====================================================================================
    // --- ������ UNITY LIFECYCLE ---
    // ====================================================================================

    private void Start()
    {
        // ����� � ����������� ������ �� ScpInterfaces
        _scpInterfaces = FindObjectOfType<ScpInterfaces>();
        if (_scpInterfaces != null)
        {
            _loadingCircleUI = _scpInterfaces.Scp049_loading;
        }
        else
        {
            Debug.LogWarning("Scp049PlayerScript: ScpInterfaces component not found in scene!");
        }

        // ����������� FirstPersonController ������ ��� ���������� ������
        if (isLocalPlayer)
        {
            _firstPersonController = GetComponent<FirstPersonController>();
        }
    }

    private void Update()
    {
        DeductInfectionProgress(); // ��������� �������� ���������
        HandleInput(); // ������������ ���� ������
        UpdateServerRecallProgress(); // ��������� ��������� �������� ����������
    }

    // ====================================================================================
    // --- ������������� � ���������� ��������� ---
    // ====================================================================================

    // ����� ������������� ������ ������
    public void Init(int classID, Class c)
    {
        IsSameClassAsScp = c.team == Team.SCP;
        IsScp049 = classID == 5;

        // ��������� UI ������ ��� ���������� ������, ���� �� SCP-049
        if (isLocalPlayer && _scpInterfaces != null)
        {
            _scpInterfaces.Scp049_eq.SetActive(IsScp049);
        }
    }

    // ���������� ��������� ��������� �� ��������
    private void DeductInfectionProgress()
    {
        if (CurrentInfectionProgress > 0f)
        {
            CurrentInfectionProgress -= Time.deltaTime;
        }
        else if (CurrentInfectionProgress < 0f)
        {
            CurrentInfectionProgress = 0f;
        }
    }

    // ���������� ��������� ���������� �� ������� (���� �������)
    private void UpdateServerRecallProgress()
    {
        if (_isServerRecallInProgress)
        {
            // ����������� ��������, ��������� ������ �������� ����������
            _serverRecallProgress += Time.deltaTime / BoostRecallTimeCurve.Evaluate(GetComponent<PlayerStats>().GetHealthPercent());

            // ���� �������� ������ ��� �������� 2 ������� (����� ����������)
            if (_serverRecallProgress >= 2f)
            {
                _isServerRecallInProgress = false;
                _serverRecallProgress = 0f;
                _serverRecallingObject = null;
            }
        }
    }

    // ====================================================================================
    // --- ��������� ����� ������ ---
    // ====================================================================================

    // ��������� ����� (�����, ����������)
    private void HandleInput()
    {
        if (!isLocalPlayer)
        {
            return; // ������ ��������� ����� ����� ������������ ����
        }

        if (Input.GetKeyDown(NewInput.GetKey("Shoot")))
        {
            PerformAttack();
        }
        if (Input.GetKeyDown(NewInput.GetKey("Interact")))
        {
            StartSurgery(); // �������� ��� ���������� ����������
        }
        UpdateRecallingState(); // ��������� ��������� �������� ���������� (���������� �����)
    }

    // ������ ����� SCP-049
    private void PerformAttack()
    {
        // ���������, �������� �� ����� SCP-049 � ���� �� ���� � ���� ������
        if (IsScp049 && Physics.Raycast(PlayerCameraGameObject.transform.position, PlayerCameraGameObject.transform.forward, out RaycastHit hitInfo, AttackDistance))
        {
            // �������� �������� ��������� Scp049PlayerScript � ����������� �������
            Scp049PlayerScript targetScp049Script = hitInfo.transform.GetComponent<Scp049PlayerScript>();

            // ���� ��������� ������ � ���� �� �������� ��������� SCP-049
            if (targetScp049Script != null && !targetScp049Script.IsSameClassAsScp)
            {
                // ���������� ������� �� ������ ��� ��������� ������
                CmdInfectPlayer(targetScp049Script.gameObject, GetComponent<HlapiPlayer>().PlayerId);
            }
        }
    }

    // ������ ������ ���������� (Surgery)
    private void StartSurgery()
    {
        // ���������, �������� �� ����� SCP-049 � ���� �� ���� � ���� ������
        if (!IsScp049 || !Physics.Raycast(PlayerCameraGameObject.transform.position, PlayerCameraGameObject.transform.forward, out RaycastHit hitInfo, RecallDistance))
        {
            return;
        }

        // �������� �������� ��������� Ragdoll � ����������� ������� ��� ��� ��������
        Ragdoll hitRagdoll = hitInfo.transform.GetComponentInParent<Ragdoll>();

        // ���� ��� �� Ragdoll ��� ���������� �� ��������� ��� ����� Ragdoll
        if (hitRagdoll == null || !hitRagdoll.allowRecall)
        {
            return;
        }

        // ���� ���������������� ������ (��������� Ragdoll) ����� ���� �������
        GameObject[] allPlayers = PlayerManager.singleton.players;
        foreach (GameObject player in allPlayers)
        {
            HlapiPlayer hlapiPlayer = player.GetComponent<HlapiPlayer>();
            Scp049PlayerScript targetScp049Script = player.GetComponent<Scp049PlayerScript>();

            // ���� ��� �������� Ragdoll, � �� �������, � Ragdoll ��������� ����������
            if (hlapiPlayer != null && hlapiPlayer.PlayerId == hitRagdoll.owner.ownerHLAPI_id &&
                targetScp049Script != null && targetScp049Script.CurrentInfectionProgress > 0f && hitRagdoll.allowRecall)
            {
                _recallingPlayerObjectClient = player;
                _recallingRagdollClient = hitRagdoll;
                // ���������� ������� �� ������ ��� ������ ����������
                CmdStartInfecting(_recallingPlayerObjectClient, _recallingRagdollClient.gameObject);
                return; // ����� ���������� ����, �������
            }
        }
    }

    // ���������� ��������� �������� ���������� (���������� �����)
    private void UpdateRecallingState()
    {
        // ���� SCP-049 � ������ "Interact" ������, � ���� ������ ��� ����������
        if (IsScp049 && Input.GetKey(NewInput.GetKey("Interact")) && _recallingPlayerObjectClient != null)
        {
            if (_firstPersonController != null)
            {
                _firstPersonController.lookingAtMe = true; // ���������� ��������� (���� �����)
            }

            // ����������� �������� ����������
            RecallProgress += Time.deltaTime / BoostRecallTimeCurve.Evaluate(GetComponent<PlayerStats>().GetHealthPercent());

            // ���� �������� ������ 100%
            if (RecallProgress >= 1f)
            {
                CuredPlayersCount++; // ����������� ������� ���������������
                if (CuredPlayersCount > 9)
                {
                    AchievementManager.Achieve("turnthemall"); // ����������
                }

                // ���������� ������� �� ������ ��� ���������� ����������
                CmdRecallPlayer(_recallingPlayerObjectClient, _recallingRagdollClient.gameObject);
                ResetRecallStateClient(); // ���������� ���������� ���������
            }
        }
        else // ���� ������ �������� ��� ��� ������� ��� ����������
        {
            if (_recallingPlayerObjectClient != null)
            {
                CmdAbortInfecting(); // ���������� ������� �� ������ ��� ������
                ResetRecallStateClient(); // ���������� ���������� ���������
            }
            if (IsScp049 && _firstPersonController != null)
            {
                _firstPersonController.lookingAtMe = false; // ���������� ���������� ���������
            }
        }

        // ��������� UI ��������� ����������
        if (_loadingCircleUI != null)
        {
            _loadingCircleUI.fillAmount = RecallProgress;
        }
    }

    // ����� ���������� ���������� ��������� ����������
    private void ResetRecallStateClient()
    {
        _recallingPlayerObjectClient = null;
        _recallingRagdollClient = null;
        RecallProgress = 0f;
    }

    // ������� ���� � ���������� ������� �� ������ ��� ���������
    private void InfectPlayer(GameObject target, string id)
    {
        CmdInfectPlayer(target, id);
        Hitmarker.Hit(); // ���������� ������ ��������� �� �������
    }

    // ====================================================================================
    // --- COMMANDS (���������� �� �������) ---
    // ====================================================================================

    // �������: ������ ������ ������ ������ ������� ����������
    [Command(channel = 2)] // ���������� ����� 2 (��������������, ��� �� Reliable)
    public void CmdStartInfecting(GameObject targetPlayer, GameObject targetRagdoll)
    {
        if (targetPlayer == null || targetRagdoll == null)
        {
            Debug.LogWarning("CmdStartInfecting: Target player or ragdoll is null.");
            return;
        }

        Ragdoll ragdollComponent = targetRagdoll.GetComponent<Ragdoll>();
        QueryProcessor targetQueryProcessor = targetPlayer.GetComponent<QueryProcessor>();
        PlyMovementSync playerMovementSync = GetComponent<PlyMovementSync>(); // �������� ������ � ������� ������� SCP-049

        // ��������� �������� ��� ��������� �������
        if (ragdollComponent == null || !ragdollComponent.allowRecall ||
            targetQueryProcessor == null || ragdollComponent.owner.PlayerId != targetQueryProcessor.PlayerId ||
            !IsScp049 ||
            playerMovementSync == null || Vector3.Distance(targetRagdoll.transform.position, playerMovementSync.CurrentPosition) >= AttackDistance * 1.3f)
        {
            Debug.LogWarning("CmdStartInfecting: Validation failed for starting recall.");
            return;
        }

        // ��������� ������� ���������� �� �������
        _serverRecallingObject = targetPlayer;
        _serverRecallProgress = 0f;
        _isServerRecallInProgress = true;
    }

    // �������: ������ ������ ������ �������� ������� ����������
    [Command(channel = 2)]
    public void CmdAbortInfecting()
    {
        // ���������� ��������� ��������� ����������
        _isServerRecallInProgress = false;
        _serverRecallingObject = null;
        _serverRecallProgress = 0f;
    }

    // �������: ������ ������ ������ �������� ������
    [Command(channel = 2)]
    private void CmdInfectPlayer(GameObject targetPlayer, string instigatorId) // ��� instigatorId ����� ����������
    {
        PlyMovementSync playerMovementSync = GetComponent<PlyMovementSync>(); // ������� ���������� SCP-049

        // ��������� �������� ��� ��������� �����
        if (!IsScp049 || targetPlayer == null || playerMovementSync == null ||
            Vector3.Distance(targetPlayer.transform.position, playerMovementSync.CurrentPosition) >= AttackDistance * 1.3f)
        {
            Debug.LogWarning("CmdInfectPlayer: Validation failed for infecting player.");
            return;
        }

        PlayerStats targetStats = targetPlayer.GetComponent<PlayerStats>();
        NicknameSync attackerNickname = GetComponent<NicknameSync>();
        CharacterClassManager attackerClassManager = GetComponent<CharacterClassManager>();

        if (targetStats != null && attackerNickname != null && attackerClassManager != null)
        {
            // ������� ���� ����
            targetStats.HurtPlayer(new PlayerStats.HitInfo(
                4949f,
                $"{attackerNickname.myNick} ({attackerClassManager.SteamId})",
                DamageTypes.Scp049,
                GetComponent<QueryProcessor>().PlayerId), // ID ���������� SCP-049
                targetPlayer);

            // ���������� Rpc �� ���� �������� ��� ���������� ������� ��������� ����
            RpcInfectPlayer(targetPlayer, BoostInfectTimeCurve.Evaluate(GetComponent<PlayerStats>().GetHealthPercent()));
        }
    }

    // �������: ������ ������ ������ ��������� ����������
    [Command(channel = 2)]
    private void CmdRecallPlayer(GameObject targetPlayer, GameObject targetRagdoll)
    {
        // �������������� ��������� �������� ��� �������������� ���������
        if (!_isServerRecallInProgress || targetPlayer != _serverRecallingObject || _serverRecallProgress < 0.85f) // �������� ������ ���� ���������� �������
        {
            Debug.LogWarning("CmdRecallPlayer: Server recall validation failed or progress too low.");
            return;
        }

        CharacterClassManager targetClassManager = targetPlayer.GetComponent<CharacterClassManager>();
        Ragdoll ragdollComponent = targetRagdoll.GetComponent<Ragdoll>();

        // �������� �� �������: ����� ������, SCP-049 �� ���, ������� ������
        if (ragdollComponent == null || targetClassManager == null ||
            targetClassManager.curClass != 2 || !IsScp049 ||
            ragdollComponent.owner.deathCause.GetDamageType() != DamageTypes.Scp049)
        {
            Debug.LogWarning("CmdRecallPlayer: Final validation failed for recalling player.");
            return;
        }

        RoundSummary.changed_into_zombies++; // ��������� ���������� ������
        targetClassManager.SetClassID(10); // ������������� ����� ����� (��������, �����)
        targetPlayer.GetComponent<PlayerStats>().Health = targetClassManager.klasy[10].maxHP; // ��������������� ��������

        // ���������� Ragdoll ������ �� ���� �������� ����� NetworkServer.Destroy
        DestroyPlayerRagdoll(targetRagdoll);

        // ���������� ��������� ��������� ����������
        _isServerRecallInProgress = false;
        _serverRecallingObject = null;
        _serverRecallProgress = 0f;
    }

    // ====================================================================================
    // --- CLIENTRPC (���������� �� ��������) ---
    // ====================================================================================

    // Rpc: ������ ������� ���� �������� �������� �������� ��������� ����
    [ClientRpc(channel = 2)]
    private void RpcInfectPlayer(GameObject targetPlayer, float infectionTime)
    {
        Scp049PlayerScript targetScp049Script = targetPlayer.GetComponent<Scp049PlayerScript>();
        if (targetScp049Script != null)
        {
            targetScp049Script.CurrentInfectionProgress = infectionTime;
        }
    }

    // ====================================================================================
    // --- ��������������� ������ ������� ---
    // ====================================================================================

    // ��������� ����� ��� ����������� Ragdoll �������
    [Server] // ����������, ��� ���� ����� ���������� ������ �� �������
    private void DestroyPlayerRagdoll(GameObject ragdollToDestroy)
    {
        if (!isServer)
        {
            Debug.LogWarning("[Server] DestroyPlayerRagdoll: Called on client!");
            return;
        }

        if (ragdollToDestroy != null && ragdollToDestroy.CompareTag("Ragdoll"))
        {
            NetworkServer.Destroy(ragdollToDestroy);
        }
    }
}