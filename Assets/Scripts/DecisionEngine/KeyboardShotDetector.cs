using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Editor/simulator stand-in for the hand pose: press Space to shoot. Inert in builds.
public class KeyboardShotDetector : MonoBehaviour, IShotDetector {
    public event Action Shot;

#if UNITY_EDITOR && ENABLE_INPUT_SYSTEM
    private void Update() {
        var kb = Keyboard.current;
        if (kb != null && kb.spaceKey.wasPressedThisFrame) Shot?.Invoke();
    }
#endif
}
