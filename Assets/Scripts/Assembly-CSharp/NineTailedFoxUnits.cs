using Mirror;
using TMPro;
using UnityEngine;

public class NineTailedFoxUnits : NetworkBehaviour
{
    public string[] names;

    public readonly SyncList<string> list = new SyncList<string>();

    private CharacterClassManager ccm;
    private TextMeshProUGUI txtlist;

    public static NineTailedFoxUnits host;

    private void Awake()
    {
        list.Callback += OnListChanged;
    }
    
    private void OnListChanged(SyncList<string>.Operation op, int index, string oldItem, string newItem)
    {
        // ћожешь логировать или обновл€ть UI если нужно
    }
    
    [Server]
    private void AddUnit(string unit)
    {
        list.Add(unit);
    }

    private string GenerateName()
    {
        return names[Random.Range(0, names.Length)] + "-" + Random.Range(1, 20).ToString("00");
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        ccm = GetComponent<CharacterClassManager>();
        txtlist = GameObject.Find("NTFlist")?.GetComponent<TextMeshProUGUI>();
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        ccm = GetComponent<CharacterClassManager>();
        txtlist = GameObject.Find("NTFlist")?.GetComponent<TextMeshProUGUI>();

        if (isServer)
        {
            NewName();
            host = this;
        }
        else
        {
            host = null;
        }
    }

    private void Update()
    {
        if (!isLocalPlayer || host == null || txtlist == null || ccm == null)
            return;

        txtlist.text = string.Empty;

        if (ccm.curClass <= 0 || ccm.klasy[ccm.curClass].team != Team.MTF)
            return;

        for (int i = 0; i < host.list.Count; i++)
        {
            if (i == ccm.ntfUnit)
            {
                txtlist.text += "<u>" + host.GetNameById(i) + "</u>\n";
            }
            else
            {
                txtlist.text += host.GetNameById(i) + "\n";
            }
        }
    }

    [Server]
    public int NewName(out int number, out char letter)
    {
        int attempts = 0;
        string text = GenerateName();
        while (list.Contains(text) && attempts < 100)
        {
            attempts++;
            text = GenerateName();
        }

        letter = text.ToUpper()[0];
        number = int.Parse(text.Split('-')[1]);
        AddUnit(text);
        return list.Count - 1;
    }

    [Server]
    public int NewName()
    {
        return NewName(out _, out _);
    }

    public string GetNameById(int id)
    {
        return (id >= 0 && id < list.Count) ? list[id] : "???";
    }
}
