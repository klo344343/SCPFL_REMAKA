using System;
using System.Collections;
using System.Collections.Generic;
using MEC;
using Mirror;
using RemoteAdmin;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityStandardAssets.ImageEffects;

public class Scp106PlayerScript : NetworkBehaviour
{
    [Header("Player Properties")]
    public Transform PlayerCameraGameObject;

    public bool iAm106;

    public bool sameClass;

    [SyncVar]
    private float ultimatePoints;

    public float teleportSpeed;

    public GameObject screamsPrefab;

    [SyncVar(hook = nameof(SetPortalPosition))]
    [Header("Portal")]
    public Vector3 portalPosition;

    private Vector3 previousPortalPosition;

    public GameObject portalPrefab;

    private Offset modelOffset;

    private CharacterClassManager ccm;

    private FirstPersonController fpc;

    private GameObject popup106;

    private TextMeshProUGUI highlightedAbilityText;

    private Text pointsText;

    private string highlightedString;

    public int highlightID;

    private Image cooldownImg;

    private static BlastDoor blastDoor;

    private float attackCooldown;

    public bool goingViaThePortal;

    private bool isCollidingDoorOpen;

    private Door doorCurrentlyIn;

    private bool isHighlightingPoints;

    public LayerMask teleportPlacementMask;

    private void Start()
    {
        if (blastDoor == null)
        {
            blastDoor = UnityEngine.Object.FindObjectOfType<BlastDoor>();
        }
        cooldownImg = GameObject.Find("Cooldown106").GetComponent<Image>();
        ccm = GetComponent<CharacterClassManager>();
        fpc = GetComponent<FirstPersonController>();
        InvokeRepeating(nameof(ExitDoor), 1f, 2f);
        if (isLocalPlayer && NetworkServer.active)
        {
            InvokeRepeating(nameof(HumanPocketLoss), 1f, 1f);
        }
        modelOffset = ccm.klasy[3].model_offset;
        if (isLocalPlayer)
        {
            pointsText = UnityEngine.Object.FindObjectOfType<ScpInterfaces>().Scp106_ability_points;
            pointsText.text = TranslationReader.Get("Legancy_Interfaces", 11);
        }
    }

    private void Update()
    {
        CheckForInventoryInput();
        CheckForShootInput();
        AnimateHighlightedText();
        UpdatePointText();
        DoorCollisionCheck();
    }

    [Server]
    private void HumanPocketLoss()
    {
        if (!NetworkServer.active)
        {
            UnityEngine.Debug.LogWarning("Server function 'System.Void Scp106PlayerScript::HumanPocketLoss()' called on client");
            return;
        }
        GameObject[] players = PlayerManager.singleton.players;
        foreach (GameObject gameObject in players)
        {
            if (gameObject.transform.position.y < -1500f && gameObject.GetComponent<CharacterClassManager>().IsHuman())
            {
                gameObject.GetComponent<PlayerStats>().HurtPlayer(new PlayerStats.HitInfo(1f, "WORLD", DamageTypes.Pocket, GetComponent<QueryProcessor>().PlayerId), gameObject);
            }
        }
    }

    private void CheckForShootInput()
    {
        if (isLocalPlayer && iAm106)
        {
            cooldownImg.fillAmount = Mathf.Clamp01((!(attackCooldown <= 0f)) ? (1f - attackCooldown * 2f) : 0f);
            if (attackCooldown > 0f)
            {
                attackCooldown -= Time.deltaTime;
            }
            if (Input.GetKeyDown(NewInput.GetKey("Shoot")) && attackCooldown <= 0f && Inventory.inventoryCooldown <= 0f)
            {
                attackCooldown = 0.5f;
                Shoot();
            }
        }
    }

