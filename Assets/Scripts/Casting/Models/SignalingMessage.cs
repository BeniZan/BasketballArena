[System.Serializable]
public class SignalingMessage
{
    public string type;
    public string data;
}

// DTO ל-ICE candidate. JsonUtility לא יודע לסריאל את RTCIceCandidate (הנתונים בו ב-Properties),
// לכן מעבירים את השדות הרלוונטיים דרך המחלקה הזו.
[System.Serializable]
public class IceCandidateData
{
    public string candidate;
    public string sdpMid;
    public int sdpMLineIndex;
}