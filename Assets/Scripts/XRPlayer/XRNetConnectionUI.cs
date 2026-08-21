using Unity.Netcode;
using UnityEngine;

public class XRNetConnectionUI : MonoBehaviour { 
    [SerializeField] GameObject _toggleConnected, _toggleDisconnected;
    NetworkManager _netMnger; 
    void Start()
    {
        _netMnger = NetBoot.Instance.NetMnger;
        _netMnger.OnConnectionEvent += Singleton_OnConnectionEvent;
        _netMnger.OnClientStopped += _netMnger_OnClientStopped;
        _netMnger.OnPreShutdown += _netMnger_OnPreShutdown;
        ValidateConnectionUI();
    }

    private void _netMnger_OnPreShutdown() => ValidateConnectionUI();
    private void _netMnger_OnClientStopped(bool obj) => ValidateConnectionUI();

    private void OnDestroy() {
        if (_netMnger) {
            _netMnger.OnConnectionEvent -= Singleton_OnConnectionEvent;
            _netMnger.OnClientStopped -= _netMnger_OnClientStopped;
            _netMnger.OnPreShutdown -= _netMnger_OnPreShutdown;
        }
    } 

    private void Singleton_OnConnectionEvent(NetworkManager arg1, ConnectionEventData arg2) {
        ValidateConnectionUI();
    } 

    void ValidateConnectionUI() {
        var isConnected = _netMnger.IsConnectedClient;
        _toggleConnected.SetActive(isConnected);
        _toggleDisconnected.SetActive(!isConnected);
    }
   
}
