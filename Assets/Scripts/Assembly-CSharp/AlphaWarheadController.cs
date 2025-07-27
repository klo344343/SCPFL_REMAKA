using System;
using System.Collections.Generic;
using System.IO;
using MEC;
using Mirror;
using UnityEngine;

public class AlphaWarheadController : NetworkBehaviour
{
    [Serializable]
    public class DetonationScenario
    {
        public AudioClip clip;
        public int tMinusTime;
        public float additionalTime;

        public float SumTime() => tMinusTime + additionalTime;
    }

    public DetonationScenario[] scenarios_start;
    public DetonationScenario[] scenarios_resume;
    public AudioClip sound_canceled;

    internal BlastDoor[] blastDoors;

    public bool doorsClosed;
    public bool doorsOpen;
    public bool detonated;

    public int cooldown = 30;
    public int warheadKills;

    private static int _startScenario;
    private static int _resumeScenario;
    private float _shake;

    public static AudioSource alarmSource;
    public static AlphaWarheadController host;

    [SyncVar(hook = nameof(OnTimeToDetonationChanged))]
    public float timeToDetonation;

    [SyncVar(hook = nameof(OnStartScenarioChanged))]
    public int sync_startScenario;

    [SyncVar(hook = nameof(OnResumeScenarioChanged))]
    public int sync_resumeScenario = -1;

    [SyncVar(hook = nameof(OnInProgressChanged))]
    public bool inProgress;

    private string file;

    public float TimeToDetonation
    {
        get => timeToDetonation;
        set
        {
            if (!isServer) return;
            timeToDetonation = value;
        }
    }

    public int StartScenario
    {
        get => sync_startScenario;
        set
        {
            if (!isServer) return;
            sync_startScenario = value;
        }
    }

    public int ResumeScenario
    {
        get => sync_resumeScenario;
        set
        {
            if (!isServer) return;
            sync_resumeScenario = value;
        }
    }

    public bool InProgress
    {
        get => inProgress;
        set
        {
            if (!isServer) return;
            inProgress = value;
        }
    }

    private void Start()
    {
        if (!isLocalPlayer || TutorialManager.status)
            return;

        Timing.RunCoroutine(ReadCustomTranslations(), Segment.FixedUpdate);

        alarmSource = GameObject.Find("GameManager").GetComponent<AudioSource>();
        blastDoors = FindObjectsOfType<BlastDoor>();

        if (!isServer)
            return;

        int value = ConfigFile.ServerConfig.GetInt("warhead_tminus_start_duration", 90);
        value = Mathf.Clamp(value, 80, 120);
        value = Mathf.RoundToInt(value / 10f) * 10;

        StartScenario = 3;

        for (int i = 0; i < scenarios_start.Length; i++)
        {
            if (scenarios_start[i].tMinusTime == value)
            {
                StartScenario = i;
                break;
            }
        }
    }

    private void OnTimeToDetonationChanged(float oldVal, float newVal)
    {
        timeToDetonation = newVal;
    }

    private void OnStartScenarioChanged(int oldVal, int newVal)
    {
        sync_startScenario = newVal;
        _startScenario = newVal;
    }

    private void OnResumeScenarioChanged(int oldVal, int newVal)
    {
        sync_resumeScenario = newVal;
        _resumeScenario = newVal;
    }

    private void OnInProgressChanged(bool oldVal, bool newVal)
    {
        inProgress = newVal;
    }

    public void StartDetonation()
    {
        if (Recontainer079.isLocked) return;

        doorsOpen = false;
        ServerLogs.AddLog(ServerLogs.Modules.Warhead, "Countdown started.", ServerLogs.ServerLogType.GameEvent);

        if ((_resumeScenario == -1 && scenarios_start[_startScenario].SumTime() == timeToDetonation) ||
            (_resumeScenario != -1 && scenarios_resume[_resumeScenario].SumTime() == timeToDetonation))
        {
            InProgress = true;
        }
    }

    public void InstantPrepare()
    {
        TimeToDetonation = (_resumeScenario != -1) ? scenarios_resume[_resumeScenario].SumTime() : scenarios_start[_startScenario].SumTime();
    }

    private IEnumerator<float> ReadCustomTranslations()
    {
        foreach (var source in scenarios_resume)
        {
            string path = TranslationReader.path + "/Custom Audio/" + source.clip.name + ".ogg";
            if (!File.Exists(path)) yield break;

            file = Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor ? "file:///" : "file://";
            using (var www = new WWW(file + path))
            {
                source.clip = www.GetAudioClip(false);
                while (source.clip.loadState != AudioDataLoadState.Loaded)
                    yield return Timing.WaitUntilDone(www);
            }
            source.clip.name = Path.GetFileName(path);
        }

        foreach (var source in scenarios_start)
        {
            string path = TranslationReader.path + "/Custom Audio/" + source.clip.name + ".ogg";
            if (!File.Exists(path)) break;

            file = Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor ? "file:///" : "file://";
            using (var www = new WWW(file + path))
            {
                source.clip = www.GetAudioClip(false);
                while (source.clip.loadState != AudioDataLoadState.Loaded)
                    yield return Timing.WaitUntilDone(www);
            }
            source.clip.name = Path.GetFileName(path);
        }
    }

