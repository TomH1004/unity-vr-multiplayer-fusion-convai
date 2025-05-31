using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VRMultiplayer.AI;
using VRMultiplayer.Network;
using System.Collections.Generic;
using System.Collections;

namespace VRMultiplayer.UI
{
    /// <summary>
    /// VR ConvAI UI for managing AI conversations in VR multiplayer environment
    /// Provides text input/output, conversation history, and voice controls
    /// </summary>
    public class VRConvAIUI : MonoBehaviour
    {
        [Header("UI Panels")]
        [SerializeField] private GameObject conversationPanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject historyPanel;
        
        [Header("Conversation UI")]
        [SerializeField] private ScrollRect conversationScrollRect;
        [SerializeField] private Transform conversationContent;
        [SerializeField] private GameObject messagePrefab;
        [SerializeField] private TMP_InputField messageInputField;
        [SerializeField] private Button sendButton;
        [SerializeField] private Button voiceButton;
        
        [Header("Status UI")]
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Image voiceIndicator;
        [SerializeField] private Slider voiceLevelSlider;
        [SerializeField] private TextMeshProUGUI currentSpeakerText;
        
        [Header("Settings UI")]
        [SerializeField] private Toggle voiceActivationToggle;
        [SerializeField] private Toggle pushToTalkToggle;
        [SerializeField] private Slider voiceThresholdSlider;
        [SerializeField] private TMP_Dropdown characterDropdown;
        [SerializeField] private Button clearHistoryButton;
        
        [Header("Visual Feedback")]
        [SerializeField] private Color userMessageColor = Color.blue;
        [SerializeField] private Color aiMessageColor = Color.green;
        [SerializeField] private Color systemMessageColor = Color.yellow;
        [SerializeField] private Color voiceActiveColor = Color.red;
        [SerializeField] private Color voiceInactiveColor = Color.gray;
        
        [Header("Animation")]
        [SerializeField] private float messageAnimationDuration = 0.3f;
        [SerializeField] private AnimationCurve messageAnimationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        // Components
        private NetworkConvAICharacter aiCharacter;
        private VRConvAIVoiceHandler voiceHandler;
        private NetworkVRPlayer localPlayer;
        
        // UI state
        private List<ConversationMessageUI> messageList = new List<ConversationMessageUI>();
        private bool isUIVisible = false;
        private string[] availableCharacters = { "Assistant", "Guide", "Companion", "Expert" };
        
        // Events
        public System.Action<bool> OnUIVisibilityChanged;
        public System.Action<string> OnMessageSent;
        
        public bool IsVisible => isUIVisible;
        
        private void Start()
        {
            InitializeUI();
            SetupEventListeners();
            FindComponents();
        }
        
        private void InitializeUI()
        {
            // Setup initial UI state
            if (conversationPanel != null) conversationPanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (historyPanel != null) historyPanel.SetActive(false);
            
            // Initialize input field
            if (messageInputField != null)
            {
                messageInputField.text = "";
                messageInputField.onEndEdit.AddListener(OnMessageInputEndEdit);
            }
            
            // Initialize character dropdown
            if (characterDropdown != null)
            {
                characterDropdown.options.Clear();
                foreach (string character in availableCharacters)
                {
                    characterDropdown.options.Add(new TMP_Dropdown.OptionData(character));
                }
                characterDropdown.value = 0;
                characterDropdown.onValueChanged.AddListener(OnCharacterChanged);
            }
            
            // Initialize voice controls
            if (voiceThresholdSlider != null)
            {
                voiceThresholdSlider.value = 0.1f;
                voiceThresholdSlider.onValueChanged.AddListener(OnVoiceThresholdChanged);
            }
            
            SetUIVisibility(false);
        }
        
        private void SetupEventListeners()
        {
            // Button listeners
            if (sendButton != null)
                sendButton.onClick.AddListener(SendTextMessage);
            if (voiceButton != null)
                voiceButton.onClick.AddListener(ToggleVoiceRecording);
            if (clearHistoryButton != null)
                clearHistoryButton.onClick.AddListener(ClearConversationHistory);
            
            // Toggle listeners
            if (voiceActivationToggle != null)
                voiceActivationToggle.onValueChanged.AddListener(OnVoiceActivationToggled);
            if (pushToTalkToggle != null)
                pushToTalkToggle.onValueChanged.AddListener(OnPushToTalkToggled);
            
            // ConvAI event listeners
            ConvAIManager.OnTextResponse += OnAITextResponse;
            ConvAIManager.OnVoiceResponse += OnAIVoiceResponse;
            ConvAIManager.OnError += OnAIError;
        }
        
