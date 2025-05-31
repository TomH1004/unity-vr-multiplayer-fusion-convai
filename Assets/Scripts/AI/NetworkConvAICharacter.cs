using Fusion;
using UnityEngine;
using VRMultiplayer.Avatar;
using VRMultiplayer.Network;
using System.Collections.Generic;
using System.Collections;
using Convai.Scripts.Runtime.Core;
using Convai.Scripts.Runtime.Features;
using Convai.Scripts.Runtime.UI;
using TMPro;

namespace VRMultiplayer.AI
{
    /// <summary>
    /// Networked ConvAI Character that synchronizes AI interactions across multiplayer clients
    /// Both players can interact with the same AI agent instance
    /// </summary>
    [RequireComponent(typeof(ConvaiChatUIHandler))]
    [RequireComponent(typeof(ConvaiGroupNPCController))]
    public class NetworkConvAICharacter : NetworkBehaviour
    {
        [Header("Character Settings")]
        [SerializeField] private string characterName = "AI Assistant";
        [SerializeField] private VRAvatarController avatarController;
        [SerializeField] private ConvAIManager convAIManager;
        [SerializeField] private Transform lookAtTarget;
        
        [Header("Interaction Settings")]
        [SerializeField] private float interactionRange = 3f;
        [SerializeField] private bool requireTurnTaking = true;
        [SerializeField] private float turnTimeLimit = 30f;
        [SerializeField] private KeyCode voiceInputKey = KeyCode.T;
        
        [Header("Visual Feedback")]
        [SerializeField] private GameObject listeningIndicator;
        [SerializeField] private GameObject speakingIndicator;
        [SerializeField] private GameObject thinkingIndicator;
        [SerializeField] private Material idleMaterial;
        [SerializeField] private Material activeMaterial;
        
        // Networked state
        [Networked] public bool IsListening { get; set; }
        [Networked] public bool IsSpeaking { get; set; }
        [Networked] public bool IsThinking { get; set; }
        [Networked] public PlayerRef CurrentSpeaker { get; set; }
        [Networked] public float TurnStartTime { get; set; }
        [Networked] public string LastResponse { get; set; }
        [Networked] public string ConversationId { get; set; }
        
        // Local state
        private Queue<ConversationRequest> conversationQueue = new Queue<ConversationRequest>();
        private bool isProcessingRequest = false;
        private List<NetworkVRPlayer> nearbyPlayers = new List<NetworkVRPlayer>();
        private NetworkVRPlayer localPlayer;
        
        // Events
        public System.Action<string, PlayerRef> OnPlayerStartedSpeaking;
        public System.Action<string> OnAIResponseReceived;
        public System.Action<PlayerRef> OnTurnChanged;
        
        public override void Spawned()
        {
            // Initialize ConvAI manager
            if (convAIManager == null)
            {
                convAIManager = GetComponent<ConvAIManager>();
                if (convAIManager == null)
                {
                    convAIManager = gameObject.AddComponent<ConvAIManager>();
                }
            }
            
            // Initialize avatar controller
            if (avatarController == null)
            {
                avatarController = GetComponent<VRAvatarController>();
            }
            
            // Subscribe to ConvAI events
            ConvAIManager.OnTextResponse += OnConvAITextResponse;
            ConvAIManager.OnVoiceResponse += OnConvAIVoiceResponse;
            ConvAIManager.OnError += OnConvAIError;
            
            // Find local player
            FindLocalPlayer();
            
            // Initialize conversation ID
            if (Object.HasStateAuthority && string.IsNullOrEmpty(ConversationId))
            {
                ConversationId = System.Guid.NewGuid().ToString("N").Substring(0, 16);
            }
            
            // Setup visual indicators
            SetupVisualIndicators();
            
            Debug.Log($"NetworkConvAICharacter spawned: {characterName}");
        }
        
        private void FindLocalPlayer()
        {
            var players = FindObjectsOfType<NetworkVRPlayer>();
            foreach (var player in players)
            {
                if (player.Object != null && player.Object.HasInputAuthority)
                {
                    localPlayer = player;
                    break;
                }
            }
        }
        
        private void SetupVisualIndicators()
        {
            // Setup visual feedback indicators
            if (listeningIndicator != null) listeningIndicator.SetActive(false);
            if (speakingIndicator != null) speakingIndicator.SetActive(false);
            if (thinkingIndicator != null) thinkingIndicator.SetActive(false);
        }
        
        public override void FixedUpdateNetwork()
        {
            // Handle turn time limits
            if (requireTurnTaking && CurrentSpeaker != PlayerRef.None)
            {
                float elapsedTime = Runner.SimulationTime - TurnStartTime;
                if (elapsedTime > turnTimeLimit)
                {
                    EndCurrentTurn();
                }
            }
            
            // Process conversation queue on master client
            if (Object.HasStateAuthority)
            {
                ProcessConversationQueue();
            }
            
            // Update nearby players
            UpdateNearbyPlayers();
        }
        
