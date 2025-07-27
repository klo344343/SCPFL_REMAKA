using System.Collections.Generic;
using System.Linq;
using MEC;
using Mirror;
using RemoteAdmin;
using Unity;
using UnityEngine;
using UnityEngine.Networking;

public class PlayerPositionManager : NetworkBehaviour
{
	public static PlayerPositionManager singleton;

	private bool isReadyToWork;

	private bool _enableAntiPlayerWallhack;

	private PlayerPositionData[] receivedData;

	private CharacterClassManager myCCM;

	private static int kTargetRpcTargetTransmit;

	private void Start()
	{
		_enableAntiPlayerWallhack = ConfigFile.ServerConfig.GetBool("anti_player_wallhack");
		Timing.RunCoroutine(_Start(), Segment.Update);
	}

	private IEnumerator<float> _Start()
	{
		singleton = this;
		if (!ServerStatic.IsDedicated)
		{
			while (PlayerManager.localPlayer == null)
			{
				yield return 0f;
			}
			CharacterClassManager local_ccm = PlayerManager.localPlayer.GetComponent<CharacterClassManager>();
			while (local_ccm.curClass < 0)
			{
				yield return 0f;
			}
		}
		isReadyToWork = true;
	}

	public static void StaticReceiveData(PlayerPositionData[] data)
	{
		singleton.ReceiveData(data);
	}

	public void ReceiveData(PlayerPositionData[] data)
	{
		receivedData = data;
	}

	private void FixedUpdate()
	{
		ReceiveData();
		if (NetworkServer.active)
		{
			TransmitData();
		}
	}

	[ServerCallback]
	private void TransmitData()
	{
		if (!NetworkServer.active)
		{
			return;
		}
		List<PlayerPositionData> list = new List<PlayerPositionData>();
		List<GameObject> list2 = PlayerManager.singleton.players.ToList();
		foreach (GameObject item in list2)
		{
			list.Add(new PlayerPositionData(item));
		}
		receivedData = list.ToArray();
		foreach (GameObject item2 in list2)
		{
			CharacterClassManager component = item2.GetComponent<CharacterClassManager>();
			if (component.curClass >= 0 && component.klasy[component.curClass].fullName.Contains("939"))
			{
				List<PlayerPositionData> list3 = new List<PlayerPositionData>(list);
				for (int i = 0; i < list3.Count; i++)
				{
					CharacterClassManager component2 = list2[i].GetComponent<CharacterClassManager>();
					if (list3[i].position.y < 800f && component2.klasy[component2.curClass].team != Team.SCP && component2.klasy[component2.curClass].team != Team.RIP && !list2[i].GetComponent<Scp939_VisionController>().CanSee(component.GetComponent<Scp939PlayerScript>()))
					{
						list3[i] = new PlayerPositionData
						{
							position = Vector3.up * 6000f,
							rotation = 0f,
							playerID = list3[i].playerID
						};
					}
				}
				TargetTransmit(item2.GetComponent<NetworkIdentity>().connectionToClient, list3.ToArray());
			}
			else
			{
				TargetTransmit(item2.GetComponent<NetworkIdentity>().connectionToClient, list.ToArray());
			}
		}
	}

	[TargetRpc(channel = 5)]
	private void TargetTransmit(NetworkConnection conn, PlayerPositionData[] data)
	{
		receivedData = data;
	}

	private void ReceiveData()
	{
		if (!isReadyToWork)
		{
			return;
		}
		if (myCCM != null)
		{
			GameObject[] players = PlayerManager.singleton.players;
			foreach (GameObject gameObject in players)
			{
				QueryProcessor component = gameObject.GetComponent<QueryProcessor>();
				PlayerPositionData[] array = receivedData;
				for (int j = 0; j < array.Length; j++)
				{
					PlayerPositionData playerPositionData = array[j];
					if (component.PlayerId != playerPositionData.playerID)
					{
						continue;
					}
					if (!component.isLocalPlayer)
					{
						CharacterClassManager component2 = gameObject.GetComponent<CharacterClassManager>();
						if (Vector3.Distance(gameObject.transform.position, playerPositionData.position) < 10f && myCCM.curClass >= 0 && (component2.curClass != 0 || !myCCM.IsHuman()))
						{
							gameObject.transform.position = Vector3.Lerp(gameObject.transform.position, playerPositionData.position, 0.2f);
							SetRotation(component2, Quaternion.Lerp(Quaternion.Euler(gameObject.transform.rotation.eulerAngles), Quaternion.Euler(Vector3.up * playerPositionData.rotation), 0.3f));
						}
						else
						{
							gameObject.transform.position = playerPositionData.position;
							SetRotation(component2, Quaternion.Euler(0f, playerPositionData.rotation, 0f));
						}
					}
					if (!NetworkServer.active)
					{
						var hrl = gameObject.GetComponent<PlyMovementSync>();
                        hrl.SetPosition(playerPositionData.position);
						hrl.SetRotation(playerPositionData.rotation);

                    }
					break;
				}
			}
		}
		else
		{
			myCCM = PlayerManager.localPlayer.GetComponent<CharacterClassManager>();
		}
	}

	private void SetRotation(CharacterClassManager target, Quaternion quat)
	{
		if (target.curClass != 0 || !myCCM.IsHuman() || Scp173PlayerScript.isBlinking)
		{
			target.transform.rotation = quat;
		}
	}
}
