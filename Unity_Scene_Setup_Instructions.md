# Unity Scene Setup Instructions

This document provides step-by-step instructions for setting up the VR Multiplayer scene in Unity with Photon Fusion, Ready Player Me, and Final IK.

## Prerequisites Checklist

Before starting, ensure you have installed:
- [x] Unity 2022.3 LTS or newer
- [x] Universal Render Pipeline (URP)
- [x] Photon Fusion SDK (imported)
- [x] Ready Player Me Unity SDK (via Package Manager)
- [x] Final IK (from Asset Store)
- [x] XR Interaction Toolkit (via Package Manager)
- [x] XR Plugin Management (via Package Manager)
- [x] Oculus XR Plugin (via Package Manager)

## Scene Setup Process

### 1. Create Base Scene Structure

1. **Create New Scene**
   - File → New Scene
   - Choose "3D Core (URP)" template
   - Save as `VRMultiplayerScene.unity`

2. **Setup Lighting**
   - Window → Rendering → Lighting
   - Set Environment Lighting Source to "Color"
   - Set Environment Color to light gray (#C0C0C0)
   - Disable Auto Generate lighting
   - Click "Generate Lighting"

### 2. Create XR Origin Setup

1. **Add XR Origin**
   - GameObject → XR → XR Origin (Mobile AR/VR)
   - Position at (0, 0, 0)
   - Ensure it has Camera Offset, Main Camera, and Controller objects

2. **Configure XR Origin**
   - Select XR Origin
   - In XR Origin component:
     - Set Camera Floor Offset Object to "Camera Offset"
     - Set Camera Y Offset to 1.36 (average eye height)
     - Enable "Track Origin"

3. **Setup VR Controllers**
   - Expand XR Origin → Camera Offset
   - Add child objects: "LeftHand Controller" and "RightHand Controller"
   - For each controller:
     - Add XR Controller component
     - Add VRHandController script (from our scripts)
     - Set appropriate Controller Node (LeftHand/RightHand)

### 3. Create Network Manager

1. **Create Network Manager GameObject**
   ```
   Create Empty GameObject: "NetworkManager"
   Position: (0, 0, 0)
   Add Components:
   - VRConnectionManager
   - VRInputProvider
   ```

2. **Configure VRConnectionManager**
   - Set Max Players: 8
   - Set Room Name: "VRRoom"
   - Create spawn points (see step 4)

### 4. Create Player Spawn Points

1. **Create Spawn Points Container**
   ```
   Create Empty GameObject: "SpawnPoints"
   Position: (0, 0, 0)
   ```

2. **Add Individual Spawn Points**
   ```
   Create 8 child objects under SpawnPoints:
   - SpawnPoint_01: Position (2, 0, 2)
   - SpawnPoint_02: Position (-2, 0, 2)
   - SpawnPoint_03: Position (2, 0, -2)
   - SpawnPoint_04: Position (-2, 0, -2)
   - SpawnPoint_05: Position (4, 0, 0)
   - SpawnPoint_06: Position (-4, 0, 0)
   - SpawnPoint_07: Position (0, 0, 4)
   - SpawnPoint_08: Position (0, 0, -4)
   ```

3. **Assign Spawn Points**
   - Select NetworkManager
   - In VRConnectionManager component
   - Drag all spawn points to "Spawn Points" array

### 5. Create Network VR Player Prefab

1. **Create Player Prefab Structure**
   ```
   Create Empty GameObject: "NetworkVRPlayer"
   Add Components:
   - NetworkVRPlayer script
   - VRLocomotion script
   - VRAvatarController script
   - VRIKSetup script
   - CharacterController
   - NetworkObject (from Fusion)
   ```

2. **Configure NetworkVRPlayer**
   - Set Default Avatar URL (get from Ready Player Me)
   - Configure movement speeds and IK settings
   - Set Position Lerp Rate: 15
   - Set Rotation Lerp Rate: 15

3. **Create Hand Controllers for Prefab**
   ```
   Under NetworkVRPlayer, create:
   - LeftHandController (with VRHandController script, IsLeftHand = true)
   - RightHandController (with VRHandController script, IsLeftHand = false)
   ```

4. **Create Avatar Container**
   ```
   Under NetworkVRPlayer, create:
   - AvatarContainer (empty GameObject for avatar placement)
   ```

5. **Save as Prefab**
   - Drag NetworkVRPlayer to Assets/Prefabs/
   - Delete from scene
   - Assign prefab to VRConnectionManager's "Player Prefab" field

### 6. Setup Environment

1. **Create Ground Plane**
   ```
   Create 3D Object → Plane
   Name: "Ground"
   Position: (0, 0, 0)
   Scale: (10, 1, 10)
   Material: Create new material with URP/Lit shader
   ```

2. **Add Teleportation Areas**
   ```
   Create multiple Plane objects for teleportation:
   - Scale: (2, 1, 2)
   - Positions: Various around the scene
   - Layer: Set to "Teleportation" layer
   - Add Teleportation Area component (from XR Toolkit)
   ```

3. **Add Interactive Objects (Optional)**
   ```
   Create some cubes/spheres with:
   - XR Grab Interactable component
   - Rigidbody component
   - Collider component
   ```

### 7. Setup UI Canvas

1. **Create UI Canvas**
   ```
   Create UI → Canvas
   Name: "VRUICanvas"
   Canvas component settings:
   - Render Mode: World Space
   - Position: (0, 2, 2)
   - Scale: (0.01, 0.01, 0.01)
   ```

2. **Add VRMenuManager**
   - Add VRMenuManager script to Canvas
   - Create UI panels as children:
     - MainMenuPanel
     - AvatarSelectionPanel
     - RoomPanel
     - SettingsPanel

3. **Setup UI Elements**
   - Add buttons, input fields, and text elements
   - Assign UI elements to VRMenuManager script fields
   - Add XR Canvas interaction components

### 8. Configure Photon Fusion

1. **Fusion Network Project Config**
   - Window → Fusion → Network Project Config
   - Create new config asset
   - Set Tick Rate: 30
   - Set Simulation Mode: Shared

2. **Setup Network Prefab Registry**
   - Add NetworkVRPlayer prefab to registry
   - Assign unique Prefab ID

### 9. Configure Ready Player Me

1. **Ready Player Me Settings**
   - Window → Ready Player Me → Settings
   - Enter your Application ID from RPM Studio
   - Configure avatar loading settings

2. **Test Avatar URLs**
   - Replace placeholder URLs in scripts with real RPM avatar URLs
   - Test loading in Play mode

### 10. Final IK Configuration

1. **VRIK Setup**
   - The VRIKSetup script handles automatic configuration
   - Verify bone mapping works with RPM avatars
   - Adjust IK weights in inspector if needed

### 11. XR Interaction Setup

1. **Interaction Manager**
   ```
   Create Empty GameObject: "XR Interaction Manager"
   Add Component: XR Interaction Manager
   ```

2. **Configure Interaction Layers**
   - Create interaction layers in Project Settings:
     - Default
     - UI
     - Teleportation
     - Hands

### 12. Audio Setup (Optional)

1. **Spatial Audio**
   ```
   Add Audio Source to NetworkVRPlayer prefab:
   - 3D Spatial Blend
   - Doppler Level: 0.5
   - Volume Rolloff: Logarithmic
   ```

## Testing Checklist

Before building, test the following in Unity Editor:

- [ ] VR headset tracking works
- [ ] Hand controllers are detected and tracked
- [ ] Movement (teleportation and smooth locomotion) works
- [ ] Avatar loads from Ready Player Me URL
- [ ] IK solver correctly maps head and hands to avatar
- [ ] Network connection can be established
- [ ] Multiple players can join the same room
- [ ] Avatar synchronization works across clients
- [ ] Hand gestures and animations work
- [ ] UI interactions work in VR

## Build Settings

### For Meta Quest/Quest 2:

1. **Player Settings**
   - Platform: Android
   - Graphics API: OpenGLES3
   - Scripting Backend: IL2CPP
   - Target API Level: Automatic (highest installed)

2. **XR Settings**
   - Initialize XR on Startup: Enabled
   - XR Management: Oculus

3. **Quality Settings**
   - Use URP settings optimized for mobile VR
   - Disable unnecessary post-processing

## Troubleshooting Common Issues

### Avatar Loading Issues
- Verify internet connection
- Check Ready Player Me Application ID
- Ensure avatar URLs are accessible
- Check console for RPM loading errors

### IK Problems
- Verify Final IK is properly imported
- Check avatar bone structure compatibility
- Adjust IK weights in VRIKSetup script

### Network Sync Issues
- Check Photon Fusion App ID
- Verify NetworkObject components
- Check input gathering in VRInputProvider

### VR Tracking Issues
- Ensure XR Origin is properly configured
- Check VR headset drivers and software
- Verify controller pairing

## Performance Optimization

1. **Avatar Optimization**
   - Use RPM avatar LOD system
   - Limit bone count for IK
   - Optimize textures for mobile

2. **Network Optimization**
   - Reduce update frequency for non-critical data
   - Use compression for transform data
   - Implement distance-based updates

3. **Rendering Optimization**
   - Use URP mobile renderer
   - Enable GPU instancing
   - Optimize lighting and shadows

## Next Steps

After basic setup:
1. Customize avatar selection UI
2. Add voice chat integration
3. Implement hand gesture recognition
4. Add object interaction systems
5. Create custom environments
6. Add game-specific features

Remember to test thoroughly on actual VR hardware before deploying! 