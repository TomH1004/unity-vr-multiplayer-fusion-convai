using Fusion;
using UnityEngine;
using UnityEngine.XR;
using VRMultiplayer.Avatar;
using VRMultiplayer.VR;

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
        [SerializeField] private string defaultAvatarUrl = "";
        
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private float rotateSpeed = 90f;
        [SerializeField] private bool enableTeleport = true;
        
        [Header("Network Sync")]
        [SerializeField] private float positionLerpRate = 15f;
        [SerializeField] private float rotationLerpRate = 15f;

        [Header("Logging Settings")]
        [SerializeField] private bool logInputData = false;
        [SerializeField] private bool logRemotePlayerUpdates = false;
        [SerializeField] private bool logHandleVRInput = false; // Added for HandleVRInput verbosity control
        
        // Networked properties
        [Networked] public Vector3 NetworkPosition { get; set; }
        [Networked] public Quaternion NetworkRotation { get; set; }
        [Networked] public Vector3 HeadPosition { get; set; }
        [Networked] public Quaternion HeadRotation { get; set; }
        [Networked] public Vector3 LeftHandPosition { get; set; }
        [Networked] public Quaternion LeftHandRotation { get; set; }
        [Networked] public Vector3 RightHandPosition { get; set; }
        [Networked] public Quaternion RightHandRotation { get; set; }
        [Networked(OnChanged = nameof(OnAvatarUrlChanged))] public string AvatarUrl { get; set; }
        [Networked] public bool IsVRUser { get; set; }
        
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
        
        public override void Spawned()
        {
            isLocalPlayer = Object.HasInputAuthority; // Set this first
            Debug.Log($"[NetworkVRPlayer] Spawned for player {Object.InputAuthority}. IsLocalPlayer: {isLocalPlayer}");
            
            if (isLocalPlayer)
            {
                SetupLocalVRPlayer();
            }
            else
            {
                SetupRemotePlayer();
            }
            
            // Initialize avatar
            if (avatarController != null)
            {
                avatarController.Initialize(this);

                // If AvatarUrl is already set (e.g. by network for late joiner, or by host quickly)
                // use it. Otherwise, use the default. The OnAvatarUrlChanged callback will also fire
                // if AvatarUrl is set by the network after this point or changes later.
                string initialUrlToLoad = AvatarUrl;
                if (string.IsNullOrEmpty(initialUrlToLoad) && !string.IsNullOrEmpty(defaultAvatarUrl))
                {
                    initialUrlToLoad = defaultAvatarUrl;
                    // Optionally, if we want the default to be authoritative if nothing else is set:
                    if (Object.HasInputAuthority) AvatarUrl = defaultAvatarUrl;
                }

                if (!string.IsNullOrEmpty(initialUrlToLoad))
                {
                    Debug.Log($"[NetworkVRPlayer:{Object.InputAuthority}] Initializing avatar. URL to load: '{initialUrlToLoad}'");
                    avatarController.LoadAvatar(initialUrlToLoad);
                }
                else
                {
                    Debug.Log($"[NetworkVRPlayer:{Object.InputAuthority}] Initializing avatar. No specific URL to load initially (AvatarUrl and defaultAvatarUrl are empty or AvatarUrl was already loaded by OnChanged).");
                }
            }
            
            IsVRUser = Application.platform != RuntimePlatform.WindowsEditor;
        }

        public static void OnAvatarUrlChanged(Changed<NetworkVRPlayer> changed)
        {
            // The property changed.Behaviour.AvatarUrl already holds the new value.
            string newUrl = changed.Behaviour.AvatarUrl;

            // To get the old value, we use LoadOld(), which temporarily sets the property to the old value.
            changed.LoadOld();
            string oldUrl = changed.Behaviour.AvatarUrl;
            // After this call, changed.Behaviour.AvatarUrl reverts to the new value if accessed again without LoadOld().

            Debug.Log($"[NetworkVRPlayer:{changed.Behaviour.Object.InputAuthority}] AvatarUrl changed via network. Old: '{oldUrl}', New: '{newUrl}'. Loading new avatar.");
            if (changed.Behaviour.avatarController != null)
            {
                // Ensure we're calling LoadAvatar with the definitive new URL.
                changed.Behaviour.avatarController.LoadAvatar(newUrl);
            }
        }
        
        private void SetupLocalVRPlayer()
        {
            Debug.Log("Setting up local VR player");
            
            // Find XR Origin if not assigned
            if (xrOrigin == null)
            {
                var xrOriginObj = FindObjectOfType<Unity.XR.CoreUtils.XROrigin>();
                if (xrOriginObj != null)
                {
                    xrOrigin = xrOriginObj.transform;
                }
            }
            
            // Get VR camera
            vrCamera = Camera.main;
            if (vrCamera == null)
            {
                vrCamera = FindObjectOfType<Camera>();
            }
            // Assign headTransform after vrCamera is found
            if (vrCamera != null)
            {
                headTransform = vrCamera.transform;
            }
            
            // Setup hand controllers
            SetupHandControllers();
            
            // Setup locomotion
            locomotion = GetComponent<VRLocomotion>();
            if (locomotion == null)
            {
                locomotion = gameObject.AddComponent<VRLocomotion>();
            }
            locomotion.Initialize(this);
            
            // Position the network player at XR Origin
            if (xrOrigin != null)
            {
                transform.position = xrOrigin.position;
                transform.rotation = xrOrigin.rotation;
            }
        }
        
        private void SetupRemotePlayer()
        {
            Debug.Log("Setting up remote player");
            
            // Disable local XR components for remote players
            if (vrCamera != null && vrCamera.gameObject != gameObject)
            {
                // Don't disable the main camera, just this player's camera
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
            // Find or create hand controllers
            var handControllers = GetComponentsInChildren<VRHandController>();
            
            foreach (var controller in handControllers)
            {
                if (controller.IsLeftHand)
                {
                    leftHandController = controller;
                    leftHandTransform = controller.transform;
                }
                else
                {
                    rightHandController = controller;
                    rightHandTransform = controller.transform;
                }
            }
            
            // If hand controllers don't exist, create them
            if (leftHandController == null)
            {
                var leftHandObj = new GameObject("LeftHand");
                leftHandObj.transform.SetParent(transform);
                leftHandController = leftHandObj.AddComponent<VRHandController>();
                leftHandController.SetHandType(true); // true for left hand
                leftHandTransform = leftHandObj.transform;
            }
            
            if (rightHandController == null)
            {
                var rightHandObj = new GameObject("RightHand");
                rightHandObj.transform.SetParent(transform);
                rightHandController = rightHandObj.AddComponent<VRHandController>();
                rightHandController.SetHandType(false); // false for right hand
                rightHandTransform = rightHandObj.transform;
            }
        }
        
        public override void FixedUpdateNetwork()
        {
            // Handle input for local player
            if (Object.HasInputAuthority)
            {
                if (GetInput<VRNetworkInputData>(out var input))
                {
                    HandleMovement(input);
                    HandleVRInput(input);
                }
            }
        }
        
        private void HandleMovement(VRNetworkInputData input)
        {
            if (locomotion != null)
            {
                locomotion.ProcessMovementInput(input.moveInput, input.turnInput);
            }
            
            // Update networked position and rotation
            NetworkPosition = transform.position;
            NetworkRotation = transform.rotation;
        }
        
        private void HandleVRInput(VRNetworkInputData input)
        {
            if (logHandleVRInput)
            {
                Debug.Log($"[NetworkVRPlayer:{Object.InputAuthority}] Applying VR Input: HeadRot={input.headRotation.eulerAngles}, LeftHandRot={input.leftHandRotation.eulerAngles}, RightTrigger={input.rightTrigger}");
            }
            // Update networked VR transforms
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
        
        public override void Render()
        {
            // Interpolate position for smooth movement
            if (!Object.HasInputAuthority)
            {
                transform.position = Vector3.Lerp(transform.position, NetworkPosition, Time.deltaTime * positionLerpRate);
                transform.rotation = Quaternion.Lerp(transform.rotation, NetworkRotation, Time.deltaTime * rotationLerpRate);
            }
            
            // Update VR transforms for all players
            UpdateVRTransforms();
        }
        
        private void UpdateVRTransforms()
        {
            if (!Object.HasInputAuthority && logRemotePlayerUpdates)
            {
                Debug.Log($"[NetworkVRPlayer:{Object.InputAuthority}] Remote player updating transforms. Current Pos: {transform.position}, Target NetPos: {NetworkPosition}, Current HeadPos: {headTransform?.position}, Target HeadPos: {transform.TransformPoint(HeadPosition)}");
            }

            if (headTransform != null)
            {
                headTransform.position = Vector3.Lerp(headTransform.position, transform.TransformPoint(HeadPosition), Time.deltaTime * positionLerpRate);
                headTransform.rotation = Quaternion.Lerp(headTransform.rotation, transform.rotation * HeadRotation, Time.deltaTime * rotationLerpRate);
            }
            
            if (leftHandTransform != null)
            {
                leftHandTransform.position = Vector3.Lerp(leftHandTransform.position, transform.TransformPoint(LeftHandPosition), Time.deltaTime * positionLerpRate);
                leftHandTransform.rotation = Quaternion.Lerp(leftHandTransform.rotation, transform.rotation * LeftHandRotation, Time.deltaTime * rotationLerpRate);
            }
            
            if (rightHandTransform != null)
            {
                rightHandTransform.position = Vector3.Lerp(rightHandTransform.position, transform.TransformPoint(RightHandPosition), Time.deltaTime * positionLerpRate);
                rightHandTransform.rotation = Quaternion.Lerp(rightHandTransform.rotation, transform.rotation * RightHandRotation, Time.deltaTime * rotationLerpRate);
            }
        }
        
        // Input gathering for local player
        public VRNetworkInputData GatherInput()
        {
            VRNetworkInputData currentInputData = new VRNetworkInputData();
            
            // Get VR head tracking
            if (vrCamera != null)
            {
                currentInputData.headPosition = transform.InverseTransformPoint(vrCamera.transform.position);
                currentInputData.headRotation = Quaternion.Inverse(transform.rotation) * vrCamera.transform.rotation;
            }
            
            // Get hand tracking
            if (leftHandController != null)
            {
                currentInputData.leftHandPosition = transform.InverseTransformPoint(leftHandController.transform.position);
                currentInputData.leftHandRotation = Quaternion.Inverse(transform.rotation) * leftHandController.transform.rotation;
                leftHandController.GetInputState(out currentInputData.leftTrigger, out currentInputData.leftGrip);
            }
            
            if (rightHandController != null)
            {
                currentInputData.rightHandPosition = transform.InverseTransformPoint(rightHandController.transform.position);
                currentInputData.rightHandRotation = Quaternion.Inverse(transform.rotation) * rightHandController.transform.rotation;
                rightHandController.GetInputState(out currentInputData.rightTrigger, out currentInputData.rightGrip);
            }
            
            // Get locomotion input
            if (locomotion != null)
            {
                locomotion.GetMovementInput(out currentInputData.moveInput, out currentInputData.turnInput);
            }
            
            // Get button inputs
            // TODO: Decide if VRButton enum from VRInputProvider should be used here or a local one.
            // For now, using CommonUsages directly.
            currentInputData.teleportButton = GetButtonInput(InputDeviceCharacteristics.LeftHand | InputDeviceCharacteristics.Controller, CommonUsages.primaryButton);
            currentInputData.menuButton = GetButtonInput(InputDeviceCharacteristics.RightHand | InputDeviceCharacteristics.Controller, CommonUsages.menuButton);

            if (logInputData)
            {
                Debug.Log($"[NetworkVRPlayer:{Object.InputAuthority}] GatherInput: HeadPos={currentInputData.headPosition}, LeftHandPos={currentInputData.leftHandPosition}, Move={currentInputData.moveInput}, TeleportBtn={currentInputData.teleportButton}");
            }

            return currentInputData;
        }
        
        private ButtonState GetButtonInput(InputDeviceCharacteristics deviceCharacteristics, InputFeatureUsage<bool> usage)
        {
            var devices = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(deviceCharacteristics, devices);
            
            bool isPressed = false;
            // Iterate in reverse to prioritize specific hand if multiple devices match (e.g. generic controller vs hand-specific)
            for (int i = devices.Count - 1; i >= 0; i--)
            {
                var device = devices[i];
                // Ensure we are checking the correct hand if characteristics specify one
                if ((deviceCharacteristics & InputDeviceCharacteristics.LeftHand) != 0 && (device.characteristics & InputDeviceCharacteristics.LeftHand) == 0) continue;
                if ((deviceCharacteristics & InputDeviceCharacteristics.RightHand) != 0 && (device.characteristics & InputDeviceCharacteristics.RightHand) == 0) continue;

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
            Debug.Log($"[NetworkVRPlayer:{Object.InputAuthority}] SetAvatarUrl called by local player. New URL: '{url}'");
            AvatarUrl = url;
            // The OnAvatarUrlChanged callback will handle loading the avatar if the new URL is different.
            // If called on a remote client, this will only update the local (non-networked) desire until the property is synced.
            // However, standard practice is for input authority to change networked props.
            // If avatarController.LoadAvatar(url) is still desired here for immediate local feedback before network confirmation:
            // if (avatarController != null && Object.HasInputAuthority) avatarController.LoadAvatar(url);
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
    }
} 