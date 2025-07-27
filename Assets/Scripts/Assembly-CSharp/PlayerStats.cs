using System;
using System.Linq;
using Dissonance.Integrations.UNet_HLAPI;
using Mirror;
using RemoteAdmin;
using Unity;
using UnityEngine;

public class PlayerStats : NetworkBehaviour
{
    [Serializable]
    public struct HitInfo
    {
        public float amount;

        public int tool;

        public int time;

        public string attacker;

        public int plyID;

        public HitInfo(float amnt, string attackerName, DamageTypes.DamageType weapon, int attackerID)
        {
            amount = amnt;
            tool = DamageTypes.ToIndex(weapon);
            attacker = attackerName;
            plyID = attackerID;
            time = ServerTime.time;
        }

        public readonly GameObject GetPlayerObject()
        {
            GameObject[] players = PlayerManager.singleton.players;
            foreach (GameObject gameObject in players)
            {
                if (gameObject.GetComponent<QueryProcessor>().PlayerId == plyID)
                {
                    return gameObject;
                }
            }
            return null;
        }

        public readonly DamageTypes.DamageType GetDamageType()
        {
            return DamageTypes.FromIndex(tool);
        }

        public readonly string GetDamageName()
        {
            return DamageTypes.FromIndex(tool).name;
        }
    }

    public HitInfo lastHitInfo = new HitInfo(0f, "NONE", DamageTypes.None, 0);

    public Transform[] grenadePoints;

    public CharacterClassManager ccm;

    private UserMainInterface _ui;

    private static Lift[] _lifts;

    public int maxHP;

    public bool used914;

    private bool _pocketCleanup;

    private bool _allowSPDmg;

    private int _health;

    private bool _hpDirty;

    private float killstreak_time;

    private int killstreak;

    public int Health
    {
        get
        {
            return _health;
        }
        set
        {
            _health = value;
            _hpDirty = true;
        }
    }

    public void MakeHpDirty()
    {
        _hpDirty = true;
    }

    private void Start()
    {
        _pocketCleanup = ConfigFile.ServerConfig.GetBool("SCP106_CLEANUP");
        _allowSPDmg = ConfigFile.ServerConfig.GetBool("spawn_protect_allow_dmg", true);
        ccm = GetComponent<CharacterClassManager>();
        _ui = UserMainInterface.singleton;
        if (_lifts == null || _lifts.Length == 0) // Добавлена проверка на null
        {
            _lifts = UnityEngine.Object.FindObjectsOfType<Lift>();
        }
    }

    private void Update()
    {
        if (base.isLocalPlayer && ccm.curClass != 2)
        {
            _ui.SetHP((Health >= 0) ? Health : 0, maxHP);
        }
        if (base.isLocalPlayer)
        {
            _ui.hpOBJ.SetActive(ccm.curClass != 2);
        }
        if (!_hpDirty)
        {
            return;
        }
        _hpDirty = false;

        if (NetworkServer.active)
        {
            TargetSyncHp(base.connectionToClient, _health);

            foreach (CharacterClassManager item in PlayerManager.singleton.players.Select((GameObject s) => s.GetComponent<CharacterClassManager>()))
            {
                if (item.curClass == 2 && item.IsVerified)
                {
                    if (item.connectionToClient != null)
                    {
                        TargetSyncHp(item.connectionToClient, _health);
                    }
                }
            }
        }
    }

    [TargetRpc(channel = 2)]
    public void TargetSyncHp(NetworkConnection conn, int hp)
    {
        _health = hp;
    }

    public float GetHealthPercent()
    {
        if (ccm.curClass < 0)
        {
            return 0f;
        }
        return Mathf.Clamp01(1f - (float)Health / (float)ccm.klasy[ccm.curClass].maxHP);
    }

    [Command(channel = 2)]
    public void CmdSelfDeduct(HitInfo info)
    {
        HurtPlayer(info, base.gameObject);
    }

