using Mirror;
using System;
using UnityEngine;

public class AmmoBox : NetworkBehaviour
{
    [Serializable]
    public class AmmoType
    {
        public string label;
        public int inventoryID;
    }

    private Inventory inv;
    private CharacterClassManager ccm;

    public AmmoType[] types;

    [SyncVar(hook = nameof(OnAmountChanged))]
    private string amount = "0:0:0";

    public string NetworkAmount
    {
        get => amount;
        set
        {
            if (isServer)
            {
                amount = value;
            }
        }
    }

    private void OnAmountChanged(string oldAmount, string newAmount)
    {
        amount = newAmount;
    }

    private void Start()
    {
        inv = GetComponent<Inventory>();
        ccm = GetComponent<CharacterClassManager>();
        if (isServer)
            SetAmmoAmount();
    }

    public void SetAmmoAmount()
    {
        if (ccm == null || ccm.klasy == null || ccm.klasy.Length == 0)
            return;

        int[] ammoTypes = ccm.klasy[ccm.curClass].ammoTypes;
        if (ammoTypes.Length >= 3)
        {
            NetworkAmount = $"{ammoTypes[0]}:{ammoTypes[1]}:{ammoTypes[2]}";
        }
    }

    public int GetAmmo(int type)
    {
        if (string.IsNullOrEmpty(amount))
            return 0;

        string[] parts = amount.Split(':');
        if (type < 0 || type >= parts.Length)
            return 0;

        if (int.TryParse(parts[type], out int result))
            return result;

        Debug.LogWarning("Parse failed for ammo amount");
        return 0;
    }

    [Command(channel = 2)]
    public void CmdDrop(int toDrop, int type)
    {
        if (types == null || type < 0 || type >= types.Length)
            return;

        int currentAmmo = GetAmmo(type);
        toDrop = Mathf.Clamp(toDrop, 0, currentAmmo);

        if (toDrop >= 15 && inv != null)
        {
            string[] parts = amount.Split(':');
            parts[type] = (currentAmmo - toDrop).ToString();

            NetworkAmount = string.Join(":", parts);
            inv.SetPickup(types[type].inventoryID, toDrop, transform.position, inv.camera.transform.rotation, 0, 0, 0);
        }
    }
}
