using System;
using System.Collections;
using System.Collections.Generic;
using MEC;
using Mirror;
using RemoteAdmin;
using UnityEngine;

public class Scp096PlayerScript : NetworkBehaviour
{
    public enum RageState
    {
        NotEnraged = 0,
        Panic = 1,
        Enraged = 2,
        Cooldown = 3
    }

    public static Scp096PlayerScript instance;

    public GameObject camera;

    public bool sameClass;

    public bool iAm096;

    public LayerMask layerMask;

    private AnimationController animationController;

    private float cooldown;

    public SoundtrackManager.Track[] tracks;

    public float rageProgress;

    [Space]
    public float ragemultiplier_looking;

    [Space]
    public float ragemultiplier_deduct = 0.08f;

    public float ragemultiplier_coodownduration = 20f;

    public AnimationCurve lookingTolerance;

    private float t;

    private CharacterClassManager ccm;

    private FirstPersonController fpc;

    private float normalSpeed;

    [Space]
    [SyncVar(hook = nameof(SetRage))]
    public RageState enraged;

    private void SetRage(RageState oldRageState, RageState newRageState)
    {
        enraged = newRageState;
    }

    public void IncreaseRage(float amount)
    {
        if (enraged == RageState.NotEnraged)
        {
            rageProgress += amount;
            rageProgress = Mathf.Clamp01(rageProgress);
            if (rageProgress == 1f)
            {
                SetRage(enraged, RageState.Panic);
                Invoke(nameof(StartRage), 5f);
            }
        }
    }

    private void StartRage()
    {
        SetRage(enraged, RageState.Enraged);
    }

    private void Update()
    {
        ExecuteClientsideCode();
        Animator();
    }

    private IEnumerator<float> _UpdateAudios()
    {
        while (this != null)
        {
            for (int i = 0; i < tracks.Length; i++)
            {
                tracks[i].playing = i == (int)enraged && iAm096;
                tracks[i].Update(tracks.Length + 1);
                yield return 0f;
            }
            yield return 0f;
        }
    }

    private void Animator()
    {
        if (!isLocalPlayer && animationController.animator != null && iAm096)
        {
            animationController.animator.SetBool("Rage", enraged == RageState.Enraged || enraged == RageState.Panic);
        }
    }

    private void ExecuteClientsideCode()
    {
        if (isLocalPlayer && iAm096)
        {
            fpc.m_WalkSpeed = (fpc.m_RunSpeed = normalSpeed * ((enraged == RageState.Panic) ? 0f : ((enraged != RageState.Enraged) ? 1f : 2.8f)));
            if (enraged == RageState.Enraged && Input.GetKey(NewInput.GetKey("Shoot")))
            {
                Shoot();
            }
        }
    }

    public void DeductRage()
    {
        if (enraged == RageState.Enraged)
        {
            rageProgress -= Time.fixedDeltaTime * ragemultiplier_deduct;
            rageProgress = Mathf.Clamp01(rageProgress);
            if (rageProgress == 0f)
            {
                cooldown = ragemultiplier_coodownduration;
                SetRage(enraged, RageState.Cooldown);
            }
        }
    }

    public void DeductCooldown()
    {
        if (enraged == RageState.Cooldown)
        {
            cooldown -= 0.02f;
            cooldown = Mathf.Clamp(cooldown, 0f, ragemultiplier_coodownduration);
            if (cooldown == 0f)
            {
                SetRage(enraged, RageState.NotEnraged);
            }
        }
    }

    [ServerCallback]
    private IEnumerator<float> _ExecuteServersideCode_Looking()
    {
        if (!NetworkServer.active)
        {
            yield break;
        }
        while (true)
        {
            if (instance != null && instance.iAm096)
            {
                GameObject[] plys = PlayerManager.singleton.players;
                bool found = false;
                foreach (GameObject item in plys)
                {
                    if (item != null && item.GetComponent<CharacterClassManager>().IsHuman() && !item.GetComponent<FlashEffect>().sync_blind)
                    {
                        Transform otherPlayerCameraTransform = item.GetComponent<Scp096PlayerScript>().camera.transform;
                        float tolerance = lookingTolerance.Evaluate(Vector3.Distance(otherPlayerCameraTransform.position, instance.camera.transform.position));
                        RaycastHit hitInfo;
                        if ((tolerance < 0.75 || Vector3.Dot(otherPlayerCameraTransform.forward, (otherPlayerCameraTransform.position - instance.camera.transform.position).normalized) < -tolerance) && Physics.Raycast(otherPlayerCameraTransform.position, (instance.camera.transform.position - otherPlayerCameraTransform.position).normalized, out hitInfo, 20f, layerMask) && hitInfo.collider.gameObject.layer == 24 && hitInfo.collider.GetComponentInParent<Scp096PlayerScript>() == instance)
                        {
                            found = true;
                        }
                    }
                }
                if (found)
                {
                    instance.IncreaseRage(0.02f * ragemultiplier_looking * (float)plys.Length);
                }
            }
            yield return 0f;
        }
    }

    [ServerCallback]
    private IEnumerator<float> _ExecuteServersideCode_RageHandler()
    {
        if (!NetworkServer.active)
        {
            yield break;
        }
        while (true)
        {
            t = Time.realtimeSinceStartup;
            if (instance != null && instance.iAm096)
            {
                if (instance.enraged == RageState.Enraged)
                {
                    instance.DeductRage();
                }
                if (instance.enraged == RageState.Cooldown)
                {
                    instance.DeductCooldown();
                }
            }
            yield return 0f;
        }
    }

    private void Shoot()
    {
        RaycastHit hitInfo;
        if (Physics.Raycast(camera.transform.position, camera.transform.forward, out hitInfo, 1.5f))
        {
            CharacterClassManager component = hitInfo.transform.GetComponent<CharacterClassManager>();
            if (component != null && component.klasy[component.curClass].team != Team.SCP)
            {
                Hitmarker.Hit();
                CmdHurtPlayer(hitInfo.transform.gameObject);
            }
        }
    }

    [Command(channel = 2)]
    private void CmdHurtPlayer(GameObject target)
    {
        CharacterClassManager component = target.GetComponent<CharacterClassManager>();
        if (ccm.curClass == 9 && Vector3.Distance(GetComponent<PlyMovementSync>().CurrentPosition, target.transform.position) < 3f && enraged == RageState.Enraged && component.klasy[component.curClass].team != Team.SCP)
        {
            GetComponent<CharacterClassManager>().RpcPlaceBlood(target.transform.position, 0, 3.1f);
            GetComponent<PlayerStats>().HurtPlayer(new PlayerStats.HitInfo(99999f, GetComponent<NicknameSync>().myNick + " (" + GetComponent<CharacterClassManager>().SteamId + ")", DamageTypes.Scp096, GetComponent<QueryProcessor>().PlayerId), target);
        }
    }

    public void Init(int classID, Class c)
    {
        sameClass = c.team == Team.SCP;
        iAm096 = classID == 9;
        if (iAm096)
        {
            instance = this;
        }
    }

    private void Start()
    {
        animationController = GetComponent<AnimationController>();
        fpc = GetComponent<FirstPersonController>();
        ccm = GetComponent<CharacterClassManager>();
        normalSpeed = ccm.klasy[9].runSpeed;
        Timing.RunCoroutine(_UpdateAudios(), Segment.FixedUpdate);
        if (isLocalPlayer && isServer)
        {
            Timing.RunCoroutine(_ExecuteServersideCode_Looking(), Segment.FixedUpdate);
            Timing.RunCoroutine(_ExecuteServersideCode_RageHandler(), Segment.FixedUpdate);
        }
    }
}