using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

namespace VRMultiplayer.AI
{
    /// <summary>
    /// ConvAI Manager for integrating conversational AI into VR multiplayer
    /// Handles API communication with ConvAI service
    /// </summary>
    public class ConvAIManager : MonoBehaviour
    {
        [Header("ConvAI Configuration")]
        [SerializeField] private string apiKey = "";
        [SerializeField] private string characterId = "";
        [SerializeField] private string baseURL = "https://api.convai.com";
        [SerializeField] private bool enableDebugLogging = true;
        
        [Header("Voice Settings")]
        [SerializeField] private bool enableVoiceInput = true;
        [SerializeField] private bool enableVoiceOutput = true;
        [SerializeField] private float voiceDetectionThreshold = 0.01f;
        [SerializeField] private int maxRecordingDuration = 10;
        
        [Header("Conversation Settings")]
        [SerializeField] private string conversationId = "";
        [SerializeField] private bool maintainContext = true;
        [SerializeField] private int maxContextHistory = 10;
        
        // Audio components
        private AudioSource audioSource;
        private AudioClip recordedClip;
        private bool isRecording = false;
        private bool isPlaying = false;
        
        // Conversation state
        private List<ConversationMessage> conversationHistory = new List<ConversationMessage>();
        private bool isProcessing = false;
        
        // Events
        public static event Action<string> OnTextResponse;
        public static event Action<AudioClip> OnVoiceResponse;
        public static event Action<string> OnConversationUpdate;
        public static event Action<string> OnError;
        
        public bool IsProcessing => isProcessing;
        public string ConversationId => conversationId;
        
        private void Start()
        {
            InitializeConvAI();
        }
        
        private void InitializeConvAI()
        {
            // Setup audio source
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f; // 3D audio for VR
            
            // Generate conversation ID if not set
            if (string.IsNullOrEmpty(conversationId))
            {
                conversationId = GenerateConversationId();
            }
            
            // Validate configuration
            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(characterId))
            {
                Debug.LogError("ConvAI: API Key and Character ID must be configured!");
                return;
            }
            
            LogDebug("ConvAI Manager initialized successfully");
        }
        
        private string GenerateConversationId()
        {
            return "conv_" + System.Guid.NewGuid().ToString("N")[..8];
        }
        
        public void SendTextMessage(string message, string playerId = "")
        {
            if (isProcessing)
            {
                LogDebug("ConvAI: Already processing a request");
                return;
            }
            
            StartCoroutine(SendTextMessageCoroutine(message, playerId));
        }
        
        private IEnumerator SendTextMessageCoroutine(string message, string playerId)
        {
            isProcessing = true;
            
            var requestData = new ConvAITextRequest
            {
                message = message,
                character_id = characterId,
                conversation_id = conversationId,
                player_id = playerId,
                voice_response = enableVoiceOutput
            };
            
            string jsonData = JsonConvert.SerializeObject(requestData);
            
            using (UnityWebRequest request = new UnityWebRequest($"{baseURL}/v1/conversation/text", "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    ProcessTextResponse(request.downloadHandler.text);
                }
                else
                {
                    string error = $"ConvAI API Error: {request.error} - {request.downloadHandler.text}";
                    LogDebug(error);
                    OnError?.Invoke(error);
                }
            }
            
            isProcessing = false;
        }
        
        private void ProcessTextResponse(string responseJson)
        {
            try
            {
                var response = JsonConvert.DeserializeObject<ConvAITextResponse>(responseJson);
                
                // Add to conversation history
                AddToHistory("user", response.user_message);
                AddToHistory("character", response.text_response);
                
                // Trigger events
                OnTextResponse?.Invoke(response.text_response);
                OnConversationUpdate?.Invoke(conversationId);
                
                // Handle voice response if available
                if (!string.IsNullOrEmpty(response.audio_response))
                {
                    StartCoroutine(DownloadAndPlayAudio(response.audio_response));
                }
                
                LogDebug($"ConvAI Response: {response.text_response}");
            }
            catch (Exception e)
            {
                string error = $"Error processing ConvAI response: {e.Message}";
                LogDebug(error);
                OnError?.Invoke(error);
            }
        }
        
