using System.Collections.Generic;
using MEC;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoundStart : NetworkBehaviour
{
    public GameObject window;
    public GameObject forceButton;
    public TextMeshProUGUI playersNumber;
    public Image loadingbar;

    public static RoundStart singleton;
    public static bool RoundJustStarted;

    [SyncVar(hook = nameof(OnInfoChanged))]
    private string _info = string.Empty;

    public string Info => _info;

    [Server]
    public void SetInfo(string value)
    {
        _info = value;
    }

    private void Awake()
    {
        singleton = this;
        if (NetworkServer.active)
            RoundJustStarted = true;
    }

    private void Start()
    {
        transform.localPosition = Vector3.zero;

        if (NetworkServer.active)
            Timing.RunCoroutine(_AntiNonclass(), Segment.FixedUpdate);
    }

    private void Update()
    {
        if (window != null)
            window.SetActive(!string.IsNullOrEmpty(_info) && _info != "started");

        if (float.TryParse(_info, out float parsed))
        {
            float t = Mathf.Clamp01((parsed - 1f) / 19f);
            if (loadingbar != null)
                loadingbar.fillAmount = Mathf.Lerp(loadingbar.fillAmount, t, Time.deltaTime);
        }

        if (playersNumber != null && PlayerManager.singleton != null && PlayerManager.singleton.players != null)
        {
            playersNumber.text = PlayerManager.singleton.players.Length.ToString();
        }
    }


    private void OnInfoChanged(string oldValue, string newValue)
    {
        if (newValue == "started")
        {
            RoundJustStarted = true;

            if (NetworkServer.active)
                Timing.RunCoroutine(AntiFloorStuck(), Segment.FixedUpdate);
        }
    }

    private IEnumerator<float> _AntiNonclass()
    {
        yield return Timing.WaitUntilTrue(() => _info == "started");

        RoundJustStarted = true;

        if (NetworkServer.active)
            Timing.RunCoroutine(AntiFloorStuck(), Segment.FixedUpdate);

        yield return Timing.WaitForSeconds(10f);

        while (this != null)
        {
            foreach (GameObject player in PlayerManager.singleton.players)
            {
                if (player == null)
                    continue;

                var ccm = player.GetComponent<CharacterClassManager>();
                if (ccm != null && ccm.curClass < 0 && ccm.IsVerified)
                {
                    ccm.SetPlayersClass(2, ccm.gameObject);
                }
            }

            yield return Timing.WaitForSeconds(1f);
        }
    }

    private IEnumerator<float> AntiFloorStuck()
    {
        yield return Timing.WaitForSeconds(5f);
        RoundJustStarted = false;
    }

    public void ShowButton()
    {
        forceButton.SetActive(true);
    }

    public void UseButton()
    {
        forceButton.SetActive(false);

        if (!NetworkServer.active)
            return;

        foreach (GameObject player in PlayerManager.singleton.players)
        {
            var ccm = player.GetComponent<CharacterClassManager>();
            if (ccm != null && player.name == "Host")
            {
                ccm.ForceRoundStart();
                break;
            }
        }
    }
}
