using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using System.Collections.Generic;

namespace VRMultiplayer.VR
{
    /// <summary>
    /// VR Hand Controller that manages VR input, hand tracking, and interactions
    /// Integrates with XR Interaction Toolkit for VR interactions
    /// </summary>
    public class VRHandController : MonoBehaviour
    {
        [Header("Hand Settings")]
        [SerializeField] private bool isLeftHand = true;
        [SerializeField] private Transform handModel;
        [SerializeField] private Animator handAnimator;
        
        [Header("Input Settings")]
        [SerializeField] private InputDeviceCharacteristics inputCharacteristics;
        [SerializeField] private float gripThreshold = 0.5f;
        [SerializeField] private float triggerThreshold = 0.5f;
        
        [Header("Hand Animation")]
        [SerializeField] private string gripAnimationParameter = "Grip";
        [SerializeField] private string triggerAnimationParameter = "Trigger";
        [SerializeField] private string pointAnimationParameter = "Point";
        [SerializeField] private string thumbAnimationParameter = "Thumb";
        
        [Header("Haptic Feedback")]
        [SerializeField] private bool enableHaptics = true;
        [SerializeField] private float hapticAmplitude = 0.5f;
        [SerializeField] private float hapticDuration = 0.1f;
        
        // Input device
        private InputDevice inputDevice;
        private bool deviceFound = false;
        
        // Input states
        private bool gripPressed = false;
        private bool triggerPressed = false;
        private bool primaryButtonPressed = false;
        private bool secondaryButtonPressed = false;
        private bool menuButtonPressed = false;
        private Vector2 thumbstickInput = Vector2.zero;
        private float gripValue = 0f;
        private float triggerValue = 0f;
        
        // XR Interaction components
        private UnityEngine.XR.Interaction.Toolkit.Interactors.XRDirectInteractor directInteractor;
        private UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor rayInteractor;
        
        // Hand pose data
        private Vector3 handPosition;
        private Quaternion handRotation;
        private Vector3 handVelocity;
        private Vector3 handAngularVelocity;
        
        // Events
        public System.Action<bool> OnGripStateChanged;
        public System.Action<bool> OnTriggerStateChanged;
        public System.Action<bool> OnPrimaryButtonStateChanged;
        public System.Action<bool> OnSecondaryButtonStateChanged;
        public System.Action<Vector2> OnThumbstickChanged;
        
        public bool IsLeftHand => isLeftHand;
        public bool GripPressed => gripPressed;
        public bool TriggerPressed => triggerPressed;
        public Vector3 HandPosition => handPosition;
        public Quaternion HandRotation => handRotation;
        public Vector3 HandVelocity => handVelocity;
        
        private void Start()
        {
            InitializeHand();
            SetupXRComponents();
        }
        
        private void InitializeHand()
        {
            // Set input characteristics based on hand type
            inputCharacteristics = isLeftHand ? 
                InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller :
                InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller;
                
            // Find and setup hand model
            if (handModel == null)
            {
                handModel = transform;
            }
            
            // Get hand animator
            if (handAnimator == null)
            {
                handAnimator = GetComponentInChildren<Animator>();
            }
            
            Debug.Log($"VR Hand Controller initialized for {(isLeftHand ? "Left" : "Right")} hand");
        }
        
        private void SetupXRComponents()
        {
            // Setup XR Controller
            // Note: ActionBasedController is deprecated in XR Interaction Toolkit 3.0+
            // We'll use direct InputDevice polling instead
            
            // Check if child objects already exist (e.g., in prefab)
            Transform directInteractorTransform = transform.Find("DirectInteractor");
            Transform rayInteractorTransform = transform.Find("RayInteractor");
            
            // Create or get child GameObject for Direct Interactor
            GameObject directInteractorGO;
            if (directInteractorTransform != null)
            {
                directInteractorGO = directInteractorTransform.gameObject;
            }
            else
            {
                directInteractorGO = new GameObject("DirectInteractor");
                directInteractorGO.transform.SetParent(transform);
                directInteractorGO.transform.localPosition = Vector3.zero;
                directInteractorGO.transform.localRotation = Quaternion.identity;
            }
            
            // Add sphere collider FIRST before XRDirectInteractor (required for XRDirectInteractor)
            SphereCollider directCollider = directInteractorGO.GetComponent<SphereCollider>();
            if (directCollider == null)
            {
                directCollider = directInteractorGO.AddComponent<SphereCollider>();
                directCollider.isTrigger = true;
                directCollider.radius = 0.1f; // Small radius for hand interaction
                Debug.Log($"Added trigger collider to Direct Interactor for {(isLeftHand ? "left" : "right")} hand");
            }
            
            // Add required components for Direct Interactor AFTER collider
            directInteractor = directInteractorGO.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRDirectInteractor>();
            if (directInteractor == null)
            {
                directInteractor = directInteractorGO.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRDirectInteractor>();
            }
            
            // Create or get child GameObject for Ray Interactor
            GameObject rayInteractorGO;
            if (rayInteractorTransform != null)
            {
                rayInteractorGO = rayInteractorTransform.gameObject;
            }
            else
            {
                rayInteractorGO = new GameObject("RayInteractor");
                rayInteractorGO.transform.SetParent(transform);
                rayInteractorGO.transform.localPosition = Vector3.zero;
                rayInteractorGO.transform.localRotation = Quaternion.identity;
            }
            
            // Add Ray Interactor component
            rayInteractor = rayInteractorGO.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>();
            if (rayInteractor == null)
            {
                rayInteractor = rayInteractorGO.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>();
            }
            
            // Configure ray interactor
            rayInteractor.enabled = false; // Start with direct interaction only
            
            // Setup interaction layers
            SetupInteractionLayers();
        }
        
