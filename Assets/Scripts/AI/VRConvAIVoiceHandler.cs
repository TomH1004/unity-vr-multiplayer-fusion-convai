using UnityEngine;
using UnityEngine.XR;
using VRMultiplayer.VR;
using VRMultiplayer.Network;
using System.Collections.Generic;

namespace VRMultiplayer.AI
{
    /// <summary>
    /// VR ConvAI Voice Handler that integrates voice interaction with VR controllers
    /// Provides voice activity detection and push-to-talk functionality
    /// </summary>
    public class VRConvAIVoiceHandler : MonoBehaviour
    {
        [Header("Voice Settings")]
        [SerializeField] private bool enableVoiceActivation = true;
        [SerializeField] private bool enablePushToTalk = true;
        [SerializeField] private float voiceActivationThreshold = 0.1f;
        [SerializeField] private float voiceActivationSustain = 0.5f;
        [SerializeField] private int audioSampleRate = 44100;
        [SerializeField] private float maxRecordingTime = 15f;
        
        [Header("VR Controls")]
        [SerializeField] private VRButton voiceButton = VRButton.Trigger;
        [SerializeField] private bool useLeftController = false;
        [SerializeField] private bool provideTactileFeedback = true;
        [SerializeField] private float feedbackIntensity = 0.3f;
        
        [Header("Visual Feedback")]
        [SerializeField] private GameObject voiceIndicator;
        [SerializeField] private LineRenderer voiceBeam;
        [SerializeField] private Color recordingColor = Color.red;
        [SerializeField] private Color idleColor = Color.blue;
        
        [Header("Audio Processing")]
        [SerializeField] private float noiseCancellationLevel = 0.05f;
        [SerializeField] private bool enableNoiseGate = true;
        [SerializeField] private float noiseGateThreshold = 0.02f;
        
        // Components
        private VRHandController handController;
        private NetworkConvAICharacter aiCharacter;
        private AudioSource audioSource;
        private AudioClip recordingClip;
        
        // Voice detection state
        private bool isVoiceDetected = false;
        private bool isPushToTalkActive = false;
        private bool isRecording = false;
        private float voiceActivationTimer = 0f;
        private float[] audioSamples;
        private string selectedMicrophone;
        
        // VR input state
        private List<InputDevice> inputDevices = new List<InputDevice>();
        private bool vrControllerFound = false;
        
        // Events
        public System.Action OnVoiceActivationStarted;
        public System.Action OnVoiceActivationEnded;
        public System.Action<float> OnVoiceLevelChanged;
        
        public bool IsRecording => isRecording;
        public bool IsVoiceActive => isVoiceDetected || isPushToTalkActive;
        
        private void Start()
        {
            InitializeVoiceHandler();
            SetupVRInput();
            SetupAudioComponents();
        }
        
        private void InitializeVoiceHandler()
        {
            // Find AI character
            aiCharacter = FindObjectOfType<NetworkConvAICharacter>();
            if (aiCharacter == null)
            {
                Debug.LogWarning("No NetworkConvAICharacter found in scene");
            }
            
            // Setup microphone
            if (Microphone.devices.Length > 0)
            {
                selectedMicrophone = Microphone.devices[0];
                Debug.Log($"Selected microphone: {selectedMicrophone}");
            }
            else
            {
                Debug.LogError("No microphone devices found");
                enableVoiceActivation = false;
            }
            
            // Initialize audio sample buffer
            audioSamples = new float[audioSampleRate];
            
            Debug.Log("VRConvAIVoiceHandler initialized");
        }
        
        private void SetupVRInput()
        {
            // Find appropriate hand controller
            var handControllers = FindObjectsOfType<VRHandController>();
            foreach (var controller in handControllers)
            {
                if (controller.IsLeftHand == useLeftController)
                {
                    handController = controller;
                    break;
                }
            }
            
            if (handController == null)
            {
                Debug.LogWarning($"No {(useLeftController ? "left" : "right")} hand controller found");
            }
        }
        
        private void SetupAudioComponents()
        {
            // Setup audio source for playback
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D for UI feedback
            
            // Setup visual indicators
            if (voiceIndicator != null)
            {
                voiceIndicator.SetActive(false);
            }
            
            if (voiceBeam != null)
            {
                voiceBeam.enabled = false;
                voiceBeam.startWidth = 0.01f;
                voiceBeam.endWidth = 0.005f;
                voiceBeam.positionCount = 2;
            }
        }
        
