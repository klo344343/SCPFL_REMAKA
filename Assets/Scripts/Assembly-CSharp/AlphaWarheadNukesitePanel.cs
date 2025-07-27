using Mirror;
using UnityEngine;

public class AlphaWarheadNukesitePanel : NetworkBehaviour
{
    public Transform lever;
    public BlastDoor blastDoor;
    public Door outsideDoor;
    public Material led_blastdoors;
    public Material led_outsidedoor;
    public Material led_detonationinprogress;
    public Material led_cancel;
    public Material[] onOffMaterial;

    private float _leverStatus;

    [SyncVar(hook = nameof(OnEnabledChanged))]
    private bool enabledState;

    public bool Enabled
    {
        get => enabledState;
        set
        {
            if (isServer)
                enabledState = value;
        }
    }

    private void Awake()
    {
        AlphaWarheadOutsitePanel.nukeside = this;
    }

    private void FixedUpdate()
    {
        UpdateLeverStatus();
    }

    public bool AllowChangeLevelState()
    {
        return Mathf.Approximately(_leverStatus, 0f) || Mathf.Approximately(_leverStatus, 1f);
    }

    private void UpdateLeverStatus()
    {
        if (AlphaWarheadController.host == null)
            return;

        Color activeColor = new Color(0.2f, 0.3f, 0.5f);

        led_detonationinprogress.SetColor("_EmissionColor", AlphaWarheadController.host.inProgress ? activeColor : Color.black);
        led_outsidedoor.SetColor("_EmissionColor", outsideDoor.IsOpen ? activeColor : Color.black);
        led_blastdoors.SetColor("_EmissionColor", blastDoor.IsClosed ? activeColor : Color.black);
        led_cancel.SetColor("_EmissionColor", (AlphaWarheadController.host.timeToDetonation > 10f && AlphaWarheadController.host.inProgress) ? Color.red : Color.black);

        _leverStatus = Mathf.Clamp01(_leverStatus + (enabledState ? 0.04f : -0.04f));

        for (int i = 0; i < onOffMaterial.Length; i++)
        {
            onOffMaterial[i].SetColor("_EmissionColor", i == Mathf.RoundToInt(_leverStatus) ? new Color(1.2f, 1.2f, 1.2f, 1f) : Color.black);
        }

        lever.localRotation = Quaternion.Euler(Mathf.Lerp(10f, -170f, _leverStatus), -90f, 90f);
    }

    private void OnEnabledChanged(bool oldVal, bool newVal)
    {
        enabledState = newVal;
    }

    public void SetEnabled(bool state)
    {
        if (isServer)
            Enabled = state;
    }
}