        private IEnumerator DownloadAndPlayAudio(string audioUrl)
        {
            using (UnityWebRequest audioRequest = UnityWebRequestMultimedia.GetAudioClip(audioUrl, AudioType.WAV))
            {
                yield return audioRequest.SendWebRequest();
                
                if (audioRequest.result == UnityWebRequest.Result.Success)
                {
                    AudioClip audioClip = DownloadHandlerAudioClip.GetContent(audioRequest);
                    OnVoiceResponse?.Invoke(audioClip);
                    PlayAudioResponse(audioClip);
                }
                else
                {
                    LogDebug($"Failed to download audio: {audioRequest.error}");
                }
            }
        }
        
        public void StartVoiceRecording()
        {
            if (!enableVoiceInput || isRecording)
                return;
                
            string microphoneName = Microphone.devices.Length > 0 ? Microphone.devices[0] : null;
            if (microphoneName == null)
            {
                LogDebug("No microphone detected");
                return;
            }
            
            recordedClip = Microphone.Start(microphoneName, false, maxRecordingDuration, 44100);
            isRecording = true;
            
            LogDebug("Started voice recording");
        }
        
        public void StopVoiceRecording(string playerId = "")
        {
            if (!isRecording)
                return;
                
            Microphone.End(null);
            isRecording = false;
            
            if (recordedClip != null)
            {
                StartCoroutine(SendVoiceMessageCoroutine(recordedClip, playerId));
            }
            
            LogDebug("Stopped voice recording");
        }
        
        private IEnumerator SendVoiceMessageCoroutine(AudioClip audioClip, string playerId)
        {
            isProcessing = true;
            
            // Convert audio clip to WAV bytes
            byte[] audioData = ConvertAudioClipToWav(audioClip);
            
            var formData = new List<IMultipartFormSection>
            {
                new MultipartFormDataSection("character_id", characterId),
                new MultipartFormDataSection("conversation_id", conversationId),
                new MultipartFormDataSection("player_id", playerId),
                new MultipartFormFileSection("audio", audioData, "audio.wav", "audio/wav")
            };
            
            using (UnityWebRequest request = UnityWebRequest.Post($"{baseURL}/v1/conversation/voice", formData))
            {
                request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    ProcessTextResponse(request.downloadHandler.text);
                }
                else
                {
                    string error = $"ConvAI Voice API Error: {request.error}";
                    LogDebug(error);
                    OnError?.Invoke(error);
                }
            }
            
