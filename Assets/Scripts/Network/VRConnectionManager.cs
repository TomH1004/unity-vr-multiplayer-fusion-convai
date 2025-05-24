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
    public class VRConnectionManager : MonoBehaviour, INetworkBehaviour
    {
        [Header("Network Settings")]
        [SerializeField] private NetworkPrefabRef playerPrefab;
        [SerializeField] private int maxPlayers = 8;
        [SerializeField] private string roomName = "VRRoom";
        
        [Header("Spawn Settings")]
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private float spawnRadius = 2f;
        
        private NetworkRunner _runner;
        private Dictionary<PlayerRef, NetworkObject> _players = new Dictionary<PlayerRef, NetworkObject>();
        
        // Events
        public static event Action<NetworkRunner> OnConnectedToServer;
        public static event Action<NetworkRunner> OnDisconnectedFromServer;
        public static event Action<PlayerRef> OnPlayerJoined;
        public static event Action<PlayerRef> OnPlayerLeft;
        
        public NetworkRunner Runner => _runner;
        public Dictionary<PlayerRef, NetworkObject> Players => _players;
        
        private void Start()
        {
            InitializeRunner();
        }
        
        private async void InitializeRunner()
        {
            // Create the NetworkRunner
            _runner = gameObject.AddComponent<NetworkRunner>();
            _runner.ProvideInput = true;
            
            // Start the session
            await StartSession();
        }
        
        private async System.Threading.Tasks.Task StartSession()
        {
            try
            {
                var result = await _runner.StartGame(new StartGameArgs()
                {
                    GameMode = GameMode.Shared,
                    SessionName = roomName,
                    Scene = SceneManager.GetActiveScene().buildIndex,
                    SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>(),
                    PlayerCount = maxPlayers
                });
                
                if (result.Ok)
                {
                    Debug.Log($"Successfully connected to room: {roomName}");
                    OnConnectedToServer?.Invoke(_runner);
                }
                else
                {
                    Debug.LogError($"Failed to connect: {result.ShutdownReason}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Connection error: {e.Message}");
            }
        }
        
        public void OnPlayerJoinedSession(PlayerRef player)
        {
            Debug.Log($"Player {player} joined the session");
            
            // Spawn player on all clients
            if (_runner.HasStateAuthority)
            {
                SpawnPlayer(player);
            }
            
            OnPlayerJoined?.Invoke(player);
        }
        
        public void OnPlayerLeftSession(PlayerRef player)
        {
            Debug.Log($"Player {player} left the session");
            
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
                Debug.LogError("Player prefab not assigned!");
                return;
            }
            
            Vector3 spawnPosition = GetSpawnPosition();
            
            // Spawn the player
            NetworkObject networkPlayer = _runner.Spawn(
                playerPrefab, 
                spawnPosition, 
                Quaternion.identity, 
                player
            );
            
            if (networkPlayer != null)
            {
                _players[player] = networkPlayer;
                Debug.Log($"Spawned player {player} at position {spawnPosition}");
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
        
        public void OnConnectedToServer(NetworkRunner runner)
        {
            Debug.Log("Connected to Fusion server");
            VRConnectionManager.OnConnectedToServer?.Invoke(runner);
        }
        
        public void OnDisconnectedFromServer(NetworkRunner runner)
        {
            Debug.Log("Disconnected from Fusion server");
            OnDisconnectedFromServer?.Invoke(runner);
        }
        
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
            // Handle session list updates if needed
        }
        
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            Debug.Log($"Network shutdown: {shutdownReason}");
        }
        
        // Manual connection methods
        public async void CreateRoom(string customRoomName = "")
        {
            if (!string.IsNullOrEmpty(customRoomName))
                roomName = customRoomName;
                
            await StartSession();
        }
        
        public async void JoinRoom(string targetRoomName)
        {
            roomName = targetRoomName;
            await StartSession();
        }
        
        public void LeaveRoom()
        {
            if (_runner != null)
            {
                _runner.Shutdown();
            }
        }
        
        private void OnDestroy()
        {
            if (_runner != null)
            {
                _runner.Shutdown();
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