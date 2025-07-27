using Mirror;
using UnityEngine;

public class LureSubjectContainer : NetworkBehaviour
{
    private Vector3 position = new Vector3(-1471f, 160.5f, -3426.9f);

    private Vector3 rotation = new Vector3(0f, 180f, 0f);

    public float range;

    [SyncVar(hook = nameof(OnAllowContainChanged))]
    public bool allowContain;

    private CharacterClassManager ccm;

    [Space(10f)]
    public Transform hatch;

    public Vector3 closedPos;

    public Vector3 openPosition;

    private GameObject localplayer;

    private void OnAllowContainChanged(bool oldAllowContain, bool newAllowContain)
    {
        allowContain = newAllowContain;

        if (newAllowContain)
        {
            if (hatch.TryGetComponent<AudioSource>(out var audioSource))
            {
                audioSource.Play();
            }
        }
    }

    private void Start()
    {
        transform.localPosition = position;
        base.transform.localRotation = Quaternion.Euler(rotation);
    }

    private void Update()
    {
        CheckForLure();
        hatch.localPosition = Vector3.Slerp(hatch.localPosition, (!allowContain) ? openPosition : closedPos, Time.deltaTime * 3f);
    }

    private void CheckForLure()
    {
        if (ccm == null)
        {
            localplayer = PlayerManager.localPlayer;
            if (localplayer != null)
            {
                ccm = localplayer.GetComponent<CharacterClassManager>();
            }
        }
        else if (ccm.curClass >= 0)
        {
            Team team = ccm.klasy[ccm.curClass].team;
            BoxCollider boxCollider = GetComponent<BoxCollider>();
            if (boxCollider != null)
            {
                boxCollider.enabled = team == Team.SCP || ccm.GodMode;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(base.transform.position, range);
    }
}