        public override void Render()
        {
            // Update visual feedback
            UpdateVisualFeedback();
            
            // Update look-at behavior
            UpdateLookAtBehavior();
        }
        
        private void UpdateNearbyPlayers()
        {
            nearbyPlayers.Clear();
            
            var allPlayers = FindObjectsOfType<NetworkVRPlayer>();
            foreach (var player in allPlayers)
            {
                if (player.Object != null)
                {
                    float distance = Vector3.Distance(transform.position, player.transform.position);
                    if (distance <= interactionRange)
                    {
                        nearbyPlayers.Add(player);
                    }
                }
            }
        }
        
        private void UpdateVisualFeedback()
        {
            // Update indicator visibility based on state
            if (listeningIndicator != null)
                listeningIndicator.SetActive(IsListening);
            if (speakingIndicator != null)
                speakingIndicator.SetActive(IsSpeaking);
            if (thinkingIndicator != null)
                thinkingIndicator.SetActive(IsThinking);
            
            // Update material based on interaction state
            var renderer = GetComponent<Renderer>();
            if (renderer != null)
            {
                bool isActive = IsListening || IsSpeaking || IsThinking;
                renderer.material = isActive ? activeMaterial : idleMaterial;
            }
        }
        
        private void UpdateLookAtBehavior()
        {
            // Look at current speaker or nearest player
            Transform targetToLookAt = null;
            
            if (CurrentSpeaker != PlayerRef.None)
            {
                var currentSpeakerObject = Runner.GetPlayerObject(CurrentSpeaker);
                if (currentSpeakerObject != null)
                {
                    targetToLookAt = currentSpeakerObject.transform;
                }
            }
            else if (nearbyPlayers.Count > 0)
            {
                // Look at nearest player
                float closestDistance = float.MaxValue;
                foreach (var player in nearbyPlayers)
                {
                    float distance = Vector3.Distance(transform.position, player.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        targetToLookAt = player.transform;
                    }
                }
            }
            
            // Apply look-at rotation
            if (targetToLookAt != null && lookAtTarget != null)
            {
                Vector3 lookDirection = targetToLookAt.position - lookAtTarget.position;
                lookDirection.y = 0; // Keep it horizontal
                
                if (lookDirection.magnitude > 0.1f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                    lookAtTarget.rotation = Quaternion.Slerp(lookAtTarget.rotation, targetRotation, Time.deltaTime * 2f);
                }
            }
        }
        
        private void Update()
        {
            // Handle local input for voice interaction
            if (localPlayer != null && CanPlayerInteract(localPlayer))
            {
                HandleVoiceInput();
            }
        }
        
        private void HandleVoiceInput()
        {
            // Check for voice input key press
            if (Input.GetKeyDown(voiceInputKey) || GetVRVoiceInput())
            {
                StartVoiceInteraction();
            }
            else if (Input.GetKeyUp(voiceInputKey) || !GetVRVoiceInput())
            {
                if (IsListening && CurrentSpeaker == localPlayer.Object.InputAuthority)
                {
                    EndVoiceInteraction();
                }
            }
        }
        
        private bool GetVRVoiceInput()
        {
            // Check VR controller input for voice activation
            // This could be a button press or voice activation detection
            return false; // Implement based on your VR input system
        }
        
        public void StartVoiceInteraction()
        {
            if (!CanPlayerInteract(localPlayer))
                return;
                
            RPC_StartVoiceInteraction(localPlayer.Object.InputAuthority);
        }
        
        public void EndVoiceInteraction()
        {
            if (CurrentSpeaker != localPlayer.Object.InputAuthority)
                return;
                
            RPC_EndVoiceInteraction();
        }
        
