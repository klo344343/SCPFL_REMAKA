using Mirror;
using TMPro;
using UnityEngine;
public class RagdollManager : NetworkBehaviour
{
	public LayerMask inspectionMask;

	private Transform cam;

	private CharacterClassManager ccm;

	private TextMeshProUGUI txt;

	public void SpawnRagdoll(Vector3 pos, Quaternion rot, int classId, PlayerStats.HitInfo ragdollInfo, bool allowRecall, string ownerID, string ownerNick, int playerId)
	{
		Class obj = ccm.klasy[classId];
		if (obj.model_ragdoll != null)
		{
			GameObject gameObject = Object.Instantiate(obj.model_ragdoll, pos + obj.ragdoll_offset.position, Quaternion.Euler(rot.eulerAngles + obj.ragdoll_offset.rotation));
			NetworkServer.Spawn(gameObject);
			gameObject.GetComponent<Ragdoll>().owner = (new Ragdoll.Info(ownerID, ownerNick, ragdollInfo, classId, playerId));
			gameObject.GetComponent<Ragdoll>().allowRecall = allowRecall;
		}
		if (ragdollInfo.GetDamageType().isScp || ragdollInfo.GetDamageType() == DamageTypes.Pocket)
		{
			RegisterScpFrag();
		}
	}

	private void Start()
	{
		txt = GameObject.Find("BodyInspection").GetComponentInChildren<TextMeshProUGUI>();
		cam = GetComponent<Scp049PlayerScript>().PlayerCameraGameObject.transform;
		ccm = GetComponent<CharacterClassManager>();
	}

    public void Update()
    {
        if (!isLocalPlayer || txt == null || cam == null || ccm == null || ccm.klasy == null)
            return;

        string text = string.Empty;
        if (Physics.Raycast(new Ray(cam.position, cam.forward), out RaycastHit hitInfo, 3f, inspectionMask) && ccm.curClass != 2)
        {
            var componentInParent = hitInfo.transform.GetComponentInParent<Ragdoll>();
            if (componentInParent != null && ccm.klasy.Length > componentInParent.owner.charclass)
            {
                text = TranslationReader.Get("Death_Causes", 12)
                    .Replace("[user]", componentInParent.owner.steamClientName)
                    .Replace("[cause]", GetCause(componentInParent.owner.deathCause, false))
                    .Replace("[class]", "<color=" + GetColor(ccm.klasy[componentInParent.owner.charclass].classColor) + ">" + ccm.klasy[componentInParent.owner.charclass].fullName + "</color>");
            }
        }
        txt.text = text;
    }


    public string GetColor(Color c)
	{
		Color32 color = new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), byte.MaxValue);
		return "#" + color.r.ToString("X2") + color.g.ToString("X2") + color.b.ToString("X2");
	}

	public void RegisterScpFrag()
	{
		RoundSummary.kills_by_scp++;
	}

	public static string GetCause(PlayerStats.HitInfo info, bool ragdoll)
	{
		string result = TranslationReader.Get("Death_Causes", 11);
		if (info.GetDamageType() == DamageTypes.Nuke)
		{
			result = TranslationReader.Get("Death_Causes", 0);
		}
		else if (info.GetDamageType() == DamageTypes.Falldown)
		{
			result = TranslationReader.Get("Death_Causes", 1);
		}
		else if (info.GetDamageType() == DamageTypes.Lure)
		{
			result = TranslationReader.Get("Death_Causes", 2);
		}
		else if (info.GetDamageType() == DamageTypes.Pocket)
		{
			result = TranslationReader.Get("Death_Causes", 3);
		}
		else if (info.GetDamageType() == DamageTypes.Contain)
		{
			result = TranslationReader.Get("Death_Causes", 4);
		}
		else if (info.GetDamageType() == DamageTypes.Tesla)
		{
			result = TranslationReader.Get("Death_Causes", 5);
		}
		else if (info.GetDamageType() == DamageTypes.Wall)
		{
			result = TranslationReader.Get("Death_Causes", 6);
		}
		else if (info.GetDamageType() == DamageTypes.Decont)
		{
			result = TranslationReader.Get("Death_Causes", 15);
		}
		else if (info.GetDamageType() == DamageTypes.Grenade)
		{
			result = TranslationReader.Get("Death_Causes", 16);
		}
		else if (info.GetDamageType().isWeapon && info.GetDamageType().weaponId != -1)
		{
			GameObject gameObject = GameObject.Find("Host");
			AmmoBox component = gameObject.GetComponent<AmmoBox>();
			WeaponManager component2 = gameObject.GetComponent<WeaponManager>();
			result = TranslationReader.Get("Death_Causes", 7).Replace("[ammotype]", component.types[component2.weapons[info.GetDamageType().weaponId].ammoType].label);
		}
		else if (info.GetDamageType().isScp)
		{
			if (info.GetDamageType() == DamageTypes.Scp173)
			{
				result = TranslationReader.Get("Death_Causes", 8);
			}
			else if (info.GetDamageType() == DamageTypes.Scp106)
			{
				result = TranslationReader.Get("Death_Causes", 9);
			}
			else if (info.GetDamageType() == DamageTypes.Scp096)
			{
				result = TranslationReader.Get("Death_Causes", 13);
			}
			else if (info.GetDamageType() == DamageTypes.Scp049 || info.GetDamageType() == DamageTypes.Scp0492)
			{
				result = TranslationReader.Get("Death_Causes", 10);
			}
			else if (info.GetDamageType() == DamageTypes.Scp939)
			{
				result = TranslationReader.Get("Death_Causes", 14);
			}
		}
		else if (info.attacker.StartsWith("*"))
		{
			return info.attacker.Substring(1);
		}
		return result;
	}
}
