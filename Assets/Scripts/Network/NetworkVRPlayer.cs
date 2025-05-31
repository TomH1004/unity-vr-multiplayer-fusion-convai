using Fusion;
using UnityEngine;
using UnityEngine.XR;
using Unity.XR.CoreUtils;
using VRMultiplayer.Avatar;
using VRMultiplayer.VR;
using System.Collections.Generic;
using System.Collections;

namespace VRMultiplayer.Network
{
    /// <summary>
    /// Networked VR Player that handles VR input, movement, and avatar synchronization
    /// Uses Photon Fusion for networking in shared mode
    /// </summary>
    public class NetworkVRPlayer : NetworkBehaviour, INetworkInput
    {
        [Header("VR Components")]
        [SerializeField] private Transform xrOrigin;
        [SerializeField] private Transform headTransform;
        [SerializeField] private Transform leftHandTransform;
        [SerializeField] private Transform rightHandTransform;
        
        [Header("Avatar")]
        [SerializeField] private VRAvatarController avatarController;
        [SerializeField] private string defaultAvatarUrl = "https://models.readyplayer.me/6409cc49c2e68b002fbbbd4e.glb"; // Working test avatar
        
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private float rotateSpeed = 90f;
        [SerializeField] private bool enableTeleport = true;
        
        [Header("Network Sync")]
        [SerializeField] private float positionLerpRate = 15f;
        [SerializeField] private float rotationLerpRate = 15f;
        
        // Networked properties
        [Networked] public Vector3 NetworkPosition { get; set; }
        [Networked] public Quaternion NetworkRotation { get; set; }
        [Networked] public Vector3 HeadPosition { get; set; }
        [Networked] public Quaternion HeadRotation { get; set; }
        [Networked] public Vector3 LeftHandPosition { get; set; }
        [Networked] public Quaternion LeftHandRotation { get; set; }
        [Networked] public Vector3 RightHandPosition { get; set; }
        [Networked] public Quaternion RightHandRotation { get; set; }
        [Networked] public string AvatarUrl { get; set; }
        [Networked] public bool IsVRUser { get; set; }
        [Networked] public PlayerRef OwnerPlayer { get; set; }
        
        // Local VR input
        private VRHandController leftHandController;
        private VRHandController rightHandController;
        private VRLocomotion locomotion;
        
        // Input data structure
        public struct VRNetworkInputData : INetworkInput
        {
            public Vector3 headPosition;
            public Quaternion headRotation;
            public Vector3 leftHandPosition;
            public Quaternion leftHandRotation;
            public Vector3 rightHandPosition;
            public Quaternion rightHandRotation;
            public Vector2 moveInput;
            public Vector2 turnInput;
            public bool leftTrigger;
            public bool rightTrigger;
            public bool leftGrip;
            public bool rightGrip;
            public ButtonState teleportButton;
            public ButtonState menuButton;
        }
        
        // Local state
        private Camera vrCamera;
        private bool isLocalPlayer;
        private float lastSyncTime = 0f; // Cooldown timer for XR Origin sync
        
        public override void Spawned()
        {
            // In Shared Mode, determine ownership more reliably
            if (Runner.IsSharedModeMasterClient)
            {
                // Master client spawns all players and assigns ownership
                OwnerPlayer = Object.InputAuthority;
            }
            else
            {
                // Non-master clients: wait a bit and check ownership
                StartCoroutine(DetermineOwnershipDelayed());
                return;
            }
            
            // Determine if this is the local player
            isLocalPlayer = (Runner.LocalPlayer == Object.InputAuthority) || 
                           (OwnerPlayer != PlayerRef.None && OwnerPlayer == Runner.LocalPlayer);
            
            CompleteSpawnSetup();
        }
        
