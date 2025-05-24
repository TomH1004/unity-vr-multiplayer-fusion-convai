using Fusion;
using UnityEngine;
using VRMultiplayer.VR;

namespace VRMultiplayer.Network
{
    /// <summary>
    /// VR Input Provider that implements INetworkInput for Photon Fusion
    /// Gathers all VR input data and provides it to the network system
    /// </summary>
    public class VRInputProvider : MonoBehaviour, INetworkInput
    {
        [Header("Input Components")]
        [SerializeField] private VRHandController leftHandController;
        [SerializeField] private VRHandController rightHandController;
        [SerializeField] private VRLocomotion locomotion;
        [SerializeField] private Transform headTransform;
        
        [Header("Input Settings")]
        [SerializeField] private bool gatherHeadTracking = true;
        [SerializeField] private bool gatherHandTracking = true;
        [SerializeField] private bool gatherLocomotionInput = true;
        [SerializeField] private bool gatherButtonInput = true;
        
        // Input data cache
        private NetworkVRPlayer.VRNetworkInputData currentInputData;
        
        // Network VR Player reference
        private NetworkVRPlayer networkPlayer;
        
        public void Initialize(NetworkVRPlayer player)
        {
            networkPlayer = player;
            
            // Auto-find components if not assigned
            FindInputComponents();
            
            Debug.Log("VRInputProvider initialized");
        }
        
        private void FindInputComponents()
        {
            // Find head transform (camera)
            if (headTransform == null)
            {
                var camera = Camera.main;
                if (camera != null)
                {
                    headTransform = camera.transform;
                }
            }
            
            // Find hand controllers
            if (leftHandController == null || rightHandController == null)
            {
                var handControllers = FindObjectsOfType<VRHandController>();
                foreach (var controller in handControllers)
                {
                    if (controller.IsLeftHand && leftHandController == null)
                    {
                        leftHandController = controller;
                    }
                    else if (!controller.IsLeftHand && rightHandController == null)
                    {
                        rightHandController = controller;
                    }
                }
            }
            
            // Find locomotion component
            if (locomotion == null)
            {
                locomotion = GetComponent<VRLocomotion>();
                if (locomotion == null)
                {
                    locomotion = FindObjectOfType<VRLocomotion>();
                }
            }
        }
        
        private void Update()
        {
            // Continuously gather input data
            GatherInputData();
        }
        
        private void GatherInputData()
        {
            // Clear previous input data
            currentInputData = new NetworkVRPlayer.VRNetworkInputData();
            
            // Gather head tracking data
            if (gatherHeadTracking && headTransform != null && networkPlayer != null)
            {
                currentInputData.headPosition = networkPlayer.transform.InverseTransformPoint(headTransform.position);
                currentInputData.headRotation = Quaternion.Inverse(networkPlayer.transform.rotation) * headTransform.rotation;
            }
            
            // Gather hand tracking data
            if (gatherHandTracking)
            {
                GatherHandTrackingData();
            }
            
            // Gather locomotion input
            if (gatherLocomotionInput && locomotion != null)
            {
                locomotion.GetMovementInput(out currentInputData.moveInput, out currentInputData.turnInput);
            }
            
            // Gather button input
            if (gatherButtonInput)
            {
                GatherButtonInput();
            }
        }
        
        private void GatherHandTrackingData()
        {
            // Left hand data
            if (leftHandController != null && networkPlayer != null)
            {
                currentInputData.leftHandPosition = networkPlayer.transform.InverseTransformPoint(leftHandController.transform.position);
                currentInputData.leftHandRotation = Quaternion.Inverse(networkPlayer.transform.rotation) * leftHandController.transform.rotation;
                
                leftHandController.GetInputState(out currentInputData.leftTrigger, out currentInputData.leftGrip);
            }
            
            // Right hand data
            if (rightHandController != null && networkPlayer != null)
            {
                currentInputData.rightHandPosition = networkPlayer.transform.InverseTransformPoint(rightHandController.transform.position);
                currentInputData.rightHandRotation = Quaternion.Inverse(networkPlayer.transform.rotation) * rightHandController.transform.rotation;
                
                rightHandController.GetInputState(out currentInputData.rightTrigger, out currentInputData.rightGrip);
            }
        }
        