    private void Shoot()
    {
        RaycastHit hitInfo;
        if (Physics.Raycast(PlayerCameraGameObject.transform.position, PlayerCameraGameObject.transform.forward, out hitInfo, 1.5f))
        {
            CharacterClassManager component = hitInfo.transform.GetComponent<CharacterClassManager>();
            if (!(component == null) && component.klasy[component.curClass].team != Team.SCP)
            {
                CmdMovePlayer(hitInfo.transform.gameObject, ServerTime.time);
                Hitmarker.Hit(1.5f);
            }
        }
    }

    private void UpdatePointText()
    {
        if (isServer)
        {
            ultimatePoints = ultimatePoints + Time.deltaTime * 6.66f * teleportSpeed;
            ultimatePoints = Mathf.Clamp(ultimatePoints, 0f, 100f);
        }
    }

    private bool BuyAbility(int cost)
    {
        if ((float)cost <= ultimatePoints)
        {
            if (isServer)
            {
                ultimatePoints = ultimatePoints - (float)cost;
            }
            return true;
        }
        return false;
    }

    private void AnimateHighlightedText()
    {
        if (highlightedAbilityText == null)
        {
            highlightedAbilityText = UnityEngine.Object.FindObjectOfType<ScpInterfaces>().Scp106_ability_highlight;
            return;
        }
        highlightedString = string.Empty;
        switch (highlightID)
        {
            case 1:
                highlightedString = TranslationReader.Get("Legancy_Interfaces", 12);
                break;
            case 2:
                highlightedString = TranslationReader.Get("Legancy_Interfaces", 13);
                break;
        }
        if (highlightedString != highlightedAbilityText.text)
        {
            if (highlightedAbilityText.canvasRenderer.GetAlpha() > 0f)
            {
                highlightedAbilityText.canvasRenderer.SetAlpha(highlightedAbilityText.canvasRenderer.GetAlpha() - Time.deltaTime * 4f);
            }
            else
            {
                highlightedAbilityText.text = highlightedString;
            }
        }
        else if (highlightedAbilityText.canvasRenderer.GetAlpha() < 1f && highlightedString != string.Empty)
        {
            highlightedAbilityText.canvasRenderer.SetAlpha(highlightedAbilityText.canvasRenderer.GetAlpha() + Time.deltaTime * 4f);
        }
    }

    private void CheckForInventoryInput()
    {
        if (isLocalPlayer)
        {
            if (popup106 == null)
            {
                popup106 = UnityEngine.Object.FindObjectOfType<ScpInterfaces>().Scp106_eq;
                return;
            }
            bool flag = iAm106 & Input.GetKey(NewInput.GetKey("Inventory"));
            CursorManager.singleton.scp106 = flag;
            popup106.SetActive(flag);
            fpc.m_MouseLook.scp106_eq = flag;
        }
    }

    public void Init(int classID, Class c)
    {
        iAm106 = classID == 3;
        sameClass = c.team == Team.SCP;
    }

    public void SetDoors()
    {
        if (!isLocalPlayer)
        {
            return;
        }
        Door[] array = UnityEngine.Object.FindObjectsOfType<Door>();
        Door[] array2 = array;
        foreach (Door door in array2)
        {
            if (!(door.permissionLevel != "UNACCESSIBLE") || door.Locked)
            {
                continue;
            }
            Collider[] componentsInChildren = door.GetComponentsInChildren<Collider>();
            foreach (Collider collider in componentsInChildren)
            {
                if (!collider.CompareTag("DoorButton"))
                {
                    try
                    {
                        collider.isTrigger = iAm106;
                    }
                    catch
                    {
                    }
                }
            }
        }
    }

    [Server]
    public void Contain(CharacterClassManager ccm)
    {
        if (!NetworkServer.active)
        {
            UnityEngine.Debug.LogWarning("Server function 'System.Void Scp106PlayerScript::Contain(CharacterClassManager)' called on client");
            return;
        }
        ultimatePoints = 0f;
        Timing.RunCoroutine(_ContainAnimation(ccm), Segment.Update);
    }

    public void DeletePortal()
    {
        if (portalPosition.y < 900f)
        {
            portalPrefab = null;
            portalPosition = Vector3.zero;
        }
    }

