using Mirror;
using UnityEngine;
using UnityEngine.UI;

public class AlphaWarheadOutsitePanel : NetworkBehaviour
{
    public Animator panelButtonCoverAnim;
    public static AlphaWarheadNukesitePanel nukeside;
    private static AlphaWarheadController _host;
    public Text[] display;
    public GameObject[] inevitable;

    [SyncVar(hook = nameof(OnKeycardEnteredChanged))]
    private bool keycardEntered;

    public bool KeycardEntered
    {
        get => keycardEntered;
        set
        {
            if (isServer)
            {
                keycardEntered = value;
            }
        }
    }

    private void Update()
    {
        if (_host == null)
        {
            _host = AlphaWarheadController.host;
            return;
        }

        transform.localPosition = new Vector3(0f, 0f, 9f);

        foreach (var text in display)
            text.text = GetTimeString();

        foreach (var go in inevitable)
            go.SetActive(_host.timeToDetonation <= 10f && _host.timeToDetonation > 0f);

        panelButtonCoverAnim.SetBool("enabled", keycardEntered);
    }

    private void OnKeycardEnteredChanged(bool oldVal, bool newVal)
    {
        keycardEntered = newVal;
    }

    public void SetKeycardState(bool state)
    {
        if (isServer)
            KeycardEntered = state;
    }

    public static string GetTimeString()
    {
        if (!nukeside.enabled && !_host.inProgress)
            return "<size=180><color=red>DISABLED</color></size>";

        if (!_host.inProgress)
        {
            bool ready = !(_host.timeToDetonation > _host.RealDetonationTime());
            return ready ? "<color=lime><size=180>READY</size></color>" : "<color=red><size=200>PLEASE WAIT</size></color>";
        }

        if (_host.timeToDetonation == 0f)
        {
            return ((int)(Time.realtimeSinceStartup * 4f) % 2 != 0) ? "<color=orange><size=270>00:00:00</size></color>" : "";
        }

        float num = (_host.RealDetonationTime() - AlphaWarheadController.alarmSource.time) * 100f;
        num *= 1f + 2.5f / _host.RealDetonationTime();
        num = Mathf.Max(num, 0f);

        int seconds = 0;
        int minutes = 0;
        while (num >= 100f)
        {
            num -= 100f;
            seconds++;
        }
        while (seconds >= 60)
        {
            seconds -= 60;
            minutes++;
        }

        return $"<color=orange><size=270>{minutes:00}:{seconds:00}:{(int)num:00}</size></color>";
    }
}