    public void CancelDetonation(GameObject disabler = null)
    {
        ServerLogs.AddLog(ServerLogs.Modules.Warhead, "Detonation cancelled.", ServerLogs.ServerLogType.GameEvent);

        if (!inProgress || timeToDetonation <= 10f) return;

        if (timeToDetonation <= 15f && disabler != null)
        {
            GetComponent<PlayerStats>().TargetAchieve(disabler.GetComponent<NetworkIdentity>().connectionToClient, "thatwasclose");
        }

        for (int i = 0; i < scenarios_resume.Length; i++)
        {
            if (scenarios_resume[i].SumTime() > timeToDetonation && scenarios_resume[i].SumTime() < scenarios_start[_startScenario].SumTime())
            {
                ResumeScenario = i;
                break;
            }
        }

        SetTime(((_resumeScenario >= 0) ? scenarios_resume[_resumeScenario].SumTime() : scenarios_start[_startScenario].SumTime()) + cooldown);
        InProgress = false;

        foreach (var door in FindObjectsOfType<Door>())
        {
            door.warheadlock = false;
            door.UpdateLock();
        }
    }

    private void SetTime(float value)
    {
        if (isServer)
            TimeToDetonation = value;
    }

    internal void Detonate()
    {
        ServerLogs.AddLog(ServerLogs.Modules.Warhead, "Warhead detonated.", ServerLogs.ServerLogType.GameEvent);
        detonated = true;
        RpcShake();

        var liftTargets = GameObject.FindGameObjectsWithTag("LiftTarget");
        var players = PlayerManager.singleton.players;

        foreach (var player in players)
        {
            foreach (var liftTarget in liftTargets)
            {
                if (player.GetComponent<PlayerStats>().Explode(Vector3.Distance(liftTarget.transform.position, player.transform.position) < 3.5f))
                {
                    warheadKills++;
                }
            }
        }

        foreach (var door in FindObjectsOfType<Door>())
        {
            if (door.blockAfterDetonation)
                door.OpenWarhead(true, true);
        }
    }

    [ClientRpc]
    private void RpcShake()
    {
        ExplosionCameraShake.singleton.Shake(1f);
        if (PlayerManager.localPlayer.transform.position.y > 900f)
        {
            AchievementManager.Achieve("tminus");
        }
    }

    private void FixedUpdate()
    {
        if (name == "Host")
        {
            host = this;
            _startScenario = sync_startScenario;
            _resumeScenario = sync_resumeScenario;
        }

        if (host == null || !isLocalPlayer) return;

        UpdateSourceState();

        if (isServer)
            ServerCountdown();
    }

    private void UpdateSourceState()
    {
        if (TutorialManager.status) return;

        if (host.inProgress)
        {
            if (host.timeToDetonation != 0f)
            {
                if (!alarmSource.isPlaying)
                {
                    alarmSource.volume = 1f;
                    alarmSource.clip = (_resumeScenario >= 0) ? scenarios_resume[_resumeScenario].clip : scenarios_start[_startScenario].clip;
                    alarmSource.Play();
                    return;
                }

                float realTime = RealDetonationTime();
                float delta = realTime - host.timeToDetonation;

                if (Mathf.Abs(alarmSource.time - delta) > 0.5f)
                    alarmSource.time = Mathf.Clamp(delta, 0f, realTime);
            }

            if (host.timeToDetonation < 5f && host.timeToDetonation != 0f)
            {
                _shake += Time.fixedDeltaTime / 20f;
                _shake = Mathf.Clamp(_shake, 0f, 0.5f);

                if (Vector3.Distance(transform.position, AlphaWarheadOutsitePanel.nukeside.transform.position) < 100f)
                    ExplosionCameraShake.singleton.Shake(_shake);
            }
        }
        else if (alarmSource.isPlaying && alarmSource.clip != null)
        {
            alarmSource.Stop();
            alarmSource.clip = null;
            alarmSource.PlayOneShot(sound_canceled);
        }
    }

    public float RealDetonationTime()
    {
        return (_resumeScenario < 0) ? scenarios_start[_startScenario].SumTime() : scenarios_resume[_resumeScenario].SumTime();
    }

    [ServerCallback]
    private void ServerCountdown()
    {
        if (!NetworkServer.active) return;

        float totalTime = RealDetonationTime();
        float currentTime = timeToDetonation;

        if (timeToDetonation != 0f)
        {
            if (inProgress)
            {
                currentTime -= Time.fixedDeltaTime;

                if (currentTime < 2f && !doorsClosed)
                {
                    doorsClosed = true;
                    foreach (var door in blastDoors)
                        door.IsClosed = true;
                }

                if (ConfigFile.ServerConfig.GetBool("open_doors_on_countdown", true) && !doorsOpen &&
                    currentTime < totalTime - ((_resumeScenario < 0) ? scenarios_start[_startScenario].additionalTime : scenarios_resume[_resumeScenario].additionalTime))
                {
                    doorsOpen = true;
                    bool lockGates = ConfigFile.ServerConfig.GetBool("lock_gates_on_countdown", true);
                    bool isolateZones = ConfigFile.ServerConfig.GetBool("isolate_zones_on_countdown");

                    foreach (var door in FindObjectsOfType<Door>())
                    {
                        if (isolateZones && door.DoorName.Contains("CHECKPOINT"))
                        {
                            door.warheadlock = true;
                            door.UpdateLock();
                            door.SetStateWithSound(false);
                        }
                        else
                        {
                            door.OpenWarhead(false, lockGates || !door.DoorName.Contains("GATE"));
                        }
                    }
                }

                if (currentTime <= 0f)
                    Detonate();

                currentTime = Mathf.Clamp(currentTime, 0f, totalTime);
            }
            else
            {
                if (currentTime > totalTime)
                    currentTime -= Time.fixedDeltaTime;

                currentTime = Mathf.Clamp(currentTime, totalTime, cooldown + totalTime);
            }
        }

        if (currentTime != timeToDetonation)
            TimeToDetonation = currentTime;
    }
}