    public void UseTeleport()
    {
        if (GetComponent<FallDamage>().isGrounded)
        {
            if (portalPrefab != null && BuyAbility(100) && portalPosition != Vector3.zero)
            {
                CmdUsePortal();
            }
            else
            {
                Timing.RunCoroutine(_HighlightPointsText(), Segment.FixedUpdate);
            }
        }
    }

    private void SetPortalPosition(Vector3 oldPos, Vector3 newPos)
    {
        portalPosition = newPos;
        Timing.RunCoroutine(_DoPortalSetupAnimation(), Segment.Update);
    }

    public void CreatePortalInCurrentPosition()
    {
        if (!GetComponent<FallDamage>().isGrounded)
        {
            return;
        }
        if (BuyAbility(100))
        {
            if (isLocalPlayer)
            {
                CmdMakePortal();
            }
        }
        else
        {
            Timing.RunCoroutine(_HighlightPointsText(), Segment.FixedUpdate);
        }
    }

    [Server]
    private IEnumerator<float> _ContainAnimation(CharacterClassManager ccm)
    {
        if (!NetworkServer.active)
        {
            UnityEngine.Debug.LogWarning("Server function 'System.Collections.Generic.IEnumerator`1<System.Single> Scp106PlayerScript::_ContainAnimation(CharacterClassManager)' called on client");
            yield break; // Use yield break for server-side coroutines if not active
        }
        RpcContainAnimation();
        yield return Timing.WaitForSeconds(18f);

        goingViaThePortal = true;
        yield return Timing.WaitForSeconds(3.5f);

        Kill(ccm);
        goingViaThePortal = false;
    }

    private IEnumerator<float> _ClientContainAnimation()
    {
        for (int i = 0; i < 900; i++)
        {
            yield return 0f;
        }
        if (isLocalPlayer)
        {
            goingViaThePortal = true;
            VignetteAndChromaticAberration vaca = GetComponentInChildren<VignetteAndChromaticAberration>();
            Recoil recoil = GetComponentInChildren<Recoil>();
            fpc.noclip = true;
            for (float i2 = 1f; i2 <= 175f; i2 += 1f)
            {
                recoil.positionOffset = -1.6f * (vaca.intensity = i2 / 175f);
                yield return 0f;
            }
            yield return Timing.WaitForSeconds(2f);
            fpc.noclip = false;
            goingViaThePortal = false;
            yield return Timing.WaitForSeconds(5f);
            vaca.intensity = 0.036f;
            recoil.positionOffset = 0f;
        }
        else
        {
            GetComponent<AnimationController>().animator.SetTrigger("Teleporting");
        }
    }

    [ClientRpc]
    private void RpcContainAnimation()
    {
        Timing.RunCoroutine(_ClientContainAnimation(), Segment.FixedUpdate);
    }

    private void LateUpdate()
    {
        Animator animator = GetComponent<AnimationController>().animator;
        if (animator != null && iAm106 && !isLocalPlayer)
        {
            AnimationFloatValue component = ccm.myModel.GetComponent<AnimationFloatValue>();
            Offset offset = modelOffset;
            offset.position -= component.v3_value * component.f_value;
            animator.transform.localPosition = offset.position;
            animator.transform.localRotation = Quaternion.Euler(offset.rotation);
        }
    }

    [Server]
    public void Kill(CharacterClassManager ccm)
    {
        if (!NetworkServer.active)
        {
            UnityEngine.Debug.LogWarning("Server function 'System.Void Scp106PlayerScript::Kill(CharacterClassManager)' called on client");
        }
        else
        {
            GetComponent<PlayerStats>().HurtPlayer(new PlayerStats.HitInfo(999799f, string.Empty, DamageTypes.RagdollLess, ccm.GetComponent<QueryProcessor>().PlayerId), base.gameObject);
        }
    }