        private IEnumerator DetermineOwnershipDelayed()
        {
            // Wait a few frames for the network object to be fully synchronized
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            
            bool shouldBeLocalPlayer = false;
            
            // First check: InputAuthority matches
            if (Object.InputAuthority == Runner.LocalPlayer)
            {
                shouldBeLocalPlayer = true;
                OwnerPlayer = Runner.LocalPlayer;
            }
            // Second check: If InputAuthority is None, check if this should be our object
            else if (Object.InputAuthority == PlayerRef.None)
            {
                // Check all other NetworkVRPlayer objects to see which players already have objects
                var allNetworkVRPlayers = FindObjectsOfType<NetworkVRPlayer>();
                var playersWithObjects = new HashSet<PlayerRef>();
                
                foreach (var otherPlayer in allNetworkVRPlayers)
                {
                    if (otherPlayer != this && otherPlayer.Object != null)
                    {
                        if (otherPlayer.Object.InputAuthority != PlayerRef.None)
                        {
                            playersWithObjects.Add(otherPlayer.Object.InputAuthority);
                        }
                        else if (otherPlayer.OwnerPlayer != PlayerRef.None)
                        {
                            playersWithObjects.Add(otherPlayer.OwnerPlayer);
                        }
                    }
                }
                
                // If our LocalPlayer doesn't have an object yet, this might be ours
                if (!playersWithObjects.Contains(Runner.LocalPlayer))
                {
                    shouldBeLocalPlayer = true;
                    OwnerPlayer = Runner.LocalPlayer;
                }
            }
            // Third check: OwnerPlayer already set and matches LocalPlayer
            else if (OwnerPlayer == Runner.LocalPlayer)
            {
                shouldBeLocalPlayer = true;
            }
            
            isLocalPlayer = shouldBeLocalPlayer;
            CompleteSpawnSetup();
        }
        
        private void CompleteSpawnSetup()
        {
            // Initialize VR quaternions to prevent zero values
            if (HeadRotation.x == 0 && HeadRotation.y == 0 && HeadRotation.z == 0 && HeadRotation.w == 0)
                HeadRotation = Quaternion.identity;
            if (LeftHandRotation.x == 0 && LeftHandRotation.y == 0 && LeftHandRotation.z == 0 && LeftHandRotation.w == 0)
                LeftHandRotation = Quaternion.identity;
            if (RightHandRotation.x == 0 && RightHandRotation.y == 0 && RightHandRotation.z == 0 && RightHandRotation.w == 0)
                RightHandRotation = Quaternion.identity;
            
            // Initialize network position to current transform position to prevent resets
            if (NetworkPosition == Vector3.zero)
            {
                NetworkPosition = transform.position;
            }
            if (NetworkRotation.x == 0 && NetworkRotation.y == 0 && NetworkRotation.z == 0 && NetworkRotation.w == 0)
            {
                NetworkRotation = transform.rotation;
            }
            
            if (isLocalPlayer)
            {
                SetupLocalVRPlayer();
            }
            else
            {
                SetupRemotePlayer();
            }
            
            // Load avatar with delay to ensure all components are ready
            StartCoroutine(InitializeAvatarControllerDelayed());
        }
        