    public bool Explode(bool inElevator)
    {
        bool flag = Health > 0 && (inElevator || base.transform.position.y < 900f);
        switch (ccm.curClass)
        {
            case 7:
                flag = true;
                break;
            case 3:
                {
                    Scp106PlayerScript component = GetComponent<Scp106PlayerScript>();
                    if (component != null)
                    {
                        component.DeletePortal();
                    }
                    bool? flag2 = (((object)component != null) ? new bool?(component.goingViaThePortal) : ((bool?)null));
                    if (flag2.HasValue && flag2.Value)
                    {
                        flag = true;
                    }
                    break;
                }
        }
        return flag && HurtPlayer(new HitInfo(-1f, "WORLD", DamageTypes.Nuke, 0), base.gameObject);
    }

    [Command(channel = 2)]
    public void CmdTesla()
    {
        HurtPlayer(new HitInfo(UnityEngine.Random.Range(100, 200), GetComponent<HlapiPlayer>().PlayerId, DamageTypes.Tesla, 0), base.gameObject);
    }

    public void SetHPAmount(int hp)
    {
        Health = hp;
    }

    public bool HealHPAmount(int hp)
    {
        int num = Mathf.Clamp(hp, 0, maxHP - Health);
        Health = ((Health + num <= Health) ? Health : (Health + num));
        return num > 0;
    }

