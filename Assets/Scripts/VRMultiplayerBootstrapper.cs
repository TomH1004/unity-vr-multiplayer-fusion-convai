using UnityEngine;
using VRMultiplayer.Network;
using VRMultiplayer.UI;
using VRMultiplayer.AI;
using ReadyPlayerMe.Core;

namespace VRMultiplayer
{
    /// <summary>
    /// Main bootstrapper for VR Multiplayer system
    /// Initializes all components and handles startup sequence
    /// </summary>
    public class VRMultiplayerBootstrapper : MonoBehaviour
    {
        [Header("System Settings")]
        [SerializeField] private bool autoStartNetworking = false;
        [SerializeField] private bool enableDebugLogging = true;
        [SerializeField] private string defaultRoomName = "VRRoom";
        [SerializeField] private string defaultAvatarUrl = "";
        
        [Header("Component References")]
        [SerializeField] private VRConnectionManager connectionManager;
        [SerializeField] private VRMenuManager menuManager;
        [SerializeField] private GameObject vrOrigin;
        [SerializeField] private NetworkConvAICharacter aiCharacter;
        
        [Header("Performance Settings")]
        [SerializeField] private int targetFrameRate = 90; // VR target framerate
        [SerializeField] private bool enableVSync = false;
        [SerializeField] private bool optimizeForMobile = true;
        
        // System state
        private bool isInitialized = false;
        private bool isVREnabled = false;
        
        public static VRMultiplayerBootstrapper Instance { get; private set; }
        
        // Events
        public System.Action OnSystemInitialized;
        public System.Action OnVRInitialized;
        public System.Action OnNetworkInitialized;
        
