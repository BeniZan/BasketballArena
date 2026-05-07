using Meta.XR.BuildingBlocks;
using Unity.Netcode;
using UnityEngine;

public class NetworkFieldData : NetworkBehaviour{
	public NetworkVariable<Vector2> FieldSize =
        new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
	[SerializeField] SharedSpatialAnchorCore _sharedAnchor;

	public Quaternion ConvertFieldRotationToWorld(Quaternion rotation) {
        //to do
        return rotation;
	}

    public Vector3 ConvertFieldPositionToWorld(Vector2 fieldPositionXZ) {
		//to do
		return fieldPositionXZ;
	}

}
