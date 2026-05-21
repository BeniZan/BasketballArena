using Meta.XR.BuildingBlocks;
using Oculus.Interaction;
using Sirenix.OdinInspector;
using System;
using Unity.Netcode;
using UnityEngine;

public class XRDeviceInstance : SingletonBehaviors.SingletonMono<XRDeviceInstance> { 
	[field: SerializeField] public OVRHand LocalRightHand { get; private set; }
	[field: SerializeField] public OVRHand LocalLeftHand { get; private set; }
	[field: SerializeField] public RayInteractor RightRay { get; private set; }
	[field: SerializeField] public RayInteractor LeftRay { get; private set; }
    [field: SerializeField] public Transform CenterEyes { get; private set; }
}