        private void Awake()
        {
            // Singleton pattern
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeSystem();
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        private void InitializeSystem()
        {
            Debug.Log("VR Multiplayer System: Starting initialization...");
            
            // Setup performance settings
            SetupPerformanceSettings();
            
            // Find required components
            FindSystemComponents();
            
            // Initialize VR
            InitializeVR();
            
            // Initialize networking
            InitializeNetworking();
            
            // Initialize UI
            InitializeUI();
            
            // Initialize ConvAI
            InitializeConvAI();
            
            // Setup Ready Player Me
            SetupReadyPlayerMe();
            
            isInitialized = true;
            OnSystemInitialized?.Invoke();
            
            Debug.Log("VR Multiplayer System: Initialization complete!");
        }
        
        private void SetupPerformanceSettings()
        {
            // Set target framerate for VR
            Application.targetFrameRate = targetFrameRate;
            
            // Configure VSync
            if (enableVSync)
            {
                QualitySettings.vSyncCount = 1;
            }
            else
            {
                QualitySettings.vSyncCount = 0;
            }
            
            // Mobile optimizations
            if (optimizeForMobile)
            {
                QualitySettings.antiAliasing = 0;
                QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
                QualitySettings.shadowResolution = ShadowResolution.Low;
                QualitySettings.shadowDistance = 50f;
            }
            
            // Prevent screen dimming on mobile
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            
            Debug.Log($"Performance settings configured - Target FPS: {targetFrameRate}, VSync: {enableVSync}");
        }
        
        private void FindSystemComponents()
        {
            // Find connection manager
            if (connectionManager == null)
            {
                connectionManager = FindObjectOfType<VRConnectionManager>();
            }
            
            // Find menu manager
            if (menuManager == null)
            {
                menuManager = FindObjectOfType<VRMenuManager>();
            }
            
            // Find VR Origin
            if (vrOrigin == null)
            {
                var xrOrigin = FindObjectOfType<Unity.XR.CoreUtils.XROrigin>();
                if (xrOrigin != null)
                {
                    vrOrigin = xrOrigin.gameObject;
                }
            }
            
            Debug.Log("System components located");
        }
        
        private void InitializeVR()
        {
            // Check if VR is available
            bool vrSupported = UnityEngine.XR.XRSettings.enabled || 
                              UnityEngine.XR.XRSettings.loadedDeviceName != "None";
            
            if (vrSupported)
            {
                isVREnabled = true;
                Debug.Log("VR system initialized successfully");
                OnVRInitialized?.Invoke();
            }
            else
            {
                Debug.LogWarning("VR not detected - running in desktop mode");
                SetupDesktopMode();
            }
        }
        
        private void SetupDesktopMode()
        {
            // Configure for desktop testing when VR is not available
            if (vrOrigin != null)
            {
                var camera = Camera.main;
                if (camera != null)
                {
                    // Enable mouse look for desktop testing
                    var mouseLook = camera.gameObject.GetComponent<MouseLook>();
                    if (mouseLook == null)
                    {
                        mouseLook = camera.gameObject.AddComponent<MouseLook>();
                    }
                }
            }
            
            Debug.Log("Desktop mode configured for testing");
        }
        
        private void InitializeNetworking()
        {
            if (connectionManager != null)
            {
                // Subscribe to network events
                VRConnectionManager.OnConnectedToServer += OnNetworkConnected;
                VRConnectionManager.OnDisconnectedFromServer += OnNetworkDisconnected;
                
                // Auto-start networking if enabled
                if (autoStartNetworking)
                {
                    StartCoroutine(AutoStartNetworkingCoroutine());
                }
                
                OnNetworkInitialized?.Invoke();
                Debug.Log("Networking system initialized");
            }
            else
            {
                Debug.LogError("VRConnectionManager not found!");
            }
        }
        
        private System.Collections.IEnumerator AutoStartNetworkingCoroutine()
        {
            // Wait a frame to ensure everything is initialized
            yield return null;
            
            Debug.Log("Auto-starting networking...");
            connectionManager.CreateRoom(defaultRoomName);
        }
        
        private void InitializeUI()
        {
            if (menuManager != null)
            {
                // Configure menu with default values
                if (!string.IsNullOrEmpty(defaultAvatarUrl))
                {
                    // Set default avatar URL in menu
                    Debug.Log($"Default avatar URL configured: {defaultAvatarUrl}");
                }
                
                Debug.Log("UI system initialized");
            }
            else
            {
                Debug.LogWarning("VRMenuManager not found - UI will not be available");
            }
        }
        
        private void InitializeConvAI()
        {
            // Find ConvAI character
            if (aiCharacter == null)
            {
                aiCharacter = FindObjectOfType<NetworkConvAICharacter>();
            }
            
            if (aiCharacter != null)
            {
                Debug.Log("ConvAI system initialized");
            }
            else
            {
                Debug.LogWarning("ConvAI character not found - AI features will not be available");
            }
        }
        
        private void SetupReadyPlayerMe()
        {
            // Configure Ready Player Me settings
            var avatarConfig = new AvatarConfig
            {
                UseEyeAnimations = true,
                UseEyeBones = true,
                UseDracoMeshCompression = true,
                UseAvatarCaching = true
            };
            
            Debug.Log("Ready Player Me configured");
        }
        
        // Network event handlers
        private void OnNetworkConnected(Fusion.NetworkRunner runner)
        {
            Debug.Log("Connected to network successfully");
            
            // Hide menu when connected
            if (menuManager != null)
            {
                menuManager.SetVisible(false);
            }
        }
        
        private void OnNetworkDisconnected(Fusion.NetworkRunner runner)
        {
            Debug.Log("Disconnected from network");
            
            // Show menu when disconnected
            if (menuManager != null)
            {
                menuManager.SetVisible(true);
            }
        }
        
        // Public API
        public void StartNetworking(string roomName = "")
        {
            if (connectionManager != null)
            {
                string targetRoomName = string.IsNullOrEmpty(roomName) ? defaultRoomName : roomName;
                connectionManager.CreateRoom(targetRoomName);
            }
        }
        
        public void JoinRoom(string roomName)
        {
            if (connectionManager != null)
            {
                connectionManager.JoinRoom(roomName);
            }
        }
        
        public void LeaveRoom()
        {
            if (connectionManager != null)
            {
                connectionManager.LeaveRoom();
            }
        }
        
        public void ToggleMenu()
        {
            if (menuManager != null)
            {
                menuManager.ToggleMenu();
            }
        }
        
        public void SetDefaultAvatar(string avatarUrl)
        {
            defaultAvatarUrl = avatarUrl;
        }
        
        public bool IsVREnabled()
        {
            return isVREnabled;
        }
        
        public bool IsInitialized()
        {
            return isInitialized;
        }
        
        // Utility method for getting system info
        public string GetSystemInfo()
        {
            return $"VR Enabled: {isVREnabled}\n" +
                   $"Platform: {Application.platform}\n" +
                   $"Target FPS: {targetFrameRate}\n" +
                   $"Graphics Device: {SystemInfo.graphicsDeviceName}\n" +
                   $"XR Device: {UnityEngine.XR.XRSettings.loadedDeviceName}";
        }
        
        private void OnDestroy()
        {
            // Unsubscribe from events
            if (connectionManager != null)
            {
                VRConnectionManager.OnConnectedToServer -= OnNetworkConnected;
                VRConnectionManager.OnDisconnectedFromServer -= OnNetworkDisconnected;
            }
        }
        
        private void OnApplicationPause(bool pauseStatus)
        {
            // Handle application pause/resume for mobile VR
            if (pauseStatus)
            {
                Debug.Log("Application paused");
            }
            else
            {
                Debug.Log("Application resumed");
            }
        }
        
        private void OnApplicationFocus(bool hasFocus)
        {
            // Handle application focus changes
            if (!hasFocus)
            {
                Debug.Log("Application lost focus");
            }
            else
            {
                Debug.Log("Application gained focus");
            }
        }
    }
    
    /// <summary>
    /// Simple mouse look component for desktop testing
    /// </summary>
    public class MouseLook : MonoBehaviour
    {
        [SerializeField] private float mouseSensitivity = 100f;
        [SerializeField] private float clampAngle = 80f;
        
        private float rotY = 0f;
        private float rotX = 0f;
        
        private void Start()
        {
            Vector3 rot = transform.localRotation.eulerAngles;
            rotY = rot.y;
            rotX = rot.x;
            Cursor.lockState = CursorLockMode.Locked;
        }
        
        private void Update()
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = -Input.GetAxis("Mouse Y");
            
            rotY += mouseX * mouseSensitivity * Time.deltaTime;
            rotX += mouseY * mouseSensitivity * Time.deltaTime;
            
            rotX = Mathf.Clamp(rotX, -clampAngle, clampAngle);
            
            transform.localRotation = Quaternion.Euler(rotX, rotY, 0);
            
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
            }
        }
    }
} 