    public bool HurtPlayer(HitInfo info, GameObject go)
    {
        bool result = false;
        if (info.amount < 0f)
        {
            int? obj;
            if (go == null)
            {
                obj = null;
            }
            else
            {
                PlayerStats component = go.GetComponent<PlayerStats>();
                obj = ((component != null) ? new int?(component.Health) : ((int?)null));
            }
            int? num = obj + 1;
            info.amount = Mathf.Abs((!num.HasValue) ? 999999f : ((float)num.Value));
        }
        if (info.amount > 2.1474836E+09f)
        {
            info.amount = 2.1474836E+09f;
        }
        if (go != null)
        {
            PlayerStats component2 = go.GetComponent<PlayerStats>();
            CharacterClassManager component3 = go.GetComponent<CharacterClassManager>();
            if (component3.GodMode)
            {
                return false;
            }
            if (ccm.curClass > -1 && component3.curClass > -1 && ccm.klasy[ccm.curClass].team == Team.SCP && ccm.klasy[component3.curClass].team == Team.SCP && ccm != component3)
            {
                return false;
            }
            if (component3.SpawnProtected && !_allowSPDmg)
            {
                return false;
            }
            if (base.isLocalPlayer && info.plyID != go.GetComponent<QueryProcessor>().PlayerId)
            {
                RoundSummary.Damages += ((!(component2.Health < info.amount)) ? ((int)info.amount) : component2.Health);
            }
            component2.Health -= Mathf.CeilToInt(info.amount);
            if (Mathf.CeilToInt(component2.Health) < 0)
            {
                component2.Health = 0;
            }
            component2.lastHitInfo = info;
            if (component2.Health < 1 && component3.curClass != 2)
            {
                foreach (Scp079PlayerScript instance in Scp079PlayerScript.instances)
                {
                    Scp079Interactable.ZoneAndRoom otherRoom = go.GetComponent<Scp079PlayerScript>().GetOtherRoom();
                    Scp079Interactable.InteractableType[] filter = new Scp079Interactable.InteractableType[5]
                    {
                        Scp079Interactable.InteractableType.Door,
                        Scp079Interactable.InteractableType.Light,
                        Scp079Interactable.InteractableType.Lockdown,
                        Scp079Interactable.InteractableType.Tesla,
                        Scp079Interactable.InteractableType.ElevatorUse
                    };
                    bool flag = false;
                    foreach (Scp079Interaction item in instance.ReturnRecentHistory(12f, filter))
                    {
                        foreach (Scp079Interactable.ZoneAndRoom currentZonesAndRoom in item.interactable.currentZonesAndRooms)
                        {
                            if (currentZonesAndRoom.currentZone == otherRoom.currentZone && currentZonesAndRoom.currentRoom == otherRoom.currentRoom)
                            {
                                flag = true;
                            }
                        }
                    }
                    if (flag)
                    {
                        instance.RpcGainExp(ExpGainType.KillAssist, component3.curClass);
                    }
                }
                if (RoundSummary.RoundInProgress() && RoundSummary.roundTime < 60)
                {
                    TargetAchieve(component3.connectionToClient, "wowreally");
                }
                if (base.isLocalPlayer && info.plyID != go.GetComponent<QueryProcessor>().PlayerId)
                {
                    RoundSummary.Kills++;
                }
                result = true;
                if (component3.curClass == 9 && go.GetComponent<Scp096PlayerScript>().enraged == Scp096PlayerScript.RageState.Panic)
                {
                    TargetAchieve(component3.connectionToClient, "unvoluntaryragequit");
                }
                else if (info.GetDamageType() == DamageTypes.Pocket)
                {
                    TargetAchieve(component3.connectionToClient, "newb");
                }
                else if (info.GetDamageType() == DamageTypes.Scp173)
                {
                    TargetAchieve(component3.connectionToClient, "firsttime");
                }
                else if (info.GetDamageType() == DamageTypes.Grenade && info.plyID == go.GetComponent<QueryProcessor>().PlayerId)
                {
                    TargetAchieve(component3.connectionToClient, "iwanttobearocket");
                }
                else if (info.GetDamageType().isWeapon)
                {
                    if (component3.curClass == 6 && component3.GetComponent<Inventory>().curItem >= 0 && component3.GetComponent<Inventory>().curItem <= 11 && GetComponent<CharacterClassManager>().curClass == 1)
                    {
                        TargetAchieve(base.connectionToClient, "betrayal");
                    }
                    if (Time.realtimeSinceStartup - killstreak_time > 30f || killstreak == 0)
                    {
                        killstreak = 0;
                        killstreak_time = Time.realtimeSinceStartup;
                    }
                    if (GetComponent<WeaponManager>().GetShootPermission(component3, true))
                    {
                        killstreak++;
                    }
                    if (killstreak > 5)
                    {
                        TargetAchieve(base.connectionToClient, "pewpew");
                    }
                    if (ccm.curClass > -1 && (ccm.klasy[ccm.curClass].team == Team.MTF || ccm.klasy[ccm.curClass].team == Team.RSC) && component3.curClass == 1)
                    {
                        TargetStats(base.connectionToClient, "dboys_killed", "justresources", 50);
                    }
                    if (ccm.curClass > -1 && ccm.klasy[ccm.curClass].team == Team.RSC && component3.curClass > -1 && ccm.klasy[component3.curClass].team == Team.SCP)
                    {
                        TargetAchieve(base.connectionToClient, "timetodoitmyself");
                    }
                }
                ServerLogs.AddLog(ServerLogs.Modules.ClassChange, "Player " + go.GetComponent<NicknameSync>().myNick + " (" + go.GetComponent<CharacterClassManager>().SteamId + ") killed by " + info.attacker + " using " + info.GetDamageName() + ".", ServerLogs.ServerLogType.KillLog);
                if (!_pocketCleanup || info.GetDamageType() != DamageTypes.Pocket)
                {
                    go.GetComponent<Inventory>().ServerDropAll();
                    if (component3.curClass >= 0 && info.GetDamageType() != DamageTypes.RagdollLess)
                    {
                        GetComponent<RagdollManager>().SpawnRagdoll(go.transform.position, go.transform.rotation, component3.curClass, info, component3.klasy[component3.curClass].team != Team.SCP, go.GetComponent<HlapiPlayer>().PlayerId, go.GetComponent<NicknameSync>().myNick, go.GetComponent<QueryProcessor>().PlayerId);
                    }
                }
                else
                {
                    go.GetComponent<Inventory>().Clear();
                }
                component3.deathPosition = go.transform.position;
                if (component3.curClass > -1 && component3.curClass != 10 && component3.klasy[component3.curClass].team == Team.SCP)
                {
                    GameObject gameObject = null;
                    GameObject[] players = PlayerManager.singleton.players;
                    foreach (GameObject gameObject2 in players)
                    {
                        if (gameObject2.GetComponent<QueryProcessor>().PlayerId == info.plyID)
                        {
                            gameObject = gameObject2;
                        }
                    }
                    if (gameObject != null)
                    {
                        RpcAnnounceScpKill(component3.klasy[component3.curClass].fullName, gameObject);
                    }
                    else
                    {
                        string text = string.Empty;
                        if (component3.klasy[component3.curClass].fullName.Contains("-"))
                        {
                            string text2 = component3.klasy[component3.curClass].fullName.Split('-')[1];
                            foreach (char c in text2)
                            {
                                text = text + c + " ";
                            }
                        }
                        MTFRespawn component4 = PlayerManager.localPlayer.GetComponent<MTFRespawn>();
                        DamageTypes.DamageType damageType = info.GetDamageType();
                        if (component3.curClass != 7)
                        {
                            if (damageType == DamageTypes.Tesla)
                            {
                                component4.RpcPlayCustomAnnouncement("SCP " + text + " SUCCESSFULLY TERMINATED BY AUTOMATIC SECURITY SYSTEM", false);
                            }
                            else if (damageType == DamageTypes.Nuke)
                            {
                                component4.RpcPlayCustomAnnouncement("SCP " + text + " TERMINATED BY ALPHA WARHEAD", false);
                            }
                            else if (damageType == DamageTypes.Decont)
                            {
                                component4.RpcPlayCustomAnnouncement("SCP " + text + " LOST IN DECONTAMINATION SEQUENCE", false);
                            }
                            else
                            {
                                RpcAnnounceScpKill(component3.klasy[component3.curClass].fullName, null);
                            }
                        }
                    }
                }
                component2.SetHPAmount(100);
                component3.SetClassID(2);
                if (TutorialManager.status)
                {
                    if (PlayerManager.localPlayer != null) // Добавлена проверка на null
                    {
                        PlayerManager.localPlayer.GetComponent<TutorialManager>().KillNPC();
                    }
                }
            }
            else
            {
                Vector3 pos = Vector3.zero;
                float num2 = 40f;
                if (info.GetDamageType().isWeapon)
                {
                    GameObject playerOfID = GetPlayerOfID(info.plyID);
                    if (playerOfID != null)
                    {
                        pos = go.transform.InverseTransformPoint(playerOfID.transform.position).normalized;
                        num2 = 100f;
                    }
                }
                if (component3.curClass > -1 && (component3.curClass == 16 || component3.curClass == 17))
                {
                    component3.GetComponent<Scp939PlayerScript>().NetworkspeedMultiplier = 1.25f;
                }
                TargetOofEffect(go.GetComponent<NetworkIdentity>().connectionToClient, pos, Mathf.Clamp01(info.amount / num2));
            }
        }
        return result;
    }