        public void SendTextMessage(string message)
        {
            if (!CanPlayerInteract(localPlayer))
                return;
                
            RPC_SendTextMessage(message, localPlayer.Object.InputAuthority);
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_StartVoiceInteraction(PlayerRef player)
        {
            if (!CanPlayerStartTurn(player))
                return;
                
            CurrentSpeaker = player;
            TurnStartTime = Runner.SimulationTime;
            IsListening = true;
            
            // Start voice recording on ConvAI manager
            if (convAIManager != null)
            {
                convAIManager.StartVoiceRecording();
            }
            
            OnPlayerStartedSpeaking?.Invoke("Voice", player);
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_EndVoiceInteraction()
        {
            if (CurrentSpeaker == PlayerRef.None)
                return;
                
            IsListening = false;
            IsThinking = true;
            
            // Stop voice recording and process
            if (convAIManager != null)
            {
                string playerId = CurrentSpeaker.ToString();
                convAIManager.StopVoiceRecording(playerId);
            }
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_SendTextMessage(string message, PlayerRef player)
        {
            if (!CanPlayerStartTurn(player))
                return;
                
            // Queue the conversation request
            var request = new ConversationRequest
            {
                message = message,
                playerId = player.ToString(),
                isVoice = false,
                timestamp = Runner.SimulationTime
            };
            
            conversationQueue.Enqueue(request);
            
            CurrentSpeaker = player;
            TurnStartTime = Runner.SimulationTime;
            IsThinking = true;
            
            OnPlayerStartedSpeaking?.Invoke(message, player);
        }
        
        private void ProcessConversationQueue()
        {
            if (isProcessingRequest || conversationQueue.Count == 0 || convAIManager == null)
                return;
                
            if (convAIManager.IsProcessing)
                return;
                
            var request = conversationQueue.Dequeue();
            isProcessingRequest = true;
            
            // Send to ConvAI
            convAIManager.SendTextMessage(request.message, request.playerId);
        }
        
        private void OnConvAITextResponse(string response)
        {
            if (Object.HasStateAuthority)
            {
                LastResponse = response;
                IsThinking = false;
                IsSpeaking = true;
                
                // Trigger animation if avatar controller exists
                if (avatarController != null)
                {
                    // Trigger speaking animation
                    TriggerSpeakingAnimation();
                }
                
                OnAIResponseReceived?.Invoke(response);
                
                // End turn after response
                Invoke(nameof(EndCurrentTurn), 2f);
            }
        }
        
        private void OnConvAIVoiceResponse(AudioClip audioClip)
        {
            // Voice response is handled by ConvAI manager's audio source
            // We just need to track when it's finished playing
        }
        
        private void OnConvAIError(string error)
        {
            Debug.LogError($"ConvAI Error: {error}");
            
            if (Object.HasStateAuthority)
            {
                // Reset state on error
                IsListening = false;
                IsThinking = false;
                IsSpeaking = false;
                EndCurrentTurn();
            }
        }
        
        private void EndCurrentTurn()
        {
            CurrentSpeaker = PlayerRef.None;
            TurnStartTime = 0;
            IsListening = false;
            IsThinking = false;
            IsSpeaking = false;
            isProcessingRequest = false;
            
            OnTurnChanged?.Invoke(PlayerRef.None);
        }
        
        private bool CanPlayerInteract(NetworkVRPlayer player)
        {
            if (player == null || player.Object == null)
                return false;
                
            // Check if player is within range
            float distance = Vector3.Distance(transform.position, player.transform.position);
            if (distance > interactionRange)
                return false;
                
            return true;
        }
        
        private bool CanPlayerStartTurn(PlayerRef player)
        {
            // Check if turn-taking is required and someone else is speaking
            if (requireTurnTaking && CurrentSpeaker != PlayerRef.None && CurrentSpeaker != player)
                return false;
                
            // Check if AI is currently processing
            if (IsThinking || convAIManager.IsProcessing)
                return false;
                
            return true;
        }
        
        private void TriggerSpeakingAnimation()
        {
            // Trigger speaking animations on the AI character
            if (avatarController != null && avatarController.AvatarAnimator != null)
            {
                var animator = avatarController.AvatarAnimator;
                animator.SetTrigger("StartSpeaking");
                
                // You could also trigger lip sync or gesture animations here
            }
        }
        
        public void SetCharacter(string characterId)
        {
            if (convAIManager != null)
            {
                convAIManager.SetCharacter(characterId);
            }
        }
        
        public string GetConversationHistory()
        {
            if (convAIManager != null)
            {
                var history = convAIManager.GetConversationHistory();
                return string.Join("\n", history.ConvertAll(msg => $"{msg.role}: {msg.message}"));
            }
            return "";
        }
        
        public bool IsPlayerInRange(NetworkVRPlayer player)
        {
            return nearbyPlayers.Contains(player);
        }
        
        private void OnDrawGizmosSelected()
        {
            // Draw interaction range
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, interactionRange);
            
            // Draw current speaker connection
            if (Application.isPlaying && CurrentSpeaker != PlayerRef.None)
            {
                var speakerObject = Runner?.GetPlayerObject(CurrentSpeaker);
                if (speakerObject != null)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine(transform.position, speakerObject.transform.position);
                }
            }
        }
        
        private void OnDestroy()
        {
            // Unsubscribe from events
            ConvAIManager.OnTextResponse -= OnConvAITextResponse;
            ConvAIManager.OnVoiceResponse -= OnConvAIVoiceResponse;
            ConvAIManager.OnError -= OnConvAIError;
        }
    }
    
    [System.Serializable]
    public struct ConversationRequest
    {
        public string message;
        public string playerId;
        public bool isVoice;
        public float timestamp;
    }
} 