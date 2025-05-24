using UnityEngine;
using UnityEngine.UI;
using VRMultiplayer.Network;
using VRMultiplayer.Avatar;
using TMPro;

namespace VRMultiplayer.UI
{
    /// <summary>
    /// VR Menu Manager for avatar selection and multiplayer room management
    /// Provides a simple VR-friendly UI for Ready Player Me integration
    /// </summary>
    public class VRMenuManager : MonoBehaviour
    {
        [Header("UI Panels")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject avatarSelectionPanel;
        [SerializeField] private GameObject roomPanel;
        [SerializeField] private GameObject settingsPanel;
        
        [Header("Main Menu")]
        [SerializeField] private Button createRoomButton;
        [SerializeField] private Button joinRoomButton;
        [SerializeField] private Button avatarButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button exitButton;
        
        [Header("Room Management")]
        [SerializeField] private TMP_InputField roomNameInput;
        [SerializeField] private Button confirmCreateButton;
        [SerializeField] private Button confirmJoinButton;
        [SerializeField] private Button backButton;
        [SerializeField] private TextMeshProUGUI statusText;
        
        [Header("Avatar Selection")]
        [SerializeField] private TMP_InputField avatarUrlInput;
        [SerializeField] private Button loadAvatarButton;
        [SerializeField] private Button useDefaultAvatarButton;
        [SerializeField] private Button avatarBackButton;
        [SerializeField] private RawImage avatarPreview;
        
        [Header("Settings")]
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private Toggle hapticToggle;
        [SerializeField] private Toggle teleportToggle;
        [SerializeField] private Button settingsBackButton;
        
        [Header("Avatar URLs")]
        [SerializeField] private string[] defaultAvatarUrls = {
            "https://models.readyplayer.me/64bfa8f1b8a9b6f1c8f5d9e3.glb",
            "https://models.readyplayer.me/64bfa8f1b8a9b6f1c8f5d9e4.glb",
            "https://models.readyplayer.me/64bfa8f1b8a9b6f1c8f5d9e5.glb"
        };
        
        // Components
        private VRConnectionManager connectionManager;
        private VRAvatarController avatarController;
        private NetworkVRPlayer localPlayer;
        
        // UI State
        private MenuState currentState = MenuState.MainMenu;
        private string selectedAvatarUrl = "";
        
        public string SelectedAvatarUrl => selectedAvatarUrl;
        
        private void Start()
        {
            InitializeUI();
            SetupEventListeners();
            ShowPanel(MenuState.MainMenu);
        }
        
        private void InitializeUI()
        {
            // Find connection manager
            connectionManager = FindObjectOfType<VRConnectionManager>();
            if (connectionManager == null)
            {
                Debug.LogError("VRConnectionManager not found in scene!");
            }
            
            // Set default values
            if (roomNameInput != null)
            {
                roomNameInput.text = "VRRoom_" + Random.Range(1000, 9999);
            }
            
            if (avatarUrlInput != null && defaultAvatarUrls.Length > 0)
            {
                avatarUrlInput.text = defaultAvatarUrls[0];
                selectedAvatarUrl = defaultAvatarUrls[0];
            }
            
            // Initialize settings
            if (volumeSlider != null)
            {
                volumeSlider.value = AudioListener.volume;
            }
        }
        
        private void SetupEventListeners()
        {
            // Main Menu Events
            if (createRoomButton != null)
                createRoomButton.onClick.AddListener(OnCreateRoomClicked);
            if (joinRoomButton != null)
                joinRoomButton.onClick.AddListener(OnJoinRoomClicked);
            if (avatarButton != null)
                avatarButton.onClick.AddListener(OnAvatarSelectionClicked);
            if (settingsButton != null)
                settingsButton.onClick.AddListener(OnSettingsClicked);
            if (exitButton != null)
                exitButton.onClick.AddListener(OnExitClicked);
            
            // Room Management Events
            if (confirmCreateButton != null)
                confirmCreateButton.onClick.AddListener(OnConfirmCreateRoom);
            if (confirmJoinButton != null)
                confirmJoinButton.onClick.AddListener(OnConfirmJoinRoom);
            if (backButton != null)
                backButton.onClick.AddListener(OnBackToMainMenu);
            
            // Avatar Selection Events
            if (loadAvatarButton != null)
                loadAvatarButton.onClick.AddListener(OnLoadCustomAvatar);
            if (useDefaultAvatarButton != null)
                useDefaultAvatarButton.onClick.AddListener(OnUseDefaultAvatar);
            if (avatarBackButton != null)
                avatarBackButton.onClick.AddListener(OnBackToMainMenu);
            
            // Settings Events
            if (volumeSlider != null)
                volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            if (hapticToggle != null)
                hapticToggle.onValueChanged.AddListener(OnHapticToggleChanged);
            if (teleportToggle != null)
                teleportToggle.onValueChanged.AddListener(OnTeleportToggleChanged);
            if (settingsBackButton != null)
                settingsBackButton.onClick.AddListener(OnBackToMainMenu);
            
            // Network Events
            VRConnectionManager.OnConnectedToServer += OnConnectedToServer;
            VRConnectionManager.OnDisconnectedFromServer += OnDisconnectedFromServer;
        }
        
        private void ShowPanel(MenuState state)
        {
            currentState = state;
            
            // Hide all panels
            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            if (avatarSelectionPanel != null) avatarSelectionPanel.SetActive(false);
            if (roomPanel != null) roomPanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            
            // Show current panel
            switch (state)
            {
                case MenuState.MainMenu:
                    if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
                    break;
                case MenuState.AvatarSelection:
                    if (avatarSelectionPanel != null) avatarSelectionPanel.SetActive(true);
                    break;
                case MenuState.RoomManagement:
                    if (roomPanel != null) roomPanel.SetActive(true);
                    break;
                case MenuState.Settings:
                    if (settingsPanel != null) settingsPanel.SetActive(true);
                    break;
            }
        }
        
        // Main Menu Event Handlers
        private void OnCreateRoomClicked()
        {
            ShowPanel(MenuState.RoomManagement);
            if (confirmCreateButton != null) confirmCreateButton.gameObject.SetActive(true);
            if (confirmJoinButton != null) confirmJoinButton.gameObject.SetActive(false);
            UpdateStatusText("Enter room name to create");
        }
        
        private void OnJoinRoomClicked()
        {
            ShowPanel(MenuState.RoomManagement);
            if (confirmCreateButton != null) confirmCreateButton.gameObject.SetActive(false);
            if (confirmJoinButton != null) confirmJoinButton.gameObject.SetActive(true);
            UpdateStatusText("Enter room name to join");
        }
        
        private void OnAvatarSelectionClicked()
        {
            ShowPanel(MenuState.AvatarSelection);
        }
        
        private void OnSettingsClicked()
        {
            ShowPanel(MenuState.Settings);
        }
        
        private void OnExitClicked()
        {
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }
        
        // Room Management Event Handlers
        private void OnConfirmCreateRoom()
        {
            string roomName = roomNameInput != null ? roomNameInput.text : "DefaultRoom";
            if (string.IsNullOrEmpty(roomName))
            {
                UpdateStatusText("Please enter a room name");
                return;
            }
            
            UpdateStatusText("Creating room...");
            connectionManager?.CreateRoom(roomName);
        }
        
        private void OnConfirmJoinRoom()
        {
            string roomName = roomNameInput != null ? roomNameInput.text : "DefaultRoom";
            if (string.IsNullOrEmpty(roomName))
            {
                UpdateStatusText("Please enter a room name");
                return;
            }
            
            UpdateStatusText("Joining room...");
            connectionManager?.JoinRoom(roomName);
        }
        
        private void OnBackToMainMenu()
        {
            ShowPanel(MenuState.MainMenu);
        }
        
        // Avatar Selection Event Handlers
        private void OnLoadCustomAvatar()
        {
            string avatarUrl = avatarUrlInput != null ? avatarUrlInput.text : "";
            if (string.IsNullOrEmpty(avatarUrl))
            {
                Debug.LogWarning("Please enter an avatar URL");
                return;
            }
            
            selectedAvatarUrl = avatarUrl;
            LoadSelectedAvatar();
        }
        
        private void OnUseDefaultAvatar()
        {
            if (defaultAvatarUrls.Length > 0)
            {
                int randomIndex = Random.Range(0, defaultAvatarUrls.Length);
                selectedAvatarUrl = defaultAvatarUrls[randomIndex];
                
                if (avatarUrlInput != null)
                {
                    avatarUrlInput.text = selectedAvatarUrl;
                }
                
                LoadSelectedAvatar();
            }
        }
        
        private void LoadSelectedAvatar()
        {
            if (avatarController == null)
            {
                // Find local player's avatar controller
                localPlayer = FindLocalPlayer();
                if (localPlayer != null)
                {
                    avatarController = localPlayer.GetComponent<VRAvatarController>();
                }
            }
            
            if (avatarController != null)
            {
                avatarController.LoadAvatar(selectedAvatarUrl);
                Debug.Log($"Loading avatar: {selectedAvatarUrl}");
            }
            else
            {
                Debug.LogWarning("Avatar controller not found");
            }
        }
        
        // Settings Event Handlers
        private void OnVolumeChanged(float value)
        {
            AudioListener.volume = value;
        }
        
        private void OnHapticToggleChanged(bool enabled)
        {
            // Configure haptic feedback settings
            var handControllers = FindObjectsOfType<VRMultiplayer.VR.VRHandController>();
            foreach (var controller in handControllers)
            {
                // Would need to add haptic enable/disable method to VRHandController
                Debug.Log($"Haptic feedback {(enabled ? "enabled" : "disabled")}");
            }
        }
        
        private void OnTeleportToggleChanged(bool enabled)
        {
            // Configure teleportation settings
            var locomotion = FindObjectOfType<VRMultiplayer.VR.VRLocomotion>();
            if (locomotion != null)
            {
                locomotion.SetTeleportationEnabled(enabled);
            }
        }
        
        // Network Event Handlers
        private void OnConnectedToServer(Fusion.NetworkRunner runner)
        {
            UpdateStatusText("Connected to server!");
            ShowPanel(MenuState.MainMenu);
        }
        
        private void OnDisconnectedFromServer(Fusion.NetworkRunner runner)
        {
            UpdateStatusText("Disconnected from server");
            ShowPanel(MenuState.MainMenu);
        }
        
        // Utility Methods
        private void UpdateStatusText(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
            Debug.Log($"Status: {message}");
        }
        
        private NetworkVRPlayer FindLocalPlayer()
        {
            var players = FindObjectsOfType<NetworkVRPlayer>();
            foreach (var player in players)
            {
                if (player.Object != null && player.Object.HasInputAuthority)
                {
                    return player;
                }
            }
            return null;
        }
        
        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }
        
        public void ToggleMenu()
        {
            gameObject.SetActive(!gameObject.activeSelf);
        }
        
        private void OnDestroy()
        {
            // Unsubscribe from events
            VRConnectionManager.OnConnectedToServer -= OnConnectedToServer;
            VRConnectionManager.OnDisconnectedFromServer -= OnDisconnectedFromServer;
        }
    }
    
    public enum MenuState
    {
        MainMenu,
        AvatarSelection,
        RoomManagement,
        Settings
    }
} 