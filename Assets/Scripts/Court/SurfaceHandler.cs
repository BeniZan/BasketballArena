using UnityEngine;

[System.Serializable]
public struct SurfaceData {
    public Vector3 Center;
    public Vector2 Size;
    public Vector3 Forward;
    public Quaternion Rotation;
}
public class SurfaceHandler : MonoBehaviour {
    public Transform ScalingTransform;
    public SurfaceData Surface { get; private set; }

    public void SetSurface(SurfaceData surface) {
        Surface = surface;
        transform.SetPositionAndRotation(surface.Center, surface.Rotation);
        ScalingTransform.localScale = surface.Size.XZToXYZ();
    }

    public void Place(Transform tf) {
        tf.parent = transform;
        tf.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
    }

}