        private void SetupInteractionLayers()
        {
            // Configure interaction layers for hand interactions
            var handLayer = LayerMask.NameToLayer("Hands");
            if (handLayer != -1)
            {
                gameObject.layer = handLayer;
            }
            
            // Set interaction layer mask
            if (directInteractor != null)
            {
                directInteractor.interactionLayers = InteractionLayerMask.GetMask("Default", "UI");
            }
            
            if (rayInteractor != null)
            {
                rayInteractor.interactionLayers = InteractionLayerMask.GetMask("Default", "UI");
            }
        }
        
        private void Update()
        {
            UpdateInputDevice();
            if (deviceFound)
            {
                UpdateInputStates();
                UpdateHandPose();
                UpdateHandAnimation();
            }
        }
        
        private void UpdateInputDevice()
        {
            if (!deviceFound)
            {
                var devices = new List<InputDevice>();
                InputDevices.GetDevicesWithCharacteristics(inputCharacteristics, devices);
                
                if (devices.Count > 0)
                {
                    inputDevice = devices[0];
                    deviceFound = true;
                    Debug.Log($"Found input device for {(isLeftHand ? "left" : "right")} hand: {inputDevice.name}");
                }
            }
        }
        
        private void UpdateInputStates()
        {
            // Update grip input
            bool newGripState = false;
            float newGripValue = 0f;
            if (inputDevice.TryGetFeatureValue(CommonUsages.grip, out newGripValue))
            {
                gripValue = newGripValue;
                newGripState = gripValue > gripThreshold;
                
                if (newGripState != gripPressed)
                {
                    gripPressed = newGripState;
                    OnGripStateChanged?.Invoke(gripPressed);
                    
                    if (enableHaptics && gripPressed)
                    {
                        TriggerHapticFeedback(hapticAmplitude * 0.5f, hapticDuration * 0.5f);
                    }
                }
            }
            
            // Update trigger input
            bool newTriggerState = false;
            float newTriggerValue = 0f;
            if (inputDevice.TryGetFeatureValue(CommonUsages.trigger, out newTriggerValue))
            {
                triggerValue = newTriggerValue;
                newTriggerState = triggerValue > triggerThreshold;
                
                if (newTriggerState != triggerPressed)
                {
                    triggerPressed = newTriggerState;
                    OnTriggerStateChanged?.Invoke(triggerPressed);
                    
                    if (enableHaptics && triggerPressed)
                    {
                        TriggerHapticFeedback(hapticAmplitude, hapticDuration);
                    }
                }
            }
            
            // Update button inputs
            bool newPrimaryButton = false;
            if (inputDevice.TryGetFeatureValue(CommonUsages.primaryButton, out newPrimaryButton))
            {
                if (newPrimaryButton != primaryButtonPressed)
                {
                    primaryButtonPressed = newPrimaryButton;
                    OnPrimaryButtonStateChanged?.Invoke(primaryButtonPressed);
                }
            }
            
            bool newSecondaryButton = false;
            if (inputDevice.TryGetFeatureValue(CommonUsages.secondaryButton, out newSecondaryButton))
            {
                if (newSecondaryButton != secondaryButtonPressed)
                {
                    secondaryButtonPressed = newSecondaryButton;
                    OnSecondaryButtonStateChanged?.Invoke(secondaryButtonPressed);
                }
            }
            
            bool newMenuButton = false;
            if (inputDevice.TryGetFeatureValue(CommonUsages.menuButton, out newMenuButton))
            {
                if (newMenuButton != menuButtonPressed)
                {
                    menuButtonPressed = newMenuButton;
                }
            }
            
            // Update thumbstick input
            Vector2 newThumbstick = Vector2.zero;
            if (inputDevice.TryGetFeatureValue(CommonUsages.primary2DAxis, out newThumbstick))
            {
                if (Vector2.Distance(newThumbstick, thumbstickInput) > 0.1f)
                {
                    thumbstickInput = newThumbstick;
                    OnThumbstickChanged?.Invoke(thumbstickInput);
                }
            }
        }
        