    private IEnumerator<float> _HighlightPointsText()
    {
        if (!isHighlightingPoints)
        {
            isHighlightingPoints = true;
            while (pointsText.color.g > 0.05)
            {
                pointsText.color = Color.Lerp(pointsText.color, Color.red, 0.19999999f);
                yield return 0f;
            }
            while (pointsText.color.g < 0.95)
            {
                pointsText.color = Color.Lerp(pointsText.color, Color.white, 0.19999999f);
                yield return 0f;
            }
            isHighlightingPoints = false;
        }
    }

    private IEnumerator<float> _DoPortalSetupAnimation()
    {
        while (portalPrefab == null)
        {
            portalPrefab = GameObject.Find("SCP106_PORTAL");
            yield return 0f;
        }
        Animator portalAnim = portalPrefab.GetComponent<Animator>();
        portalAnim.SetBool("activated", false);
        yield return Timing.WaitForSeconds(1f);
        portalPrefab.transform.position = portalPosition;
        portalAnim.SetBool("activated", true);
    }

    [Server]
    private IEnumerator<float> _DoTeleportAnimation()
    {
        if (!NetworkServer.active)
        {
            UnityEngine.Debug.LogWarning("Server function 'System.Collections.Generic.IEnumerator`1<System.Single> Scp106PlayerScript::_DoTeleportAnimation()' called on client");
            yield break;
        }
        if (portalPrefab != null && !goingViaThePortal)
        {
            RpcTeleportAnimation();
            goingViaThePortal = true;
            PlyMovementSync pms = GetComponent<PlyMovementSync>();
            yield return Timing.WaitForSeconds(3.5f);
            pms.SetPosition(portalPrefab.transform.position + Vector3.up * 1.5f);
            yield return Timing.WaitForSeconds(3.5f);
            if (AlphaWarheadController.host.detonated && transform.position.y < 800f)
            {
                GetComponent<PlayerStats>().HurtPlayer(new PlayerStats.HitInfo(9000f, "WORLD", DamageTypes.Nuke, 0), gameObject);
            }
            pms.SetAllowInput(true);
            goingViaThePortal = false;
        }
    }

    [ClientRpc]
    public void RpcTeleportAnimation()
    {
        Timing.RunCoroutine(_ClientTeleportAnimation(), Segment.FixedUpdate);
    }

    private IEnumerator<float> _ClientTeleportAnimation()
    {
        if (!(portalPrefab != null))
        {
            yield break;
        }
        if (isLocalPlayer)
        {
            goingViaThePortal = true;
            VignetteAndChromaticAberration vaca = GetComponentInChildren<VignetteAndChromaticAberration>();
            Recoil recoil = GetComponentInChildren<Recoil>();
            fpc.noclip = true;
            for (float i = 1f; i <= 175f; i += 1f)
            {
                recoil.positionOffset = -1.6f * (vaca.intensity = i / 175f);
                yield return 0f;
            }
            for (float i2 = 1f; i2 <= 25f; i2 += 1f)
            {
                yield return 0f;
            }
            for (float i3 = 1f; i3 <= 150f; i3 += 1f)
            {
                recoil.positionOffset = -1.6f * (vaca.intensity = 1f - i3 / 150f);
                yield return 0f;
            }
            vaca.intensity = 0.036f;
            recoil.positionOffset = 0f;
            fpc.noclip = false;
            goingViaThePortal = false;
        }
        else
        {
            GetComponent<AnimationController>().animator.SetTrigger("Teleporting");
        }
    }

    [Command(channel = 4)]
    private void CmdMakePortal()
    {
        if (GetComponent<FallDamage>().isGrounded)
        {
            UnityEngine.Debug.DrawRay(transform.position, -transform.up, Color.red, 10f);
            RaycastHit hitInfo;
            if (iAm106 && !goingViaThePortal && Physics.Raycast(new Ray(transform.position, -transform.up), out hitInfo, 10f, teleportPlacementMask))
            {
                SetPortalPosition(Vector3.zero, hitInfo.point - Vector3.up); // Call hook with dummy old value
            }
        }
    }