        private void SetupLocalVRPlayer()
        {
            // Find XR Origin if not assigned
            if (xrOrigin == null)
            {
                var xrOriginObj = FindObjectOfType<XROrigin>();
                if (xrOriginObj != null)
                {
                    xrOrigin = xrOriginObj.transform;
                }
                else
                {
                    // Alternative search methods
                    var xrOriginGO = GameObject.Find("XR Origin (XR Rig)");
                    if (xrOriginGO == null) xrOriginGO = GameObject.Find("XR Rig");
                    if (xrOriginGO == null) xrOriginGO = GameObject.Find("XROrigin");
                    if (xrOriginGO == null) xrOriginGO = GameObject.Find("XR Origin");
                    
                    if (xrOriginGO != null)
                    {
                        xrOrigin = xrOriginGO.transform;
                        xrOriginObj = xrOriginGO.GetComponent<XROrigin>();
                    }
                    else
                    {
                        Debug.LogError($"[NetworkVRPlayer] Could not find XR Origin! VR tracking will not work for Player {Runner.LocalPlayer}");
                        return;
                    }
                }
                
                if (xrOriginObj?.Camera != null)
                {
                    vrCamera = xrOriginObj.Camera;
                }
            }
            
            // Find VR camera if still not found
            if (vrCamera == null)
            {
                var cameraNames = new[] { "Main Camera", "CenterEyeAnchor", "Camera", "Head Camera" };
                foreach (var cameraName in cameraNames)
                {
                    var cameraGO = GameObject.Find(cameraName);
                    if (cameraGO != null)
                    {
                        var camera = cameraGO.GetComponent<Camera>();
                        if (camera != null)
                        {
                            vrCamera = camera;
                            break;
                        }
                    }
                }
                
                // Last resort: find any camera
                if (vrCamera == null)
                {
                    vrCamera = Camera.main ?? FindObjectOfType<Camera>();
                    
                    if (vrCamera == null)
                    {
                        Debug.LogError($"[NetworkVRPlayer] Could not find any camera! VR head tracking will not work for Player {Runner.LocalPlayer}");
                        return;
                    }
                }
            }
            
            // Verify we have the essential components before continuing
            if (xrOrigin == null)
            {
                Debug.LogError($"[NetworkVRPlayer] XR Origin is null - cannot set up VR player {Runner.LocalPlayer}!");
                return;
            }
            
            if (vrCamera == null)
            {
                Debug.LogError($"[NetworkVRPlayer] VR Camera is null - cannot set up VR player {Runner.LocalPlayer}!");
                return;
            }
            
            // IMPORTANT: Ensure NetworkVRPlayer is NOT a child of XR Origin to prevent feedback loops
            if (transform.IsChildOf(xrOrigin))
            {
                Debug.LogWarning($"[NetworkVRPlayer] NetworkVRPlayer is a child of XR Origin! Moving to scene root to prevent feedback loops.");
                transform.SetParent(null);
            }
            
            // Also ensure XR Origin is not a child of NetworkVRPlayer
            if (xrOrigin.IsChildOf(transform))
            {
                Debug.LogError($"[NetworkVRPlayer] XR Origin is a child of NetworkVRPlayer! This will cause major issues. Fix the hierarchy!");
                return;
            }
            
            // Find the head transform
            if (headTransform == null)
            {
                var headNames = new[] { "Camera Offset", "CameraOffset", "Head", "CenterEyeAnchor", "Main Camera" };
                foreach (var headName in headNames)
                {
                    var headChild = xrOrigin.Find(headName);
                    if (headChild != null)
                    {
                        var camera = headChild.GetComponentInChildren<Camera>();
                        if (camera != null)
                        {
                            headTransform = camera.transform;
                            if (vrCamera == null) vrCamera = camera;
                            break;
                        }
                    }
                }
                
                // If still not found, use the camera's transform directly
                if (headTransform == null && vrCamera != null)
                {
                    headTransform = vrCamera.transform;
                }
                
                if (headTransform == null)
                {
                    Debug.LogError($"[NetworkVRPlayer] Could not find head transform for Player {Runner.LocalPlayer}!");
                    return;
                }
            }
            
            // Setup hand controllers and find hand transforms
            SetupHandControllers();
            
            // Setup locomotion
            locomotion = GetComponent<VRLocomotion>();
            if (locomotion == null)
            {
                locomotion = gameObject.AddComponent<VRLocomotion>();
            }
            locomotion.Initialize(this);
            
            // Position the network player at XR Origin but don't move XR Origin
            Vector3 originalXROriginPos = xrOrigin.position;
            Quaternion originalXROriginRot = xrOrigin.rotation;
            
            transform.position = originalXROriginPos;
            transform.rotation = originalXROriginRot;
            
            // Ensure XR Origin stays in its original position (prevent feedback)
            if (Vector3.Distance(xrOrigin.position, originalXROriginPos) > 0.001f)
            {
                Debug.LogWarning($"[NetworkVRPlayer] XR Origin moved unexpectedly, restoring position for Player {Runner.LocalPlayer}");
                xrOrigin.position = originalXROriginPos;
                xrOrigin.rotation = originalXROriginRot;
            }
            
            // Force assignment of serialized fields for inspector display
            ForceInspectorUpdate();
        }
        
        // Method to force inspector field updates
        private void ForceInspectorUpdate()
        {
            if (vrCamera != null && headTransform == null)
            {
                headTransform = vrCamera.transform;
            }
            
            // Unity inspector update
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }
        
        private void SetupRemotePlayer()
        {
            // Disable local XR components for remote players
            if (vrCamera != null && vrCamera.gameObject != gameObject)
            {
                var playerCamera = GetComponentInChildren<Camera>();
                if (playerCamera != null)
                {
                    playerCamera.enabled = false;
                }
            }
            
            // Remote players don't need input controllers
            var xrControllers = GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.XRController>();
            foreach (var controller in xrControllers)
            {
                controller.enabled = false;
            }
        }
        
