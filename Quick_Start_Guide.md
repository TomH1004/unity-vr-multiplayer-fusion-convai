# Quick Start Guide - VR Multiplayer with Photon Fusion & Ready Player Me

This guide helps you get up and running with the VR multiplayer system in the shortest time possible.

## 🚀 Quick Setup (15 minutes)

### 1. Install Required Packages (5 min)

**Via Unity Package Manager:**
```
Window → Package Manager → Add package via URL:
- XR Interaction Toolkit: com.unity.xr.interaction.toolkit
- XR Plugin Management: com.unity.xr.management  
- Oculus XR Plugin: com.unity.xr.oculus
- Ready Player Me SDK: https://github.com/readyplayerme/rpm-unity-sdk-core.git
```

**From Asset Store/External:**
- Download Photon Fusion SDK from [Photon Dashboard](https://dashboard.photonengine.com/)
- Purchase and import Final IK from Unity Asset Store

### 2. Get Required IDs (2 min)

**Photon Fusion App ID:**
1. Sign up at [Photon Dashboard](https://dashboard.photonengine.com/)
2. Create new Fusion app
3. Copy App ID

**Ready Player Me Application ID:**
1. Sign up at [Ready Player Me Studio](https://studio.readyplayer.me/)
2. Create new application
3. Copy Application ID

### 3. Basic Scene Setup (5 min)

1. **Create New Scene:** Choose URP 3D template
2. **Add XR Origin:** GameObject → XR → XR Origin (Mobile AR/VR)
3. **Add Network Manager:**
   ```
   Create Empty GameObject: "NetworkManager"
   Add Components:
   - VRConnectionManager
   - VRMultiplayerBootstrapper
   ```
4. **Create Ground:** 3D Object → Plane, Scale (10,1,10)

### 4. Configure IDs (1 min)

1. **Photon:** Window → Fusion → Realtime Settings → Enter App ID
2. **RPM:** Window → Ready Player Me → Settings → Enter Application ID

### 5. Create Player Prefab (2 min)

1. Create Empty GameObject: "NetworkVRPlayer"
2. Add all scripts from our package:
   - NetworkVRPlayer
   - VRAvatarController  
   - VRLocomotion
   - VRIKSetup
   - NetworkObject (Fusion)
   - CharacterController
3. Save as prefab in Assets/Prefabs/
4. Assign to VRConnectionManager's Player Prefab field

## 🎮 Test Your Setup

### In Unity Editor:
1. Press Play
2. Check Console for "VR Multiplayer System: Initialization complete!"
3. Test avatar loading with a Ready Player Me URL
4. Verify network connection

### With VR Headset:
1. Build to Android (for Quest) or Windows (for PC VR)
2. Test VR tracking
3. Test multiplayer with multiple devices

## 🔧 Common Issues & Fixes

| Issue | Solution |
|-------|----------|
| Avatar won't load | Check RPM Application ID and internet connection |
| VR not working | Verify XR Plugin enabled in Project Settings |
| Network fails | Check Photon App ID and internet connection |
| IK not working | Ensure Final IK imported correctly |
| Hand tracking issues | Check XR Interaction Toolkit setup |

## 📋 Default Controls

### VR Headset:
- **Left Thumbstick:** Move forward/backward/strafe
- **Right Thumbstick:** Snap turn left/right
- **A Button (Right):** Teleport
- **Menu Button:** Open/close menu
- **Grip:** Grab objects
- **Trigger:** Point/interact

### Desktop (Testing):
- **WASD:** Move
- **Mouse:** Look around
- **ESC:** Unlock cursor

## 🎯 Next Steps

Once basic setup works:

1. **Customize Avatars:** Replace default URLs with your Ready Player Me avatars
2. **Build Environment:** Add teleportation areas, interactive objects
3. **Enhanced UI:** Customize the VR menu system
4. **Voice Chat:** Integrate Photon Voice for communication
5. **Gestures:** Implement hand gesture recognition
6. **Animations:** Add custom avatar animations

## 📞 Getting Help

**Check Logs:** Unity Console shows detailed initialization steps
**Debug Mode:** Enable debug logging in VRMultiplayerBootstrapper
**Documentation:** Refer to full setup instructions in Unity_Scene_Setup_Instructions.md

**Common Support Resources:**
- [Photon Fusion Documentation](https://doc.photonengine.com/fusion/v1/)
- [Ready Player Me Unity Docs](https://docs.readyplayer.me/ready-player-me/integration-guides/unity)
- [XR Interaction Toolkit Manual](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@latest/)

## 🚀 Ready to Build!

Your VR multiplayer system is now set up! Players can:
- ✅ Join VR multiplayer rooms
- ✅ Embody Ready Player Me avatars  
- ✅ Use full-body IK with Final IK
- ✅ Interact with VR hand controllers
- ✅ Move via teleportation or smooth locomotion
- ✅ See each other's real-time movements

Start building your VR social experience! 🎉 