            isProcessing = false;
        }
        
        private void PlayAudioResponse(AudioClip audioClip)
        {
            if (audioClip != null && audioSource != null)
            {
                audioSource.clip = audioClip;
                audioSource.Play();
                isPlaying = true;
                
                StartCoroutine(WaitForAudioComplete());
            }
        }
        
        private IEnumerator WaitForAudioComplete()
        {
            while (audioSource.isPlaying)
            {
                yield return null;
            }
            isPlaying = false;
        }
        
        private void AddToHistory(string role, string message)
        {
            if (!maintainContext)
                return;
                
            conversationHistory.Add(new ConversationMessage
            {
                role = role,
                message = message,
                timestamp = DateTime.UtcNow
            });
            
            // Maintain max history size
            if (conversationHistory.Count > maxContextHistory)
            {
                conversationHistory.RemoveAt(0);
            }
        }
        
        public void ClearConversationHistory()
        {
            conversationHistory.Clear();
            conversationId = GenerateConversationId();
            LogDebug("Conversation history cleared");
        }
        
        public List<ConversationMessage> GetConversationHistory()
        {
            return new List<ConversationMessage>(conversationHistory);
        }
        
        public void SetCharacter(string newCharacterId)
        {
            characterId = newCharacterId;
            ClearConversationHistory();
            LogDebug($"Character changed to: {characterId}");
        }
        
        public bool IsAudioPlaying()
        {
            return isPlaying;
        }
        
        private byte[] ConvertAudioClipToWav(AudioClip audioClip)
        {
            // Simple WAV conversion - you might want to use a more robust solution
            float[] samples = new float[audioClip.samples];
            audioClip.GetData(samples, 0);
            
            byte[] intData = new byte[samples.Length * 2];
            int rescaleFactor = 32767;
            
            for (int i = 0; i < samples.Length; i++)
            {
                short intSample = (short)(samples[i] * rescaleFactor);
                byte[] byteArr = BitConverter.GetBytes(intSample);
                intData[i * 2] = byteArr[0];
                intData[i * 2 + 1] = byteArr[1];
            }
            
            return CreateWavHeader(audioClip.frequency, audioClip.channels, intData);
        }
        
        private byte[] CreateWavHeader(int frequency, int channels, byte[] audioData)
        {
            byte[] header = new byte[44];
            
            // RIFF header
            header[0] = 0x52; header[1] = 0x49; header[2] = 0x46; header[3] = 0x46; // "RIFF"
            
            int fileSize = audioData.Length + 36;
            header[4] = (byte)(fileSize & 0xFF);
            header[5] = (byte)((fileSize >> 8) & 0xFF);
            header[6] = (byte)((fileSize >> 16) & 0xFF);
            header[7] = (byte)((fileSize >> 24) & 0xFF);
            
            // WAVE header
            header[8] = 0x57; header[9] = 0x41; header[10] = 0x56; header[11] = 0x45; // "WAVE"
            header[12] = 0x66; header[13] = 0x6D; header[14] = 0x74; header[15] = 0x20; // "fmt "
            
            // fmt chunk size
            header[16] = 16; header[17] = 0; header[18] = 0; header[19] = 0;
            
            // Audio format (PCM)
            header[20] = 1; header[21] = 0;
            
            // Channels
            header[22] = (byte)channels; header[23] = 0;
            
            // Sample rate
            header[24] = (byte)(frequency & 0xFF);
            header[25] = (byte)((frequency >> 8) & 0xFF);
            header[26] = (byte)((frequency >> 16) & 0xFF);
            header[27] = (byte)((frequency >> 24) & 0xFF);
            
            // Byte rate
            int byteRate = frequency * channels * 2;
            header[28] = (byte)(byteRate & 0xFF);
            header[29] = (byte)((byteRate >> 8) & 0xFF);
            header[30] = (byte)((byteRate >> 16) & 0xFF);
            header[31] = (byte)((byteRate >> 24) & 0xFF);
            
            // Block align
            header[32] = (byte)(channels * 2); header[33] = 0;
            
            // Bits per sample
            header[34] = 16; header[35] = 0;
            
            // Data header
            header[36] = 0x64; header[37] = 0x61; header[38] = 0x74; header[39] = 0x61; // "data"
            
            // Data size
            header[40] = (byte)(audioData.Length & 0xFF);
            header[41] = (byte)((audioData.Length >> 8) & 0xFF);
            header[42] = (byte)((audioData.Length >> 16) & 0xFF);
            header[43] = (byte)((audioData.Length >> 24) & 0xFF);
            
            byte[] result = new byte[header.Length + audioData.Length];
            Array.Copy(header, 0, result, 0, header.Length);
            Array.Copy(audioData, 0, result, header.Length, audioData.Length);
            
            return result;
        }
        
        private void LogDebug(string message)
        {
            if (enableDebugLogging)
            {
                Debug.Log($"[ConvAI] {message}");
            }
        }
        
        private void OnDestroy()
        {
            if (isRecording)
            {
                Microphone.End(null);
            }
        }
    }
    
    [Serializable]
    public class ConvAITextRequest
    {
        public string message;
        public string character_id;
        public string conversation_id;
        public string player_id;
        public bool voice_response = true;
    }
    
    [Serializable]
    public class ConvAITextResponse
    {
        public string text_response;
        public string audio_response;
        public string conversation_id;
        public string user_message;
        public string character_id;
    }
    
    [Serializable]
    public class ConversationMessage
    {
        public string role;
        public string message;
        public DateTime timestamp;
    }
} 