        private void Update()
        {
            UpdateVRInput();
            UpdateVoiceActivation();
            UpdateVisualFeedback();
        }
        
        private void UpdateVRInput()
        {
            if (handController == null) return;
            
            // Check for push-to-talk button
            bool buttonPressed = handController.GetButtonState(voiceButton);
            
            if (enablePushToTalk)
            {
                if (buttonPressed && !isPushToTalkActive)
                {
                    StartPushToTalk();
                }
                else if (!buttonPressed && isPushToTalkActive)
                {
                    EndPushToTalk();
                }
            }
        }
        
        private void UpdateVoiceActivation()
        {
            if (!enableVoiceActivation || string.IsNullOrEmpty(selectedMicrophone))
                return;
                
            // Check microphone input level
            float currentLevel = GetMicrophoneLevel();
            OnVoiceLevelChanged?.Invoke(currentLevel);
            
            if (currentLevel > voiceActivationThreshold)
            {
                if (!isVoiceDetected)
                {
                    StartVoiceActivation();
                }
                voiceActivationTimer = voiceActivationSustain;
            }
            else
            {
                voiceActivationTimer -= Time.deltaTime;
                if (voiceActivationTimer <= 0f && isVoiceDetected)
                {
                    EndVoiceActivation();
                }
            }
        }
        
        private float GetMicrophoneLevel()
        {
            if (!Microphone.IsRecording(selectedMicrophone))
                return 0f;
                
            // Get current microphone data
            int currentPosition = Microphone.GetPosition(selectedMicrophone);
            if (currentPosition < audioSamples.Length)
                return 0f;
                
            // Get the last chunk of audio data
            recordingClip.GetData(audioSamples, currentPosition - audioSamples.Length);
            
            // Calculate RMS (Root Mean Square) for volume level
            float sum = 0f;
            for (int i = 0; i < audioSamples.Length; i++)
            {
                sum += audioSamples[i] * audioSamples[i];
            }
            
            float rms = Mathf.Sqrt(sum / audioSamples.Length);
            
            // Apply noise cancellation
            if (rms < noiseCancellationLevel)
                rms = 0f;
                
            return rms;
        }
        
        private void StartPushToTalk()
        {
            isPushToTalkActive = true;
            StartVoiceRecording();
            
            // Provide haptic feedback
            if (provideTactileFeedback && handController != null)
            {
                handController.TriggerHapticFeedback(feedbackIntensity, 0.1f);
            }
            
            Debug.Log("Push-to-talk activated");
        }
        
        private void EndPushToTalk()
        {
            isPushToTalkActive = false;
            StopVoiceRecording();
            
            // Provide haptic feedback
            if (provideTactileFeedback && handController != null)
            {
                handController.TriggerHapticFeedback(feedbackIntensity * 0.5f, 0.05f);
            }
            
            Debug.Log("Push-to-talk deactivated");
        }
        
        private void StartVoiceActivation()
        {
            isVoiceDetected = true;
            StartVoiceRecording();
            
            OnVoiceActivationStarted?.Invoke();
            Debug.Log("Voice activation started");
        }
        
        private void EndVoiceActivation()
        {
            isVoiceDetected = false;
            StopVoiceRecording();
            
            OnVoiceActivationEnded?.Invoke();
            Debug.Log("Voice activation ended");
        }
        
        private void StartVoiceRecording()
        {
            if (isRecording || string.IsNullOrEmpty(selectedMicrophone))
                return;
                
            // Start microphone recording
            recordingClip = Microphone.Start(selectedMicrophone, false, (int)maxRecordingTime, audioSampleRate);
            isRecording = true;
            
            // Start voice interaction with AI character
            if (aiCharacter != null)
            {
                aiCharacter.StartVoiceInteraction();
            }
            
            Debug.Log("Voice recording started");
        }
        
