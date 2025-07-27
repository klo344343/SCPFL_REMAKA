using System;
using UnityEngine;

[Serializable]
public struct Offset
{
	public Vector3 position;

	public Vector3 rotation;

	public Vector3 scale;

	public Offset(Vector3 pos, Vector3 rot, Vector3 sca)
	{
        position = pos;
		rotation = rot;
		scale = sca;
    }
}