        private void SetupHandControllers()
        {
            // First, try to find existing hand controllers in the XR Origin hierarchy
            if (xrOrigin != null)
            {
                var leftHandNames = new[] { "LeftHand Controller", "Left Controller", "LeftHandAnchor", "LeftHand", "Left Hand" };
                var rightHandNames = new[] { "RightHand Controller", "Right Controller", "RightHandAnchor", "RightHand", "Right Hand" };
                
                // Search for left hand controller
                foreach (var handName in leftHandNames)
                {
                    var handObj = xrOrigin.Find(handName) ?? FindChildRecursive(xrOrigin, handName);
                    
                    if (handObj != null)
                    {
                        leftHandTransform = handObj;
                        leftHandController = handObj.GetComponent<VRHandController>();
                        if (leftHandController == null)
                        {
                            leftHandController = handObj.gameObject.AddComponent<VRHandController>();
                            leftHandController.SetHandType(true); // true for left hand
                        }
                        break;
                    }
                }
                
                // Search for right hand controller
                foreach (var handName in rightHandNames)
                {
                    var handObj = xrOrigin.Find(handName) ?? FindChildRecursive(xrOrigin, handName);
                    
                    if (handObj != null)
                    {
                        rightHandTransform = handObj;
                        rightHandController = handObj.GetComponent<VRHandController>();
                        if (rightHandController == null)
                        {
                            rightHandController = handObj.gameObject.AddComponent<VRHandController>();
                            rightHandController.SetHandType(false); // false for right hand
                        }
                        break;
                    }
                }
            }
            
            // If we still don't have hand controllers, look for any existing VRHandController components
            if (leftHandController == null || rightHandController == null)
            {
                var handControllers = FindObjectsOfType<VRHandController>();
                
                foreach (var controller in handControllers)
                {
                    if (controller.IsLeftHand && leftHandController == null)
                    {
                        leftHandController = controller;
                        leftHandTransform = controller.transform;
                    }
                    else if (!controller.IsLeftHand && rightHandController == null)
                    {
                        rightHandController = controller;
                        rightHandTransform = controller.transform;
                    }
                }
            }
            
            // Create hand controllers if they still don't exist
            if (leftHandController == null)
            {
                GameObject leftHandObj = new GameObject("LeftHand");
                leftHandObj.transform.SetParent(xrOrigin ?? transform);
                leftHandController = leftHandObj.AddComponent<VRHandController>();
                leftHandController.SetHandType(true);
                leftHandTransform = leftHandObj.transform;
            }
            
            if (rightHandController == null)
            {
                GameObject rightHandObj = new GameObject("RightHand");
                rightHandObj.transform.SetParent(xrOrigin ?? transform);
                rightHandController = rightHandObj.AddComponent<VRHandController>();
                rightHandController.SetHandType(false);
                rightHandTransform = rightHandObj.transform;
            }
        }
        
        // Helper method to find child objects recursively
        private Transform FindChildRecursive(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name.Contains(childName) || child.name.Equals(childName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }
                
                var result = FindChildRecursive(child, childName);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }
        
        public override void FixedUpdateNetwork()
        {
            // Handle input for local player - use isLocalPlayer instead of Object.HasInputAuthority
            // because in Shared Mode, InputAuthority might be None for some players
            if (isLocalPlayer)
            {
                if (GetInput<VRNetworkInputData>(out var input))
                {
                    HandleMovement(input);
                    HandleVRInput(input);
                }
                
                // Always try to sync with XR Origin for local players
                SyncWithXROriginSafely();
            }
        }
        
        private void HandleMovement(VRNetworkInputData input)
        {
            if (locomotion != null)
            {
                locomotion.ProcessMovementInput(input.moveInput, input.turnInput);
            }
            
            // Only update networked position and rotation if they've actually changed to prevent network spam
            Vector3 currentPos = transform.position;
            Quaternion currentRot = transform.rotation;
            
            if (Vector3.Distance(NetworkPosition, currentPos) > 0.01f)
            {
                NetworkPosition = currentPos;
            }
            
            if (Quaternion.Angle(NetworkRotation, currentRot) > 0.1f)
            {
                NetworkRotation = currentRot;
            }
        }
        