        private void FindComponents()
        {
            // Find AI character
            aiCharacter = FindObjectOfType<NetworkConvAICharacter>();
            if (aiCharacter != null)
            {
                aiCharacter.OnPlayerStartedSpeaking += OnPlayerStartedSpeaking;
                aiCharacter.OnAIResponseReceived += OnAIResponseReceived;
                aiCharacter.OnTurnChanged += OnTurnChanged;
            }
            
            // Find voice handler
            voiceHandler = FindObjectOfType<VRConvAIVoiceHandler>();
            if (voiceHandler != null)
            {
                voiceHandler.OnVoiceActivationStarted += OnVoiceActivationStarted;
                voiceHandler.OnVoiceActivationEnded += OnVoiceActivationEnded;
                voiceHandler.OnVoiceLevelChanged += OnVoiceLevelChanged;
            }
            
            // Find local player
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
        
        public void ToggleUI()
        {
            SetUIVisibility(!isUIVisible);
        }
        
        public void SetUIVisibility(bool visible)
        {
            isUIVisible = visible;
            
            if (conversationPanel != null)
                conversationPanel.SetActive(visible);
                
            OnUIVisibilityChanged?.Invoke(visible);
            
            // Focus input field when opening
            if (visible && messageInputField != null)
            {
                messageInputField.Select();
                messageInputField.ActivateInputField();
            }
        }
        
        private void SendTextMessage()
        {
            if (messageInputField == null || string.IsNullOrEmpty(messageInputField.text.Trim()))
                return;
                
            string message = messageInputField.text.Trim();
            messageInputField.text = "";
            
            // Send to AI character
            if (aiCharacter != null)
            {
                aiCharacter.SendTextMessage(message);
            }
            
            // Add to UI
            AddMessageToUI("You", message, userMessageColor, true);
            
            OnMessageSent?.Invoke(message);
        }
        
        private void ToggleVoiceRecording()
        {
            if (voiceHandler == null) return;
            
            if (voiceHandler.IsRecording)
            {
                voiceHandler.StopRecording();
            }
            else
            {
                voiceHandler.StartRecording();
            }
        }
        
        private void AddMessageToUI(string sender, string message, Color color, bool isUser = false)
        {
            if (messagePrefab == null || conversationContent == null)
                return;
                
            GameObject messageObj = Instantiate(messagePrefab, conversationContent);
            ConversationMessageUI messageUI = messageObj.GetComponent<ConversationMessageUI>();
            
            if (messageUI == null)
            {
                messageUI = messageObj.AddComponent<ConversationMessageUI>();
            }
            
            messageUI.Setup(sender, message, color, isUser);
            messageList.Add(messageUI);
            
            // Animate message appearance
            StartCoroutine(AnimateMessageAppearance(messageUI));
            
            // Scroll to bottom
            StartCoroutine(ScrollToBottom());
        }
        
        private IEnumerator AnimateMessageAppearance(ConversationMessageUI messageUI)
        {
            Transform messageTransform = messageUI.transform;
            Vector3 originalScale = messageTransform.localScale;
            messageTransform.localScale = Vector3.zero;
            
            float elapsed = 0f;
            while (elapsed < messageAnimationDuration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / messageAnimationDuration;
                float curveValue = messageAnimationCurve.Evaluate(progress);
                
                messageTransform.localScale = Vector3.Lerp(Vector3.zero, originalScale, curveValue);
                yield return null;
            }
            
            messageTransform.localScale = originalScale;
        }
        
        private IEnumerator ScrollToBottom()
        {
            yield return new WaitForEndOfFrame();
            
            if (conversationScrollRect != null)
            {
                conversationScrollRect.normalizedPosition = new Vector2(0, 0);
            }
        }
        
        // Event handlers
        private void OnMessageInputEndEdit(string text)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                SendTextMessage();
            }
        }
        
        private void OnCharacterChanged(int characterIndex)
        {
            if (characterIndex >= 0 && characterIndex < availableCharacters.Length && aiCharacter != null)
            {
                string characterId = availableCharacters[characterIndex].ToLower();
                aiCharacter.SetCharacter(characterId);
                
                AddMessageToUI("System", $"Changed character to {availableCharacters[characterIndex]}", systemMessageColor);
            }
        }
        
        private void OnVoiceThresholdChanged(float threshold)
        {
            if (voiceHandler != null)
            {
                voiceHandler.SetVoiceActivationThreshold(threshold);
            }
        }
        
        private void OnVoiceActivationToggled(bool enabled)
        {
            if (voiceHandler != null)
            {
                voiceHandler.SetVoiceActivationEnabled(enabled);
            }
        }
        
        private void OnPushToTalkToggled(bool enabled)
        {
            if (voiceHandler != null)
            {
                voiceHandler.SetPushToTalkEnabled(enabled);
            }
        }
        
        private void ClearConversationHistory()
        {
            // Clear UI messages
            foreach (var messageUI in messageList)
            {
                if (messageUI != null && messageUI.gameObject != null)
                {
                    Destroy(messageUI.gameObject);
                }
            }
            messageList.Clear();
            
            // Clear ConvAI history
            var convAIManager = FindObjectOfType<ConvAIManager>();
            if (convAIManager != null)
            {
                convAIManager.ClearConversationHistory();
            }
            
            AddMessageToUI("System", "Conversation history cleared", systemMessageColor);
        }
        
