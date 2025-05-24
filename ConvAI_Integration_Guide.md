# ConvAI Integration Guide for VR Multiplayer

This guide explains how to integrate ConvAI (Conversational AI) into your VR multiplayer system, allowing both players to interact with the same AI agent.

## Prerequisites

### Required Packages
```
Install via Unity Package Manager:
- Newtonsoft.Json (com.unity.nuget.newtonsoft-json)
```

### ConvAI Account Setup
1. Sign up at [ConvAI](https://convai.com)
2. Create a new character in ConvAI Studio
3. Get your API Key and Character ID
4. Configure character personality, voice, and knowledge base

## Integration Steps

### 1. Import ConvAI Scripts

All ConvAI scripts are included in the `Assets/Scripts/AI/` folder:
- `ConvAIManager.cs` - Main API integration
- `NetworkConvAICharacter.cs` - Networked AI character
- `VRConvAIVoiceHandler.cs` - VR voice controls
- `VRConvAIUI.cs` - UI management

### 2. Scene Setup

#### Create AI Character GameObject
```
1. Create Empty GameObject: "AICharacter"
2. Position at (0, 0, 2) - in front of spawn area
3. Add Components:
   - NetworkConvAICharacter
   - ConvAIManager
   - VRConvAIVoiceHandler
   - NetworkObject (Fusion)
   - AudioSource
```

#### Configure ConvAI Manager
```
In ConvAIManager component:
- API Key: Your ConvAI API key
- Character ID: Your ConvAI character ID
- Enable Voice Input: true
- Enable Voice Output: true
- Voice Detection Threshold: 0.01
- Max Recording Duration: 10 seconds
```

#### Setup Visual Indicators
```
Create child objects under AICharacter:
- ListeningIndicator (sphere with glowing material)
- SpeakingIndicator (particle system or animated object)
- ThinkingIndicator (rotating object or loading animation)
```

#### Configure Network Settings
```
In NetworkConvAICharacter component:
- Character Name: "AI Assistant"
- Interaction Range: 3 meters
- Require Turn Taking: true
- Turn Time Limit: 30 seconds
- Voice Input Key: T (for desktop testing)
```

### 3. VR Voice Integration

#### Setup Voice Handler
```
In VRConvAIVoiceHandler component:
- Enable Voice Activation: true
- Enable Push To Talk: true
- Voice Activation Threshold: 0.1
- Voice Button: Trigger (right controller trigger)
- Use Left Controller: false
- Provide Tactile Feedback: true
```

#### Create Voice Indicators
```
Add to AICharacter:
- Voice Indicator GameObject (UI element showing recording state)
- Voice Beam (LineRenderer from controller to AI)
- Configure colors for recording/idle states
```

### 4. UI Setup

#### Create ConvAI UI Canvas
```
1. Create UI Canvas: "ConvAICanvas"
2. Set Render Mode: World Space
3. Position: (2, 1.5, 0) - to the side of play area
4. Scale: (0.01, 0.01, 0.01)
5. Add VRConvAIUI component
```

#### UI Panels Structure
```
ConvAICanvas/
├── ConversationPanel/
│   ├── MessageScrollView/
│   │   └── MessageContent
│   ├── InputField
│   ├── SendButton
│   └── VoiceButton
├── SettingsPanel/
│   ├── VoiceActivationToggle
│   ├── PushToTalkToggle
│   ├── VoiceThresholdSlider
│   ├── CharacterDropdown
│   └── ClearHistoryButton
└── StatusPanel/
    ├── StatusText
    ├── VoiceIndicator
    ├── VoiceLevelSlider
    └── CurrentSpeakerText
```

### 5. Network Configuration

#### Add to Network Prefab Registry
```
1. Open Fusion Network Project Config
2. Add NetworkConvAICharacter prefab to registry
3. Assign unique Prefab ID
4. Configure network sync settings
```

#### Spawning AI Character
In your `VRConnectionManager`, add AI character spawning:
```csharp
public void SpawnAICharacter()
{
    if (_runner.HasStateAuthority && aiCharacterPrefab != null)
    {
        Vector3 spawnPos = new Vector3(0, 0, 2);
        _runner.Spawn(aiCharacterPrefab, spawnPos, Quaternion.identity);
    }
}
```

### 6. Testing Setup

#### Desktop Testing
```
Controls:
- T key: Push-to-talk with AI
- Chat UI: Click voice button or type messages
- Settings: Adjust voice threshold and character
```

#### VR Testing
```
Controls:
- Right Trigger: Push-to-talk (hold to record)
- Menu Button: Open/close ConvAI UI
- Hand pointing: Interact with UI elements
- Proximity: Move within 3m of AI character to interact
```

## API Configuration

### ConvAI API Settings
```json
{
  "base_url": "https://api.convai.com",
  "endpoints": {
    "text": "/v1/conversation/text",
    "voice": "/v1/conversation/voice"
  },
  "audio_format": "wav",
  "sample_rate": 44100
}
```

### Request Headers
```
Authorization: Bearer YOUR_API_KEY
Content-Type: application/json (for text) or multipart/form-data (for voice)
```

### Example Text Request
```json
{
  "message": "Hello, how are you?",
  "character_id": "your_character_id",
  "conversation_id": "unique_conversation_id",
  "player_id": "player_1",
  "voice_response": true
}
```

## Advanced Features

### Custom Character Creation
1. Use ConvAI Studio to create characters with specific:
   - Personality traits
   - Knowledge domains
   - Voice characteristics
   - Response styles

### Voice Customization
```csharp
// In ConvAIManager, customize voice settings:
public void ConfigureVoice(string voiceId, float speed, float pitch)
{
    // Add voice configuration to API requests
}
```

### Context Management
```csharp
// Maintain conversation context across sessions:
public void SaveConversationContext()
{
    var context = convAIManager.GetConversationHistory();
    PlayerPrefs.SetString("ConvAI_Context", JsonUtility.ToJson(context));
}
```

### Multi-Language Support
```csharp
// Configure language settings:
public void SetLanguage(string languageCode)
{
    // Add language parameter to API requests
    requestData.language = languageCode;
}
```

## Troubleshooting

### Common Issues

#### API Connection Problems
```
Issue: "ConvAI API Error: 401 Unauthorized"
Solution: Check API key in ConvAIManager settings
```

#### Voice Recording Issues
```
Issue: No microphone input detected
Solution: 
- Check microphone permissions
- Verify microphone device selection
- Test with VRConvAIVoiceHandler.TestMicrophone()
```

#### Network Sync Problems
```
Issue: AI responses not synchronized between players
Solution:
- Ensure NetworkConvAICharacter has NetworkObject
- Check if master client has state authority
- Verify RPC calls are reaching all clients
```

#### Avatar Integration Issues
```
Issue: AI character has no visual representation
Solution:
- Add VRAvatarController to AI character
- Configure Ready Player Me avatar URL
- Setup Final IK for realistic animations
```

### Debug Tools

#### Enable Debug Logging
```csharp
// In ConvAIManager:
[SerializeField] private bool enableDebugLogging = true;
```

#### Voice Level Monitoring
```csharp
// In VRConvAIVoiceHandler:
public float GetCurrentVoiceLevel()
{
    return GetMicrophoneLevel();
}
```

#### Network State Debugging
```csharp
// In NetworkConvAICharacter:
void DebugNetworkState()
{
    Debug.Log($"State Authority: {Object.HasStateAuthority}");
    Debug.Log($"Current Speaker: {CurrentSpeaker}");
    Debug.Log($"AI State: Listening={IsListening}, Speaking={IsSpeaking}");
}
```

## Performance Optimization

### Audio Optimization
- Use 16kHz sample rate for voice (reduces API calls)
- Implement voice activity detection to avoid sending silence
- Cache AI responses for repeated questions

### Network Optimization
- Limit conversation history sync frequency
- Use string compression for long conversations
- Implement distance-based interaction culling

### Mobile VR Considerations
- Reduce audio quality for mobile platforms
- Implement battery-aware voice processing
- Use lightweight UI for better performance

## Security Considerations

### API Key Protection
```csharp
// Don't expose API keys in builds:
#if UNITY_EDITOR
    [SerializeField] private string apiKey = "your_api_key";
#else
    private string apiKey => LoadApiKeyFromSecureStorage();
#endif
```

### Content Filtering
```csharp
// Implement content filtering for inappropriate responses:
public bool IsContentAppropriate(string message)
{
    // Add your content filtering logic
    return !ContainsProfanity(message);
}
```

## Best Practices

### Conversation Design
1. **Turn-taking**: Implement clear turn indicators
2. **Context**: Maintain conversation context across interactions
3. **Feedback**: Provide visual/audio feedback for all states
4. **Fallback**: Handle API failures gracefully

### User Experience
1. **Latency**: Minimize response times with local feedback
2. **Clarity**: Use clear visual indicators for AI states
3. **Accessibility**: Support both voice and text input
4. **Privacy**: Inform users about voice recording

### Technical Implementation
1. **Error Handling**: Implement robust error recovery
2. **State Management**: Synchronize AI state across all clients
3. **Resource Management**: Clean up audio resources properly
4. **Scalability**: Design for multiple AI characters if needed

## Example Integration

Here's a minimal example of how to use the ConvAI system:

```csharp
// In your game manager or player script:
public class GameAIIntegration : MonoBehaviour
{
    private NetworkConvAICharacter aiCharacter;
    
    void Start()
    {
        aiCharacter = FindObjectOfType<NetworkConvAICharacter>();
        if (aiCharacter != null)
        {
            aiCharacter.OnAIResponseReceived += HandleAIResponse;
        }
    }
    
    void HandleAIResponse(string response)
    {
        Debug.Log($"AI said: {response}");
        // Handle AI response in your game logic
    }
    
    public void AskAIAboutGame()
    {
        string question = "What can you tell me about this virtual world?";
        aiCharacter.SendTextMessage(question);
    }
}
```

## Support and Resources

- [ConvAI Documentation](https://docs.convai.com/)
- [Unity Audio Development](https://docs.unity3d.com/Manual/Audio.html)
- [Photon Fusion Networking](https://doc.photonengine.com/fusion/current/)

## Next Steps

After basic integration:
1. Experiment with different AI character personalities
2. Implement gesture-based interactions
3. Add environmental context to conversations
4. Create multi-character AI scenarios
5. Integrate with game mechanics and narrative systems

Your VR multiplayer system now supports conversational AI that both players can interact with simultaneously! 🎉 