        private void HandleVRInput(VRNetworkInputData input)
        {
            // Update networked VR transforms (these are relative to the NetworkVRPlayer)
            HeadPosition = input.headPosition;
            HeadRotation = input.headRotation;
            LeftHandPosition = input.leftHandPosition;
            LeftHandRotation = input.leftHandRotation;
            RightHandPosition = input.rightHandPosition;
            RightHandRotation = input.rightHandRotation;
            
            // Handle hand interactions
            if (leftHandController != null)
            {
                leftHandController.SetInputState(input.leftTrigger, input.leftGrip);
            }
            
            if (rightHandController != null)
            {
                rightHandController.SetInputState(input.rightTrigger, input.rightGrip);
            }
        }
        
        // Method to safely sync NetworkVRPlayer with XR Origin without feedback loops
        private void SyncWithXROriginSafely()
        {
            if (!isLocalPlayer || xrOrigin == null) return;
            
            // Add a cooldown to prevent rapid updates that cause jiggling
            const float syncCooldown = 0.1f; // 100ms minimum between syncs
            
            if (Time.fixedTime - lastSyncTime < syncCooldown) return;
            
            // Calculate position difference
            float positionDiff = Vector3.Distance(transform.position, xrOrigin.position);
            
            // Use a larger threshold to prevent micro-movements that cause jitter
            if (positionDiff > 0.1f) // 10cm threshold
            {
                // Store the current XR Origin state before any changes
                Vector3 xrOriginalPos = xrOrigin.position;
                Quaternion xrOriginalRot = xrOrigin.rotation;
                
                // Update NetworkVRPlayer position
                transform.position = xrOriginalPos;
                
                // Immediately verify XR Origin didn't move (indicating feedback loop)
                if (Vector3.Distance(xrOrigin.position, xrOriginalPos) > 0.01f)
                {
                    Debug.LogWarning($"[NetworkVRPlayer] XR Origin moved during sync! Possible feedback loop detected for Player {Runner.LocalPlayer}.");
                    
                    // Restore XR Origin position
                    xrOrigin.position = xrOriginalPos;
                    xrOrigin.rotation = xrOriginalRot;
                    
                    // Check for problematic parent-child relationships
                    CheckForHierarchyIssues();
                }
                
                // Update networked position and sync time
                NetworkPosition = transform.position;
                lastSyncTime = Time.fixedTime;
            }
        }
        
        // Method to check for hierarchy issues that could cause feedback loops
        private void CheckForHierarchyIssues()
        {
            // Check if NetworkVRPlayer became a child of XR Origin somehow
            if (transform.IsChildOf(xrOrigin))
            {
                Debug.LogError($"[NetworkVRPlayer] NetworkVRPlayer is a child of XR Origin! This causes feedback loops for Player {Runner.LocalPlayer}.");
                transform.SetParent(null);
            }
            
            // Check if XR Origin became a child of NetworkVRPlayer
            if (xrOrigin.IsChildOf(transform))
            {
                Debug.LogError($"[NetworkVRPlayer] XR Origin is a child of NetworkVRPlayer! This causes feedback loops for Player {Runner.LocalPlayer}.");
            }
            
            // Check for NetworkTransform components that might interfere
            var networkTransforms = GetComponents<NetworkTransform>();
            if (networkTransforms.Length > 0)
            {
                Debug.LogWarning($"[NetworkVRPlayer] Found {networkTransforms.Length} NetworkTransform component(s) that might interfere with manual position syncing for Player {Runner.LocalPlayer}.");
                
                // Disable NetworkTransform components to prevent conflicts
                foreach (var nt in networkTransforms)
                {
                    nt.enabled = false;
                }
            }
            
            // Check XR Origin for conflicting components
            CheckXROriginForConflicts();
        }
        
        // Method to check XR Origin for components that might cause position conflicts
        private void CheckXROriginForConflicts()
        {
            if (xrOrigin == null) return;
            
            // Check for NetworkTransform on XR Origin
            var xrNetworkTransforms = xrOrigin.GetComponents<NetworkTransform>();
            if (xrNetworkTransforms.Length > 0)
            {
                Debug.LogWarning($"[NetworkVRPlayer] Found {xrNetworkTransforms.Length} NetworkTransform component(s) on XR Origin! This will cause position conflicts for Player {Runner.LocalPlayer}.");
                
                foreach (var nt in xrNetworkTransforms)
                {
                    nt.enabled = false;
                }
            }
            
            // Check for other network components
            var networkBehaviours = xrOrigin.GetComponents<NetworkBehaviour>();
            if (networkBehaviours.Length > 0)
            {
                Debug.LogWarning($"[NetworkVRPlayer] Found {networkBehaviours.Length} NetworkBehaviour component(s) on XR Origin that might cause networking conflicts for Player {Runner.LocalPlayer}.");
            }
        }
        
