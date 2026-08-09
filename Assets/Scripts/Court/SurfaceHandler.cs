using UnityEngine;

[System.Serializable]
public struct SurfaceData {
    public Vector3 Center;
    public Vector3 Size;
    public Vector3 Forward;
    public Quaternion Rotation;
    public Matrix4x4 GetMatrix() => Matrix4x4.TRS(Center, Rotation, Size); 
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
        ScalingTransform.localScale = surface.Size;
    }

    public void ParentAndPlace(Transform tf) {
        tf.parent = transform;
        tf.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
    }

    private void OnDrawGizmos() {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(ScalingTransform.TransformPoint(-0.5f, 0f, 0f), 2f);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(ScalingTransform.TransformPoint(0f, 0f, -0.5f), 2f);
    }
    public Pose TransformPose(Pose pose) {
        var surfaceMatrix = Surface.GetMatrix();
        var surfacePosition = surfaceMatrix.MultiplyPoint(pose.position);
        var surfaceRotation = surfaceMatrix.rotation * pose.rotation;
        return new Pose(surfacePosition, surfaceRotation);
    }
}
