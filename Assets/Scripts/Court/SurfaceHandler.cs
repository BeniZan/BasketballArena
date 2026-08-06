using UnityEngine;

[System.Serializable]
public struct SurfaceData {
    public Vector3 Center;
    public Vector2 SizeXZ;
    public Vector3 Forward;
    public Quaternion Rotation;
    public Matrix4x4 GetMatrix() => Matrix4x4.TRS(Center, Rotation, SizeXZ.XZToXYZ(1f)); 
}
public class SurfaceHandler : MonoBehaviour {
    public Transform ScalingTransform;
    public SurfaceData Surface { get; private set; }

    public Transform AddTfNormalizedSurfaceToWorld(string name,
        Vector3 normalizedLocalPos, Quaternion localRot) {
        var tf = new GameObject(name).transform;
        var matrix = ScalingTransform.localToWorldMatrix;
        tf.SetPositionAndRotation(matrix.MultiplyPoint(normalizedLocalPos), matrix.rotation * localRot);
        tf.SetParent(transform);
        return tf;
    }

    public void SetSurface(SurfaceData surface) {
        Surface = surface;
        transform.SetPositionAndRotation(surface.Center, surface.Rotation);
        ScalingTransform.localScale = surface.SizeXZ;
    }

    public void ParentAndPlace(Transform tf) {
        tf.parent = transform;
        tf.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
    }
}