        private void StopVoiceRecording()
        {
            if (!isRecording)
                return;
                
            // Stop microphone recording
            Microphone.End(selectedMicrophone);
            isRecording = false;
            
            // Process the recorded audio
            if (recordingClip != null && aiCharacter != null)
            {
                ProcessRecordedAudio();
            }
            
            // End voice interaction with AI character
            if (aiCharacter != null)
            {
                aiCharacter.EndVoiceInteraction();
            }
            
            Debug.Log("Voice recording stopped");
        }
        
        private void ProcessRecordedAudio()
        {
            // Apply noise gate if enabled
            if (enableNoiseGate)
            {
                ApplyNoiseGate(recordingClip);
            }
            
            // Here you could apply additional audio processing
            // such as noise reduction, echo cancellation, etc.
        }
        
        private void ApplyNoiseGate(AudioClip clip)
        {
            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);
            
            for (int i = 0; i < samples.Length; i++)
            {
                if (Mathf.Abs(samples[i]) < noiseGateThreshold)
                {
                    samples[i] = 0f;
                }
            }
            
            clip.SetData(samples, 0);
        }
        
        private void UpdateVisualFeedback()
        {
            bool isActive = IsVoiceActive;
            
            // Update voice indicator
            if (voiceIndicator != null)
            {
                voiceIndicator.SetActive(isActive);
            }
            
            // Update voice beam to AI character
            if (voiceBeam != null && aiCharacter != null)
            {
                voiceBeam.enabled = isActive;
                
                if (isActive && handController != null)
                {
                    voiceBeam.SetPosition(0, handController.transform.position);
                    voiceBeam.SetPosition(1, aiCharacter.transform.position);
                    voiceBeam.material.color = isRecording ? recordingColor : idleColor;
                }
            }
        }
        
        public void SetVoiceActivationEnabled(bool enabled)
        {
            enableVoiceActivation = enabled;
            if (!enabled && isVoiceDetected)
            {
                EndVoiceActivation();
            }
        }
        
        public void SetPushToTalkEnabled(bool enabled)
        {
            enablePushToTalk = enabled;
            if (!enabled && isPushToTalkActive)
            {
                EndPushToTalk();
            }
        }
        
        public void SetVoiceActivationThreshold(float threshold)
        {
            voiceActivationThreshold = Mathf.Clamp01(threshold);
        }
        
        public void SetVoiceButton(VRButton button)
        {
            voiceButton = button;
        }
        
        public void SwitchHandController(bool useLeft)
        {
            useLeftController = useLeft;
            SetupVRInput();
        }
        
        public float GetCurrentVoiceLevel()
        {
            return GetMicrophoneLevel();
        }
        
        public bool IsMicrophoneAvailable()
        {
            return !string.IsNullOrEmpty(selectedMicrophone) && Microphone.devices.Length > 0;
        }
        
        public void TestMicrophone()
        {
            if (IsMicrophoneAvailable())
            {
                StartCoroutine(TestMicrophoneCoroutine());
            }
        }
        
        /// <summary>
        /// Manually start voice recording (public interface for UI)
        /// </summary>
        public void StartRecording()
        {
            StartVoiceRecording();
        }
        
        /// <summary>
        /// Manually stop voice recording (public interface for UI)
        /// </summary>
        public void StopRecording()
        {
            StopVoiceRecording();
        }
        
        private System.Collections.IEnumerator TestMicrophoneCoroutine()
        {
            Debug.Log("Testing microphone...");
            
            AudioClip testClip = Microphone.Start(selectedMicrophone, false, 3, audioSampleRate);
            yield return new WaitForSeconds(3f);
            Microphone.End(selectedMicrophone);
            
            if (testClip != null)
            {
                audioSource.clip = testClip;
                audioSource.Play();
                Debug.Log("Microphone test completed - playing back recording");
            }
        }
        
        private void OnDrawGizmosSelected()
        {
            // Draw voice activation range
            if (handController != null)
            {
                Gizmos.color = IsVoiceActive ? Color.red : Color.blue;
                Gizmos.DrawWireSphere(handController.transform.position, 0.1f);
                
                // Draw connection to AI character
                if (aiCharacter != null)
                {
                    Gizmos.DrawLine(handController.transform.position, aiCharacter.transform.position);
                }
            }
        }
        
        private void OnDestroy()
        {
            // Stop any ongoing recording
            if (isRecording)
            {
                Microphone.End(selectedMicrophone);
            }
        }
    }
} 