        private void GatherButtonInput()
        {
            // Gather button states from controllers
            if (leftHandController != null)
            {
                // Left hand buttons could be used for different functions
                // For now, we'll use them for teleportation and menu
                currentInputData.teleportButton = leftHandController.GetButtonState(VRButton.Primary) ? 
                    ButtonState.Pressed : ButtonState.Released;
            }
            
            if (rightHandController != null)
            {
                // Right hand buttons
                currentInputData.menuButton = rightHandController.GetButtonState(VRButton.Menu) ? 
                    ButtonState.Pressed : ButtonState.Released;
            }
        }
        
        // INetworkInput implementation
        public void OnInput(NetworkRunner runner, NetworkInputData input)
        {
            if (networkPlayer == null) return;
            
            // Gather the most recent input data
            networkPlayer.GatherInput(ref currentInputData);
            
            // Set the input data for the network
            input.Set(currentInputData);
        }
        
        // Utility methods for manual input setting (useful for testing)
        public void SetHeadTracking(Vector3 position, Quaternion rotation)
        {
            currentInputData.headPosition = position;
            currentInputData.headRotation = rotation;
        }
        
        public void SetHandTracking(bool isLeftHand, Vector3 position, Quaternion rotation, bool trigger, bool grip)
        {
            if (isLeftHand)
            {
                currentInputData.leftHandPosition = position;
                currentInputData.leftHandRotation = rotation;
                currentInputData.leftTrigger = trigger;
                currentInputData.leftGrip = grip;
            }
            else
            {
                currentInputData.rightHandPosition = position;
                currentInputData.rightHandRotation = rotation;
                currentInputData.rightTrigger = trigger;
                currentInputData.rightGrip = grip;
            }
        }
        
        public void SetMovementInput(Vector2 move, Vector2 turn)
        {
            currentInputData.moveInput = move;
            currentInputData.turnInput = turn;
        }
        
        public void SetButtonState(VRInputButton button, bool pressed)
        {
            ButtonState state = pressed ? ButtonState.Pressed : ButtonState.Released;
            
            switch (button)
            {
                case VRInputButton.Teleport:
                    currentInputData.teleportButton = state;
                    break;
                case VRInputButton.Menu:
                    currentInputData.menuButton = state;
                    break;
            }
        }
        
        // Enable/disable input gathering
        public void SetInputGathering(bool head, bool hands, bool locomotion, bool buttons)
        {
            gatherHeadTracking = head;
            gatherHandTracking = hands;
            gatherLocomotionInput = locomotion;
            gatherButtonInput = buttons;
        }
        
        // Get current input data (for debugging)
        public NetworkVRPlayer.VRNetworkInputData GetCurrentInputData()
        {
            return currentInputData;
        }
        
        private void OnValidate()
        {
            // Auto-find components in editor
            if (Application.isPlaying) return;
            
            if (headTransform == null)
            {
                var camera = Camera.main;
                if (camera != null)
                {
                    headTransform = camera.transform;
                }
            }
            
            if (locomotion == null)
            {
                locomotion = GetComponent<VRLocomotion>();
            }
        }
        
        private void OnDrawGizmosSelected()
        {
            // Draw input gathering visualization
            if (!Application.isPlaying) return;
            
            // Draw head tracking
            if (gatherHeadTracking && headTransform != null)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(headTransform.position, 0.15f);
                Gizmos.DrawRay(headTransform.position, headTransform.forward * 0.5f);
            }
            
            // Draw hand tracking
            if (gatherHandTracking)
            {
                if (leftHandController != null)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireSphere(leftHandController.transform.position, 0.05f);
                }
                
                if (rightHandController != null)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawWireSphere(rightHandController.transform.position, 0.05f);
                }
            }
        }
    }
    
    public enum VRInputButton
    {
        Teleport,
        Menu,
        Primary,
        Secondary
    }
} 