        private void UpdateHandPose()
        {
            // Update hand position and rotation
            if (inputDevice.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position))
            {
                handPosition = position;
                transform.localPosition = position;
            }
            
            if (inputDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation))
            {
                handRotation = rotation;
                transform.localRotation = rotation;
            }
            
            // Update hand velocity
            if (inputDevice.TryGetFeatureValue(CommonUsages.deviceVelocity, out Vector3 velocity))
            {
                handVelocity = velocity;
            }
            
            if (inputDevice.TryGetFeatureValue(CommonUsages.deviceAngularVelocity, out Vector3 angularVelocity))
            {
                handAngularVelocity = angularVelocity;
            }
        }
        
        private void UpdateHandAnimation()
        {
            if (handAnimator == null) return;
            
            // Update hand animation parameters
            handAnimator.SetFloat(gripAnimationParameter, gripValue);
            handAnimator.SetFloat(triggerAnimationParameter, triggerValue);
            
            // Calculate point gesture (opposite of grip)
            float pointValue = 1f - triggerValue;
            handAnimator.SetFloat(pointAnimationParameter, pointValue);
            
            // Calculate thumb position based on buttons
            float thumbValue = (primaryButtonPressed || secondaryButtonPressed) ? 1f : 0f;
            handAnimator.SetFloat(thumbAnimationParameter, thumbValue);
        }
        
        public void SetHandType(bool isLeft)
        {
            isLeftHand = isLeft;
            inputCharacteristics = isLeftHand ? 
                InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller :
                InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller;
                
            // Reset device search
            deviceFound = false;
        }
        
        public void SetInputState(bool trigger, bool grip)
        {
            // For network synchronization - set input states manually
            triggerPressed = trigger;
            gripPressed = grip;
            triggerValue = trigger ? 1f : 0f;
            gripValue = grip ? 1f : 0f;
            
            UpdateHandAnimation();
        }
        
        public void GetInputState(out bool trigger, out bool grip)
        {
            trigger = triggerPressed;
            grip = gripPressed;
        }
        
        public void TriggerHapticFeedback(float amplitude = 0.5f, float duration = 0.1f)
        {
            if (enableHaptics && deviceFound)
            {
                inputDevice.SendHapticImpulse(0, amplitude, duration);
            }
        }
        
        public void EnableRayInteractor(bool enable)
        {
            if (rayInteractor != null)
            {
                rayInteractor.enabled = enable;
            }
            
            if (directInteractor != null)
            {
                directInteractor.enabled = !enable; // Disable direct when ray is enabled
            }
        }
        
        public bool IsInteracting()
        {
            bool directInteracting = directInteractor != null && directInteractor.hasSelection;
            bool rayInteracting = rayInteractor != null && rayInteractor.hasSelection;
            return directInteracting || rayInteracting;
        }
        
        public GameObject GetInteractedObject()
        {
            if (directInteractor != null && directInteractor.hasSelection)
            {
                return directInteractor.interactablesSelected[0].transform.gameObject;
            }
            
            if (rayInteractor != null && rayInteractor.hasSelection)
            {
                return rayInteractor.interactablesSelected[0].transform.gameObject;
            }
            
            return null;
        }
        
        // Input interface for network synchronization
        public Vector2 GetThumbstickInput()
        {
            return thumbstickInput;
        }
        
        public bool GetButtonState(VRButton button)
        {
            switch (button)
            {
                case VRButton.Grip:
                    return gripPressed;
                case VRButton.Trigger:
                    return triggerPressed;
                case VRButton.Primary:
                    return primaryButtonPressed;
                case VRButton.Secondary:
                    return secondaryButtonPressed;
                case VRButton.Menu:
                    return menuButtonPressed;
                default:
                    return false;
            }
        }
        
        private void OnDrawGizmosSelected()
        {
            // Draw hand interaction sphere
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, 0.1f);
            
            // Draw velocity vector
            if (Application.isPlaying && handVelocity.magnitude > 0.1f)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawRay(transform.position, handVelocity);
            }
        }
    }
    
    public enum VRButton
    {
        Grip,
        Trigger,
        Primary,
        Secondary,
        Menu
    }
} 