        // ConvAI event handlers
        private void OnAITextResponse(string response)
        {
            string characterName = characterDropdown != null && characterDropdown.value < availableCharacters.Length
                ? availableCharacters[characterDropdown.value]
                : "AI";
                
            AddMessageToUI(characterName, response, aiMessageColor);
        }
        
        private void OnAIVoiceResponse(AudioClip audioClip)
        {
            // Voice response is handled by the audio system
            // We could add visual indicators here
            UpdateStatusText("AI is speaking...");
        }
        
        private void OnAIError(string error)
        {
            AddMessageToUI("System", $"Error: {error}", Color.red);
        }
        
        // Character event handlers
        private void OnPlayerStartedSpeaking(string message, Fusion.PlayerRef player)
        {
            if (currentSpeakerText != null)
            {
                currentSpeakerText.text = $"Speaking: Player {player}";
            }
            
            if (!string.IsNullOrEmpty(message) && message != "Voice")
            {
                AddMessageToUI($"Player {player}", message, userMessageColor);
            }
        }
        
        private void OnAIResponseReceived(string response)
        {
            UpdateStatusText("AI responded");
        }
        
        private void OnTurnChanged(Fusion.PlayerRef player)
        {
            if (currentSpeakerText != null)
            {
                currentSpeakerText.text = player != Fusion.PlayerRef.None ? $"Speaking: Player {player}" : "No one speaking";
            }
        }
        
        // Voice handler event handlers
        private void OnVoiceActivationStarted()
        {
            if (voiceIndicator != null)
            {
                voiceIndicator.color = voiceActiveColor;
            }
            UpdateStatusText("Voice activated");
        }
        
        private void OnVoiceActivationEnded()
        {
            if (voiceIndicator != null)
            {
                voiceIndicator.color = voiceInactiveColor;
            }
            UpdateStatusText("Voice deactivated");
        }
        
        private void OnVoiceLevelChanged(float level)
        {
            if (voiceLevelSlider != null)
            {
                voiceLevelSlider.value = level;
            }
        }
        
        private void UpdateStatusText(string text)
        {
            if (statusText != null)
            {
                statusText.text = text;
                StartCoroutine(ClearStatusTextAfterDelay(3f));
            }
        }
        
        private IEnumerator ClearStatusTextAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (statusText != null)
            {
                statusText.text = "";
            }
        }
        
        public void ShowSettingsPanel()
        {
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(true);
            }
        }
        
        public void HideSettingsPanel()
        {
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(false);
            }
        }
        
        public void ShowHistoryPanel()
        {
            if (historyPanel != null)
            {
                historyPanel.SetActive(true);
                DisplayConversationHistory();
            }
        }
        
        public void HideHistoryPanel()
        {
            if (historyPanel != null)
            {
                historyPanel.SetActive(false);
            }
        }
        
        private void DisplayConversationHistory()
        {
            if (aiCharacter != null)
            {
                string history = aiCharacter.GetConversationHistory();
                // Display history in a text area or similar UI element
            }
        }
        
        private void OnDestroy()
        {
            // Unsubscribe from events
            ConvAIManager.OnTextResponse -= OnAITextResponse;
            ConvAIManager.OnVoiceResponse -= OnAIVoiceResponse;
            ConvAIManager.OnError -= OnAIError;
            
            if (aiCharacter != null)
            {
                aiCharacter.OnPlayerStartedSpeaking -= OnPlayerStartedSpeaking;
                aiCharacter.OnAIResponseReceived -= OnAIResponseReceived;
                aiCharacter.OnTurnChanged -= OnTurnChanged;
            }
            
            if (voiceHandler != null)
            {
                voiceHandler.OnVoiceActivationStarted -= OnVoiceActivationStarted;
                voiceHandler.OnVoiceActivationEnded -= OnVoiceActivationEnded;
                voiceHandler.OnVoiceLevelChanged -= OnVoiceLevelChanged;
            }
        }
    }
    
    /// <summary>
    /// UI component for individual conversation messages
    /// </summary>
    public class ConversationMessageUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI senderText;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Image backgroundImage;
        
        public void Setup(string sender, string message, Color color, bool isUser)
        {
            if (senderText != null)
                senderText.text = sender;
            if (messageText != null)
                messageText.text = message;
            if (backgroundImage != null)
                backgroundImage.color = color;
                
            // Adjust layout for user vs AI messages
            if (isUser)
            {
                // Align to right for user messages
                var rectTransform = GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    rectTransform.anchorMin = new Vector2(0.2f, 0);
                    rectTransform.anchorMax = new Vector2(1f, 1);
                }
            }
        }
    }
} 