    [Command(channel = 4)]
    public void CmdUsePortal()
    {
        if (GetComponent<FallDamage>().isGrounded && iAm106 && portalPosition != Vector3.zero && !goingViaThePortal)
        {
            Timing.RunCoroutine(_DoTeleportAnimation(), Segment.Update);
        }
    }

    [Command(channel = 2)]
    private void CmdMovePlayer(GameObject ply, int t)
    {
        if (!ServerTime.CheckSynchronization(t) || !iAm106 || !(Vector3.Distance(GetComponent<PlyMovementSync>().CurrentPosition, ply.transform.position) < 3f) || !ply.GetComponent<CharacterClassManager>().IsHuman())
        {
            return;
        }
        CharacterClassManager component = ply.GetComponent<CharacterClassManager>();
        if (!component.GodMode && component.klasy[component.curClass].team != Team.SCP)
        {
            GetComponent<CharacterClassManager>().RpcPlaceBlood(ply.transform.position, 1, 2f);
            if (blastDoor.IsClosed)
            {
                GetComponent<CharacterClassManager>().RpcPlaceBlood(ply.transform.position, 1, 2f);
                GetComponent<PlayerStats>().HurtPlayer(new PlayerStats.HitInfo(500f, GetComponent<NicknameSync>().myNick + " (" + GetComponent<CharacterClassManager>().SteamId + ")", DamageTypes.Scp106, GetComponent<QueryProcessor>().PlayerId), ply);
            }
            else
            {
                GetComponent<PlayerStats>().HurtPlayer(new PlayerStats.HitInfo(40f, GetComponent<NicknameSync>().myNick + " (" + GetComponent<CharacterClassManager>().SteamId + ")", DamageTypes.Scp106, GetComponent<QueryProcessor>().PlayerId), ply);
                ply.GetComponent<PlyMovementSync>().SetPosition(Vector3.down * 1997f);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isLocalPlayer || ccm.curClass != 3)
        {
            return;
        }
        Door componentInParent = other.GetComponentInParent<Door>();
        if (!(componentInParent == null))
        {
            doorCurrentlyIn = componentInParent;
            isCollidingDoorOpen = false;
            fpc.m_WalkSpeed = 1f;
            fpc.m_RunSpeed = 1f;
            if (componentInParent.IsOpen && componentInParent.curCooldown <= 0f)
            {
                fpc.m_WalkSpeed = ccm.klasy[ccm.curClass].walkSpeed;
                fpc.m_RunSpeed = ccm.klasy[ccm.curClass].runSpeed;
                isCollidingDoorOpen = true;
            }
        }
    }

    private void ExitDoor()
    {
        if (isLocalPlayer && ccm.curClass == 3)
        {
            fpc.m_WalkSpeed = ccm.klasy[ccm.curClass].walkSpeed;
            fpc.m_RunSpeed = ccm.klasy[ccm.curClass].runSpeed;
            doorCurrentlyIn = null;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        ExitDoor();
    }

    private void DoorCollisionCheck()
    {
        if (doorCurrentlyIn != null && doorCurrentlyIn.Destroyed)
        {
            ExitDoor();
        }
        else if (!isCollidingDoorOpen && doorCurrentlyIn != null && doorCurrentlyIn.IsOpen && doorCurrentlyIn.curCooldown <= 0f && !isCollidingDoorOpen)
        {
            fpc.m_WalkSpeed = ccm.klasy[ccm.curClass].walkSpeed;
            fpc.m_RunSpeed = ccm.klasy[ccm.curClass].runSpeed;
            isCollidingDoorOpen = true;
        }
        else if (isCollidingDoorOpen && doorCurrentlyIn != null && !doorCurrentlyIn.IsOpen)
        {
            isCollidingDoorOpen = false;
            fpc.m_WalkSpeed = 1f;
            fpc.m_RunSpeed = 1f;
        }
    }
}