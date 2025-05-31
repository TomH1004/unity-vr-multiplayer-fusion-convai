using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VRMultiplayer.Network
{
    /// <summary>
    /// Main connection manager for VR multiplayer using Photon Fusion shared mode
    /// Handles room creation, joining, and player spawning
    /// </summary>
    public class VRConnectionManager : MonoBehaviour, INetworkRunnerCallbacks
    {
        [Header("Network Settings")]
        [SerializeField] private NetworkPrefabRef playerPrefab;
        [SerializeField] private int maxPlayers = 8;
        [SerializeField] private string roomName = "VRRoom";
        [SerializeField] private bool autoConnectOnStart = false; // New setting to control auto-connection
        
        [Header("Spawn Settings")]
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private float spawnRadius = 2f;
        
        [Header("Input Settings")]
        [SerializeField] private VRInputProvider inputProvider;
        
        private NetworkRunner _runner;
        private Dictionary<PlayerRef, NetworkObject> _players = new Dictionary<PlayerRef, NetworkObject>();
        private Dictionary<PlayerRef, VRInputProvider> _playerInputProviders = new Dictionary<PlayerRef, VRInputProvider>();
        private bool isConnecting = false;
        
        // Events
        public static event Action<NetworkRunner> OnConnectedToServer;
        public static event Action<NetworkRunner> OnDisconnectedFromServer;
        public static event Action<PlayerRef> OnPlayerJoined;
        public static event Action<PlayerRef> OnPlayerLeft;
        
        public NetworkRunner Runner => _runner;
        public Dictionary<PlayerRef, NetworkObject> Players => _players;
        public bool IsConnected => _runner != null && _runner.IsRunning;
        public bool IsConnecting => isConnecting;
        
        private void Start()
        {
            // Find or create input provider
            if (inputProvider == null)
            {
                inputProvider = FindObjectOfType<VRInputProvider>();
                if (inputProvider == null)
                {
                    var inputProviderObj = new GameObject("VRInputProvider");
                    inputProvider = inputProviderObj.AddComponent<VRInputProvider>();
                }
            }
            
            if (autoConnectOnStart)
            {
                InitializeRunner();
            }
        }
        
        private async void InitializeRunner()
        {
            if (_runner != null)
            {
                Debug.LogWarning("[VRConnectionManager] NetworkRunner already exists!");
                return;
            }
            
            // Create the NetworkRunner
            _runner = gameObject.AddComponent<NetworkRunner>();
            _runner.ProvideInput = true;
            
            // Start the session
            await StartSession();
        }
        
        private async System.Threading.Tasks.Task StartSession()
        {
            if (isConnecting)
            {
                Debug.LogWarning("[VRConnectionManager] Already connecting to a session!");
                return;
            }
            
            isConnecting = true;
            
            try
            {
                var result = await _runner.StartGame(new StartGameArgs()
                {
                    GameMode = GameMode.Shared,
                    SessionName = roomName,
                    Scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex),
                    SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>(),
                    PlayerCount = maxPlayers
                });
                
                if (result.Ok)
                {
                    OnConnectedToServer?.Invoke(_runner);
                }
                else
                {
                    Debug.LogError($"[VRConnectionManager] Failed to connect: {result.ShutdownReason}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[VRConnectionManager] Connection error: {e.Message}");
            }
            finally
            {
                isConnecting = false;
            }
        }
        
        public void OnPlayerJoinedSession(PlayerRef player)
        {
            // In Shared Mode, check if we are the master client to spawn players
            if (_runner != null && _runner.IsSharedModeMasterClient)
            {
                SpawnPlayer(player);
            }
            
            OnPlayerJoined?.Invoke(player);
        }
        
        public void OnPlayerLeftSession(PlayerRef player)
        {
            // Despawn player
            if (_players.TryGetValue(player, out NetworkObject networkObject))
            {
                if (networkObject != null)
                {
                    _runner.Despawn(networkObject);
                }
                _players.Remove(player);
            }
            
            OnPlayerLeft?.Invoke(player);
        }
        
        private void SpawnPlayer(PlayerRef player)
        {
            if (playerPrefab == null)
            {
                Debug.LogError("[VRConnectionManager] Player prefab not assigned!");
                return;
            }
            
            Vector3 spawnPosition = GetSpawnPosition();
            
            // Spawn the player with the correct InputAuthority
            NetworkObject networkPlayer = _runner.Spawn(
                playerPrefab, 
                spawnPosition, 
                Quaternion.identity, 
                player  // This ensures the InputAuthority is set to the correct player
            );
            
            if (networkPlayer != null)
            {
                _players[player] = networkPlayer;
                
                // Get the NetworkVRPlayer component and verify setup
                var networkVRPlayer = networkPlayer.GetComponent<NetworkVRPlayer>();
                if (networkVRPlayer == null)
                {
                    Debug.LogError($"[VRConnectionManager] NetworkVRPlayer component NOT found on spawned object for Player {player}!");
                }
            }
            else
            {
                Debug.LogError($"[VRConnectionManager] Failed to spawn player {player}!");
            }
        }
        
        private Vector3 GetSpawnPosition()
        {
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                // Use predefined spawn points
                int spawnIndex = _players.Count % spawnPoints.Length;
                return spawnPoints[spawnIndex].position;
            }
            else
            {
                // Generate random spawn position
                Vector3 randomOffset = UnityEngine.Random.insideUnitSphere * spawnRadius;
                randomOffset.y = 0; // Keep on ground level
                return transform.position + randomOffset;
            }
        }
        
        // INetworkRunnerCallbacks implementation
        void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner)
        {
            OnConnectedToServer?.Invoke(runner);
        }
        
        void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            Debug.LogWarning($"[VRConnectionManager] Disconnected from server: {reason}");
            OnDisconnectedFromServer?.Invoke(runner);
        }
        
        void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            OnPlayerJoinedSession(player);
        }
        
        void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            OnPlayerLeftSession(player);
        }
        
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
            // Handle session list updates if needed
        }
        
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            Debug.LogWarning($"[VRConnectionManager] Network shutdown: {shutdownReason}");
        }
        
        // Required INetworkRunnerCallbacks methods
        public void OnInput(NetworkRunner runner, NetworkInput input) 
        { 
            // In Shared Mode, each client controls their own player object directly
            if (runner != null)
            {
                // Find the local player's NetworkVRPlayer component
                var allNetworkVRPlayers = FindObjectsOfType<NetworkVRPlayer>();
                NetworkVRPlayer localNetworkVRPlayer = null;
                
                foreach (var networkVRPlayer in allNetworkVRPlayers)
                {
                    if (networkVRPlayer.Object != null)
                    {
                        // In Shared Mode, check if this object's InputAuthority matches our LocalPlayer
                        // OR if the OwnerPlayer matches (in case InputAuthority is None)
                        bool isLocalPlayer = (networkVRPlayer.Object.InputAuthority == runner.LocalPlayer) ||
                                           (networkVRPlayer.OwnerPlayer == runner.LocalPlayer);
                        
                        if (isLocalPlayer)
                        {
                            localNetworkVRPlayer = networkVRPlayer;
                            break;
                        }
                    }
                }
                
                if (localNetworkVRPlayer != null)
                {
                    // Update the players dictionary
                    _players[runner.LocalPlayer] = localNetworkVRPlayer.Object;
                    
                    // Gather input from the local player
                    var inputData = new NetworkVRPlayer.VRNetworkInputData();
                    localNetworkVRPlayer.GatherInput(ref inputData);
                    input.Set(inputData);
                }
                else
                {
                    // Fallback: try to use the global input provider
                    if (inputProvider != null)
                    {
                        inputProvider.OnInput(runner, input);
                    }
                    else
                    {
                        Debug.LogError($"[VRConnectionManager] No input source available for Player {runner.LocalPlayer}!");
                    }
                }
            }
            else
            {
                Debug.LogError("[VRConnectionManager] NetworkRunner is null in OnInput!");
            }
        }
        
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) 
        {
            Debug.LogError($"[VRConnectionManager] Connection failed to {remoteAddress}: {reason}");
        }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        
        // Update method to periodically check for spawned NetworkVRPlayer objects that aren't tracked
        private void Update()
        {
            if (_runner != null && _runner.IsRunning)
            {
                // Check for any NetworkVRPlayer objects that aren't in our _players dictionary
                var allNetworkVRPlayers = FindObjectsOfType<NetworkVRPlayer>();
                foreach (var networkVRPlayer in allNetworkVRPlayers)
                {
                    if (networkVRPlayer.Object != null)
                    {
                        // In Shared Mode, check both InputAuthority and OwnerPlayer
                        PlayerRef playerRef = PlayerRef.None;
                        
                        if (networkVRPlayer.Object.InputAuthority != PlayerRef.None)
                        {
                            playerRef = networkVRPlayer.Object.InputAuthority;
                        }
                        else if (networkVRPlayer.OwnerPlayer != PlayerRef.None)
                        {
                            playerRef = networkVRPlayer.OwnerPlayer;
                        }
                        
                        if (playerRef != PlayerRef.None && !_players.ContainsKey(playerRef))
                        {
                            _players[playerRef] = networkVRPlayer.Object;
                        }
                    }
                }
            }
        }
        
        // Manual connection methods
        public async void CreateRoom(string customRoomName = "")
        {
            if (isConnecting)
            {
                Debug.LogWarning("[VRConnectionManager] Already connecting!");
                return;
            }
            
            // Set room name
            if (!string.IsNullOrEmpty(customRoomName))
                roomName = customRoomName;
            
            // If already connected, disconnect first
            if (IsConnected)
            {
                await DisconnectAndCleanup();
            }
            
            // Initialize runner if needed
            if (_runner == null)
            {
                _runner = gameObject.AddComponent<NetworkRunner>();
                _runner.ProvideInput = true;
            }
            
            await StartSession();
        }
        
        public async void JoinRoom(string targetRoomName)
        {
            if (isConnecting)
            {
                Debug.LogWarning("[VRConnectionManager] Already connecting!");
                return;
            }
            
            if (string.IsNullOrEmpty(targetRoomName))
            {
                Debug.LogError("[VRConnectionManager] Room name cannot be empty!");
                return;
            }
            
            // Set room name
            roomName = targetRoomName;
            
            // If already connected, disconnect first
            if (IsConnected)
            {
                await DisconnectAndCleanup();
            }
            
            // Initialize runner if needed
            if (_runner == null)
            {
                _runner = gameObject.AddComponent<NetworkRunner>();
                _runner.ProvideInput = true;
            }
            
            await StartSession();
        }
        
        public async void LeaveRoom()
        {
            await DisconnectAndCleanup();
        }
        
        private async System.Threading.Tasks.Task DisconnectAndCleanup()
        {
            if (_runner != null)
            {
                // Clear players
                _players.Clear();
                
                // Shutdown runner
                if (_runner.IsRunning)
                {
                    _runner.Shutdown();
                    
                    // Wait for shutdown to complete
                    while (_runner.IsRunning)
                    {
                        await System.Threading.Tasks.Task.Yield();
                    }
                }
                
                // Destroy the component
                if (_runner != null)
                {
                    DestroyImmediate(_runner);
                    _runner = null;
                }
            }
        }
        
        private void OnDestroy()
        {
            // Use async cleanup but don't await since this is OnDestroy
            if (_runner != null)
            {
                _players.Clear();
                if (_runner.IsRunning)
                {
                    _runner.Shutdown();
                }
                _runner = null;
            }
        }
        
        private void OnDrawGizmosSelected()
        {
            // Draw spawn radius
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, spawnRadius);
            
            // Draw spawn points
            if (spawnPoints != null)
            {
                Gizmos.color = Color.blue;
                foreach (var spawnPoint in spawnPoints)
                {
                    if (spawnPoint != null)
                    {
                        Gizmos.DrawWireCube(spawnPoint.position, Vector3.one);
                    }
                }
            }
        }
    }
} 