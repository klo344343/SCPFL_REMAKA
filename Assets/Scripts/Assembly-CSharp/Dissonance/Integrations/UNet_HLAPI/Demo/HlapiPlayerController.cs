using Mirror;
using UnityEngine;
using UnityEngine.Networking;

namespace Dissonance.Integrations.UNet_HLAPI.Demo
{
	public class HlapiPlayerController : NetworkBehaviour
	{
		private void Update()
		{
			if (isLocalPlayer)
			{
				CharacterController component = GetComponent<CharacterController>();
				float yAngle = Input.GetAxis("Horizontal") * Time.deltaTime * 150f;
				float num = Input.GetAxis("Vertical") * 3f;
				transform.Rotate(0f, yAngle, 0f);
				Vector3 vector = transform.TransformDirection(Vector3.forward);
				component.SimpleMove(vector * num);
				if (transform.position.y < -3f)
				{
					transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                }
			}
		}
	}
}