        public override void Render()
        {
            // Interpolate position for smooth movement
            if (!Object.HasInputAuthority)
            {
                transform.position = Vector3.Lerp(transform.position, NetworkPosition, Time.deltaTime * positionLerpRate);
                
                // Validate NetworkRotation before lerping to prevent Unity assertion errors
                if (IsValidQuaternion(NetworkRotation) && IsValidQuaternion(transform.rotation))
                {
                    transform.rotation = Quaternion.Lerp(transform.rotation, NetworkRotation, Time.deltaTime * rotationLerpRate);
                }
                else if (IsValidQuaternion(NetworkRotation))
                {
                    // If current rotation is invalid but network rotation is valid, use network rotation directly
                    transform.rotation = NetworkRotation;
                }
                // If NetworkRotation is invalid, keep current rotation
            }
            
            // Update VR transforms for all players
            UpdateVRTransforms();
        }
        
        private void UpdateVRTransforms()
        {
            if (headTransform != null)
            {
                headTransform.position = Vector3.Lerp(headTransform.position, transform.TransformPoint(HeadPosition), Time.deltaTime * positionLerpRate);
                
                // Validate quaternion before lerping
                Quaternion targetHeadRotation = transform.rotation * HeadRotation;
                if (IsValidQuaternion(HeadRotation) && IsValidQuaternion(targetHeadRotation))
                {
                    headTransform.rotation = Quaternion.Lerp(headTransform.rotation, targetHeadRotation, Time.deltaTime * rotationLerpRate);
                }
                else
                {
                    headTransform.rotation = targetHeadRotation;
                }
            }
            
            if (leftHandTransform != null)
            {
                leftHandTransform.position = Vector3.Lerp(leftHandTransform.position, transform.TransformPoint(LeftHandPosition), Time.deltaTime * positionLerpRate);
                
                // Validate quaternion before lerping
                Quaternion targetLeftRotation = transform.rotation * LeftHandRotation;
                if (IsValidQuaternion(LeftHandRotation) && IsValidQuaternion(targetLeftRotation))
                {
                    leftHandTransform.rotation = Quaternion.Lerp(leftHandTransform.rotation, targetLeftRotation, Time.deltaTime * rotationLerpRate);
                }
                else
                {
                    leftHandTransform.rotation = targetLeftRotation;
                }
            }
            
            if (rightHandTransform != null)
            {
                rightHandTransform.position = Vector3.Lerp(rightHandTransform.position, transform.TransformPoint(RightHandPosition), Time.deltaTime * positionLerpRate);
                
                // Validate quaternion before lerping
                Quaternion targetRightRotation = transform.rotation * RightHandRotation;
                if (IsValidQuaternion(RightHandRotation) && IsValidQuaternion(targetRightRotation))
                {
                    rightHandTransform.rotation = Quaternion.Lerp(rightHandTransform.rotation, targetRightRotation, Time.deltaTime * rotationLerpRate);
                }
                else
                {
                    rightHandTransform.rotation = targetRightRotation;
                }
            }
        }
        
        // Helper method to validate quaternions
        private bool IsValidQuaternion(Quaternion q)
        {
            // Check if quaternion is not zero and is normalized
            float sqrMagnitude = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
            return sqrMagnitude > 0.0001f && Mathf.Abs(sqrMagnitude - 1.0f) < 0.01f;
        }
        
