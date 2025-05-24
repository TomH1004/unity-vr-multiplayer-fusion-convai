# Unity VR Multiplayer with Photon Fusion, Ready Player Me & Final IK

This project sets up a Unity VR multiplayer experience using:
- **Photon Fusion** (Shared Mode)
- **Ready Player Me** avatars
- **Final IK** for realistic VR body tracking
- **XR Interaction Toolkit** for VR interactions

## Prerequisites

### Unity Version
- Unity 2022.3 LTS or newer
- Universal Render Pipeline (URP)

### Required Packages
Install these packages via Unity Package Manager:

1. **Photon Fusion SDK**
   - Download from [Photon Dashboard](https://dashboard.photonengine.com/)
   - Import `Fusion2.unitypackage`

2. **Ready Player Me Unity SDK**
   ```
   https://github.com/readyplayerme/rpm-unity-sdk-core.git
   ```

3. **Final IK**
   - Available on Unity Asset Store
   - Required for realistic VR body tracking

4. **XR Interaction Toolkit**
   ```
   com.unity.xr.interaction.toolkit
   ```

5. **XR Plugin Management**
   ```
   com.unity.xr.management
   ```

6. **Oculus XR Plugin** (for Meta Quest)
   ```
   com.unity.xr.oculus
   ```

## Setup Instructions

### 1. Project Setup
1. Create new Unity 3D URP project
2. Install all required packages listed above
3. Set up XR settings:
   - Go to **Edit > Project Settings > XR Plug-in Management**
   - Enable **Oculus** provider
   - Configure **XR Interaction Toolkit** settings

### 2. Photon Fusion Setup
1. Create Photon account and get App ID
2. Go to **Fusion > Realtime Settings**
3. Enter your App ID
4. Set **App ID Fusion** field

### 3. Ready Player Me Setup
1. Create Ready Player Me account at [readyplayer.me](https://readyplayer.me/)
2. Get your Application ID from RPM Studio
3. Configure in **Ready Player Me > Settings**

### 4. ConvAI Setup (Optional)
1. Create ConvAI account at [convai.com](https://convai.com)
2. Create AI character and get API Key + Character ID
3. Configure in ConvAIManager component
4. See `ConvAI_Integration_Guide.md` for detailed setup

### 5. Scene Setup
1. Create new scene named "VRMultiplayerScene"
2. Add the provided prefabs and scripts
3. Configure XR Origin with interaction setup
4. Set up network spawn points

### 6. Build Settings
1. Add scenes to build settings
2. Set platform to **Android** for Quest builds
3. Configure player settings for VR

## Project Structure

```
Assets/
├── Scripts/
│   ├── Network/
│   │   ├── NetworkVRPlayer.cs
│   │   ├── VRConnectionManager.cs
│   │   └── VRInputProvider.cs
│   ├── Avatar/
│   │   ├── VRAvatarController.cs
│   │   ├── VRIKSetup.cs
│   │   └── AvatarAnimationHelper.cs
│   ├── VR/
│   │   ├── VRHandController.cs
│   │   └── VRLocomotion.cs
│   ├── AI/
│   │   ├── ConvAIManager.cs
│   │   ├── NetworkConvAICharacter.cs
│   │   └── VRConvAIVoiceHandler.cs
│   └── UI/
│       ├── VRMenuManager.cs
│       └── VRConvAIUI.cs
├── Prefabs/
│   ├── NetworkVRPlayer.prefab
│   ├── VRAvatar.prefab
│   └── NetworkManager.prefab
└── Scenes/
    └── VRMultiplayerScene.unity
```

## Key Features

- **VR Body Tracking**: Full body representation in VR using Final IK
- **Avatar Customization**: Ready Player Me avatar integration
- **Hand Tracking**: Natural hand movements and interactions
- **Conversational AI**: ConvAI integration for interactive AI characters
- **Voice Spatial Audio**: 3D positional voice chat
- **Cross-Platform**: Support for various VR headsets

## Testing

1. Build and deploy to multiple VR devices
2. Test local multiplayer with multiple headsets
3. Verify avatar synchronization and IK solving
4. Test voice chat and interactions

## Troubleshooting

### Common Issues:
- **Avatar not loading**: Check RPM Application ID and internet connection
- **IK solving issues**: Verify Final IK setup and bone assignments
- **Network sync problems**: Check Photon Fusion tick rate and interpolation settings
- **VR tracking issues**: Ensure proper XR Origin setup and room-scale tracking

## Support Resources

- [Photon Fusion Documentation](https://doc.photonengine.com/fusion/v1/)
- [Ready Player Me Unity SDK](https://docs.readyplayer.me/ready-player-me/integration-guides/unity)
- [Final IK Documentation](https://assetstore.unity.com/packages/tools/animation/final-ik-14290)
- [XR Interaction Toolkit](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@latest/) 