    [TargetRpc]
    public void TargetAchieve(NetworkConnection conn, string key)
    {
        AchievementManager.Achieve(key);
    }

    [ClientRpc]
    private void RpcAnnounceScpKill(string scpnum, GameObject exec)
    {
        NineTailedFoxAnnouncer.singleton.AnnounceScpKill(scpnum, (!(exec == null)) ? exec.GetComponent<CharacterClassManager>() : null);
    }

    [TargetRpc]
    public void TargetStats(NetworkConnection conn, string key, string targetAchievement, int maxValue)
    {
        AchievementManager.StatsProgress(key, targetAchievement, maxValue);
    }

    private GameObject GetPlayerOfID(int id)
    {
        return PlayerManager.singleton.players.FirstOrDefault((GameObject ply) => ply.GetComponent<QueryProcessor>().PlayerId == id);
    }

    [TargetRpc]
    private void TargetOofEffect(NetworkConnection conn, Vector3 pos, float overall)
    {
        OOF_Controller.singleton.AddBlood(pos, overall);
    }

    [ClientRpc(channel = 7)]
    private void RpcRoundrestart()
    {
        if (!base.isServer)
        {
            CustomNetworkManager customNetworkManager = UnityEngine.Object.FindObjectOfType<CustomNetworkManager>();
            customNetworkManager.reconnect = true;
            Invoke("ChangeLevel", 0.5f);
        }
    }

    public void Roundrestart()
    {
        RpcRoundrestart();
        Invoke("ChangeLevel", 2.5f);
    }

    private void ChangeLevel()
    {
        if (base.isServer)
        {
            GC.Collect();
            NetworkManager.singleton.ServerChangeScene(NetworkManager.singleton.onlineScene);
        }
        else
        {
            NetworkManager.singleton.StopClient();
        }
    }

    public string HealthToString()
    {
        double num = (double)Health / (double)maxHP * 100.0;
        return Health + "/" + maxHP + "(" + num + "%)";
    }
}