        // Input gathering for local player
        public void GatherInput(ref VRNetworkInputData input)
        {
            if (!isLocalPlayer) 
            {
                Debug.LogWarning($"[NetworkVRPlayer] GatherInput called on non-local player. LocalPlayer: {Runner.LocalPlayer}, OwnerPlayer: {OwnerPlayer}");
                return;
            }
            
            // Get VR head tracking
            if (vrCamera != null)
            {
                input.headPosition = transform.InverseTransformPoint(vrCamera.transform.position);
                input.headRotation = Quaternion.Inverse(transform.rotation) * vrCamera.transform.rotation;
            }
            else
            {
                Debug.LogError($"[NetworkVRPlayer] VR Camera is null for local player {Runner.LocalPlayer}!");
            }
            
            // Get hand tracking
            if (leftHandController != null)
            {
                input.leftHandPosition = transform.InverseTransformPoint(leftHandController.transform.position);
                input.leftHandRotation = Quaternion.Inverse(transform.rotation) * leftHandController.transform.rotation;
                leftHandController.GetInputState(out input.leftTrigger, out input.leftGrip);
            }
            else
            {
                Debug.LogError($"[NetworkVRPlayer] Left hand controller is null for local player {Runner.LocalPlayer}!");
            }
            
            if (rightHandController != null)
            {
                input.rightHandPosition = transform.InverseTransformPoint(rightHandController.transform.position);
                input.rightHandRotation = Quaternion.Inverse(transform.rotation) * rightHandController.transform.rotation;
                rightHandController.GetInputState(out input.rightTrigger, out input.rightGrip);
            }
            else
            {
                Debug.LogError($"[NetworkVRPlayer] Right hand controller is null for local player {Runner.LocalPlayer}!");
            }
            
            // Get locomotion input
            if (locomotion != null)
            {
                locomotion.GetMovementInput(out input.moveInput, out input.turnInput);
            }
            else
            {
                Debug.LogError($"[NetworkVRPlayer] Locomotion component is null for local player {Runner.LocalPlayer}!");
            }
            
            // Get button inputs
            input.teleportButton = GetButtonInput(InputDeviceCharacteristics.Controller, CommonUsages.primaryButton);
            input.menuButton = GetButtonInput(InputDeviceCharacteristics.Controller, CommonUsages.menuButton);
        }
        
        private ButtonState GetButtonInput(InputDeviceCharacteristics deviceCharacteristics, InputFeatureUsage<bool> usage)
        {
            var devices = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(deviceCharacteristics, devices);
            
            bool isPressed = false;
            foreach (var device in devices)
            {
                if (device.TryGetFeatureValue(usage, out bool buttonValue))
                {
                    isPressed = buttonValue;
                    break;
                }
            }
            
            return isPressed ? ButtonState.Pressed : ButtonState.Released;
        }
        
        // Public methods for avatar management
        public void SetAvatarUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                Debug.LogError($"[NetworkVRPlayer] Cannot set empty avatar URL for Player {Runner.LocalPlayer}!");
                return;
            }
            
            AvatarUrl = url;
            if (avatarController != null)
            {
                avatarController.LoadAvatar(url);
            }
            else
            {
                Debug.LogError($"[NetworkVRPlayer] Avatar controller is null, cannot load avatar for Player {Runner.LocalPlayer}!");
            }
        }
        
        public Vector3 GetHeadPosition()
        {
            return transform.TransformPoint(HeadPosition);
        }
        
        public Vector3 GetLeftHandPosition()
        {
            return transform.TransformPoint(LeftHandPosition);
        }
        
        public Vector3 GetRightHandPosition()
        {
            return transform.TransformPoint(RightHandPosition);
        }
        
        private IEnumerator InitializeAvatarControllerDelayed()
        {
            yield return null; // Wait for a frame to ensure all components are ready
            
            // Initialize avatar controller
            avatarController = GetComponent<VRAvatarController>();
            if (avatarController == null)
            {
                avatarController = gameObject.AddComponent<VRAvatarController>();
            }
            
            // Initialize avatar
            if (avatarController != null)
            {
                avatarController.Initialize(this);
                
                // Load default avatar or user's avatar
                if (!string.IsNullOrEmpty(defaultAvatarUrl))
                {
                    avatarController.LoadAvatar(defaultAvatarUrl);
                }
                else
                {
                    Debug.LogWarning($"[NetworkVRPlayer] No default avatar URL specified for Player {Runner.LocalPlayer}");
                }
            }
            else
            {
                Debug.LogError($"[NetworkVRPlayer] Failed to get or create VRAvatarController component for Player {Runner.LocalPlayer}!");
            }
        }
    }
    
    // Button state enum for VR input
    public enum ButtonState
    {
        Released,
        Pressed,
        Held
    }
} 