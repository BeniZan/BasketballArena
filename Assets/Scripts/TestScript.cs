using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class TestScript : MonoBehaviour
{
    [ShowInInspector] WebCamTexture[] questTextures;

    private void Start() {
        var devices = WebCamTexture.devices;
        questTextures = new WebCamTexture[devices.Length];
        string log = "";
        for(int i=0; i < devices.Length; i++) {
            var device = devices[i];
            log += device.name + " | ";
            questTextures[i] = new WebCamTexture(device.name, 1280, 720);
            if(!questTextures[i].isPlaying)
                questTextures[i].Play();
        }
        Debug.Log(log);
    } 
}
