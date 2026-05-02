using Meta.XR.BuildingBlocks;
using Sirenix.OdinInspector;
using System;
using Unity.Netcode;
using UnityEngine;

public class NetworkXRPlayer : NetworkBehaviour
{
	[NonSerialized] public NetworkVariable<Vector2> NetPosOnField = 
		new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
    [NonSerialized] public NetworkVariable<Vector2> NetRotation = 
		new(readPerm:NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
	[ShowInInspector] Vector2 PosOnField => NetPosOnField.Value;
	[ShowInInspector] Vector2 Rotation => NetPosOnField.Value;
}