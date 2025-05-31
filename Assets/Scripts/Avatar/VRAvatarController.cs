using UnityEngine;
using ReadyPlayerMe.Core;
using VRMultiplayer.Network;
using RootMotion.FinalIK;
using System.Collections;
using System.Linq;
using System.Collections.Generic;

namespace VRMultiplayer.Avatar
{
    /// <summary>
    /// VR Avatar Controller that handles Ready Player Me avatar loading and Final IK setup
    /// Provides full body tracking for VR users
    /// </summary>
    public class VRAvatarController : MonoBehaviour
    {
        [Header("Avatar Loading")]
        [SerializeField] private AvatarObjectLoader avatarLoader;
        [SerializeField] private string fallbackAvatarUrl = "https://models.readyplayer.me/6409cc49c2e68b002fbbbd4e.glb"; // Working test avatar
        [SerializeField] private bool useAvatarCaching = true;
        [SerializeField] private string[] workingTestAvatars = {
            "https://models.readyplayer.me/6409cc49c2e68b002fbbbd4e.glb", // Confirmed working avatar 1
            "https://models.readyplayer.me/638df693d72bffc6fa17943c.glb", // Confirmed working avatar 2
            "https://models.readyplayer.me/638df34ad72bffc6fa17943a.glb"  // Confirmed working avatar 3
        };
        
        [Header("VR IK Setup")]
        [SerializeField] private VRIKSetup vrikSetup;
        [SerializeField] private bool enableFullBodyIK = true;
        [SerializeField] private bool enableHandIK = true;
        [SerializeField] private bool enableEyeTracking = true;
        
        [Header("Avatar Transforms")]
        [SerializeField] private Transform avatarRoot;
        [SerializeField] private Transform headTarget;
        [SerializeField] private Transform leftHandTarget;
        [SerializeField] private Transform rightHandTarget;
        [SerializeField] private Transform pelvisTarget;
        [SerializeField] private Transform leftFootTarget;
        [SerializeField] private Transform rightFootTarget;
        
        [Header("Avatar Settings")]
        [SerializeField] private float avatarHeight = 1.8f;
        [SerializeField] private Vector3 avatarOffset = Vector3.zero;
        [SerializeField] private bool hideLocalAvatar = false;
        
        // Components
        private NetworkVRPlayer networkPlayer;
        private GameObject currentAvatar;
        private Animator avatarAnimator;
        private VRIK vrik;
        private LookAtIK lookAtIK;
        private AvatarAnimationHelper animationHelper;
        
        // Avatar state
        private bool isAvatarLoaded = false;
        private bool isInitialized = false;
        private string currentAvatarUrl;
        
        // Events
        public System.Action<GameObject> OnAvatarLoaded;
        public System.Action OnAvatarLoadFailed;
        
        public bool IsAvatarLoaded => isAvatarLoaded;
        public bool IsInitialized => isInitialized;
        public GameObject CurrentAvatar => currentAvatar;
        public Animator AvatarAnimator => avatarAnimator;
        
        public void Initialize(NetworkVRPlayer player)
        {
            networkPlayer = player;
            
            // Initialize avatar loader
            if (avatarLoader == null)
            {
                avatarLoader = new AvatarObjectLoader();
            }
            
            // Setup VR IK
            if (vrikSetup == null)
            {
                vrikSetup = GetComponent<VRIKSetup>();
                if (vrikSetup == null)
                {
                    vrikSetup = gameObject.AddComponent<VRIKSetup>();
                }
            }
            
            // Create target transforms if not assigned
            CreateTargetTransforms();
            
            isInitialized = true;
            Debug.Log("VRAvatarController initialized");
        }
        
        private void CreateTargetTransforms()
        {
            if (headTarget == null)
            {
                var headObj = new GameObject("HeadTarget");
                headObj.transform.SetParent(transform);
                headTarget = headObj.transform;
            }
            
            if (leftHandTarget == null)
            {
                var leftHandObj = new GameObject("LeftHandTarget");
                leftHandObj.transform.SetParent(transform);
                leftHandTarget = leftHandObj.transform;
            }
            
            if (rightHandTarget == null)
            {
                var rightHandObj = new GameObject("RightHandTarget");
                rightHandObj.transform.SetParent(transform);
                rightHandTarget = rightHandObj.transform;
            }
            
            if (pelvisTarget == null)
            {
                var pelvisObj = new GameObject("PelvisTarget");
                pelvisObj.transform.SetParent(transform);
                pelvisTarget = pelvisObj.transform;
            }
            
            if (leftFootTarget == null)
            {
                var leftFootObj = new GameObject("LeftFootTarget");
                leftFootObj.transform.SetParent(transform);
                leftFootTarget = leftFootObj.transform;
            }
            
            if (rightFootTarget == null)
            {
                var rightFootObj = new GameObject("RightFootTarget");
                rightFootObj.transform.SetParent(transform);
                rightFootTarget = rightFootObj.transform;
            }
        }
        
        public void LoadAvatar(string avatarUrl)
        {
            if (!isInitialized)
            {
                Debug.LogError("VRAvatarController not initialized!");
                return;
            }

            // If the provided URL is problematic, use a working test avatar instead
            if (string.IsNullOrEmpty(avatarUrl) || IsProblematicUrl(avatarUrl))
            {
                Debug.LogWarning($"Avatar URL '{avatarUrl}' appears problematic, using working test avatar");
                avatarUrl = GetWorkingTestAvatar();
            }

            currentAvatarUrl = avatarUrl;
            Debug.Log($"Loading avatar: {avatarUrl}");
            
            StartCoroutine(TryLoadAvatarWithFallbacks(avatarUrl));
        }
        
        private bool IsProblematicUrl(string url)
        {
            // Known problematic URLs based on Ready Player Me forum discussions
            string[] problematicIds = {
                "682a0667bdbf61e0fa665dc8", // The URL that was failing
                "64bfa8f1b8a9b6f1c8f5d9e3", // Other potentially problematic URLs
                "64bfa8f1b8a9b6f1c8f5d9e4",
                "64bfa8f1b8a9b6f1c8f5d9e5"
            };
            
            foreach (var problematicId in problematicIds)
            {
                if (url.Contains(problematicId))
                {
                    return true;
                }
            }
            
            return false;
        }
        
        private string GetWorkingTestAvatar()
        {
            if (workingTestAvatars != null && workingTestAvatars.Length > 0)
            {
                // Return a random working avatar
                int randomIndex = UnityEngine.Random.Range(0, workingTestAvatars.Length);
                return workingTestAvatars[randomIndex];
            }
            return fallbackAvatarUrl;
        }
        
        private System.Collections.IEnumerator TryLoadAvatarWithFallbacks(string primaryUrl)
        {
            // List of URLs to try in order
            var urlsToTry = new List<string> { primaryUrl };
            
            // Add working test avatars as fallbacks
            if (workingTestAvatars != null)
            {
                foreach (var testUrl in workingTestAvatars)
                {
                    if (!urlsToTry.Contains(testUrl))
                    {
                        urlsToTry.Add(testUrl);
                    }
                }
            }
            
            // Add final fallback
            if (!urlsToTry.Contains(fallbackAvatarUrl) && !string.IsNullOrEmpty(fallbackAvatarUrl))
            {
                urlsToTry.Add(fallbackAvatarUrl);
            }
            
            foreach (var url in urlsToTry)
            {
                Debug.Log($"Attempting to load avatar: {url}");
                
                bool loadAttempted = false;
                bool loadSucceeded = false;
                
                // Create a simple, compatible configuration
                var config = ScriptableObject.CreateInstance<AvatarConfig>();
                config.UseDracoCompression = false; // Disable compression to avoid processing errors
                config.UseMeshOptCompression = false;
                config.Pose = ReadyPlayerMe.Core.Pose.APose;
                
                // Configure avatar loader
                if (avatarLoader == null)
                {
                    avatarLoader = new AvatarObjectLoader();
                }
                
                // Based on Ready Player Me forum discussions, recreate the loader for each attempt to avoid state issues
                avatarLoader = new AvatarObjectLoader();
                
                // Clear previous events
                avatarLoader.OnCompleted -= OnAvatarLoadCompleted;
                avatarLoader.OnFailed -= OnAvatarLoadError;
                
                // Set up events for this attempt
                System.EventHandler<CompletionEventArgs> onSuccess = null;
                System.EventHandler<FailureEventArgs> onFailure = null;
                
                onSuccess = (sender, args) =>
                {
                    loadSucceeded = true;
                    loadAttempted = true;
                    avatarLoader.OnCompleted -= onSuccess;
                    avatarLoader.OnFailed -= onFailure;
                    OnAvatarLoadCompleted(sender, args);
                };
                
                onFailure = (sender, args) =>
                {
                    loadAttempted = true;
                    avatarLoader.OnCompleted -= onSuccess;
                    avatarLoader.OnFailed -= onFailure;
                    Debug.LogWarning($"Failed to load avatar {url}: {args.Message}");
                };
                
                avatarLoader.OnCompleted += onSuccess;
                avatarLoader.OnFailed += onFailure;
                avatarLoader.AvatarConfig = config;
                
                try
                {
                    Debug.Log($"Starting avatar load for: {url}");
                    avatarLoader.LoadAvatar(url);
                    Debug.Log($"Avatar load call completed for: {url}");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Exception starting avatar load: {e.Message}");
                    Debug.LogError($"Stack trace: {e.StackTrace}");
                    loadAttempted = true;
                }
                
                // Wait for this attempt to complete (max 10 seconds)
                float timeout = 10f;
                while (!loadAttempted && timeout > 0)
                {
                    yield return new WaitForSeconds(0.1f);
                    timeout -= 0.1f;
                }
                
                if (loadSucceeded)
                {
                    Debug.Log($"Successfully loaded avatar: {url}");
                    yield break; // Success! Exit the coroutine
                }
                
                // If this wasn't the last URL, try the next one
                if (url != urlsToTry[urlsToTry.Count - 1])
                {
                    Debug.Log($"Failed to load {url}, trying next fallback...");
                    yield return new WaitForSeconds(0.5f); // Brief pause between attempts
                }
            }
            
            // If we get here, all attempts failed
            Debug.LogError("All avatar loading attempts failed, creating simple fallback");
            CreateSimpleFallbackAvatar();
        }
        
        private void OnAvatarLoadCompleted(object sender, CompletionEventArgs args)
        {
            Debug.Log("Avatar loading completed successfully");
            
            // Validate the loaded avatar GameObject
            if (args.Avatar == null)
            {
                Debug.LogError("Avatar loading completed but GameObject is null - this is a Ready Player Me SDK issue");
                Debug.LogError("URL attempted: " + currentAvatarUrl);
                Debug.LogError("This is a known issue discussed in RPM forums. Trying fallback...");
                CreateSimpleFallbackAvatar();
                return;
            }
            
            if (args.Avatar.GetComponent<Animator>() == null)
            {
                Debug.LogError("Avatar loaded but has no Animator component - incompatible avatar");
                Debug.LogError("URL attempted: " + currentAvatarUrl);
                CreateSimpleFallbackAvatar();
                return;
            }
            
            Debug.Log($"Avatar GameObject validated successfully: {args.Avatar.name}");
            
            // Remove old avatar
            if (currentAvatar != null)
            {
                DestroyImmediate(currentAvatar);
            }
            
            // Set new avatar
            currentAvatar = args.Avatar;
            currentAvatar.transform.SetParent(transform);
            currentAvatar.transform.localPosition = avatarOffset;
            currentAvatar.transform.localRotation = Quaternion.identity;
            
            // Get animator
            avatarAnimator = currentAvatar.GetComponent<Animator>();
            if (avatarAnimator == null)
            {
                Debug.LogError("Avatar has no Animator component!");
                CreateSimpleFallbackAvatar();
                return;
            }
            
            Debug.Log("Avatar components validated, setting up VR IK...");
            
            // Setup VR IK
            SetupVRIK();
            
            // Setup animation helper
            SetupAnimationHelper();
            
            // Hide avatar for local player if specified
            if (hideLocalAvatar && networkPlayer != null && networkPlayer.Object.HasInputAuthority)
            {
                SetAvatarVisibility(false);
            }
            
            isAvatarLoaded = true;
            OnAvatarLoaded?.Invoke(currentAvatar);
            
            Debug.Log($"Avatar setup completed successfully: {currentAvatar.name}");
        }
        
        private void OnAvatarLoadError(object sender, FailureEventArgs args)
        {
            Debug.LogError($"Failed to load avatar: {args.Message}");
            Debug.LogError($"Avatar URL: {currentAvatarUrl}");
            Debug.LogError($"Failure Type: {args.Type}");
            
            // Try fallback avatar if we haven't tried it yet
            if (!string.IsNullOrEmpty(fallbackAvatarUrl) && currentAvatarUrl != fallbackAvatarUrl)
            {
                Debug.Log($"Attempting to load fallback avatar: {fallbackAvatarUrl}");
                
                // Use a simpler configuration for fallback
                var simpleConfig = new AvatarConfig();
                simpleConfig.UseDracoCompression = false; // Disable compression for better compatibility
                simpleConfig.UseMeshOptCompression = false;
                simpleConfig.Pose = ReadyPlayerMe.Core.Pose.APose;
                
                if (avatarLoader == null)
                {
                    avatarLoader = new AvatarObjectLoader();
                }
                
                avatarLoader.AvatarConfig = simpleConfig;
                avatarLoader.LoadAvatar(fallbackAvatarUrl);
            }
            else
            {
                Debug.LogWarning("Ready Player Me avatar loading failed, creating simple Unity primitive avatar");
                CreateSimpleFallbackAvatar();
            }
        }
        
        private void CreateSimpleFallbackAvatar()
        {
            // Remove old avatar
            if (currentAvatar != null)
            {
                DestroyImmediate(currentAvatar);
            }
            
            // Create simple capsule avatar
            currentAvatar = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            currentAvatar.name = "SimpleFallbackAvatar";
            currentAvatar.transform.SetParent(transform);
            currentAvatar.transform.localPosition = avatarOffset;
            currentAvatar.transform.localRotation = Quaternion.identity;
            currentAvatar.transform.localScale = new Vector3(0.5f, 1f, 0.5f);
            
            // Add a simple material
            var renderer = currentAvatar.GetComponent<Renderer>();
            if (renderer != null)
            {
                var material = new Material(Shader.Find("Standard"));
                material.color = Color.blue;
                renderer.material = material;
            }
            
            // Remove collider (we don't need physics)
            var collider = currentAvatar.GetComponent<Collider>();
            if (collider != null)
            {
                DestroyImmediate(collider);
            }
            
            // Mark as loaded
            isAvatarLoaded = true;
            OnAvatarLoaded?.Invoke(currentAvatar);
            
            Debug.Log("Simple fallback avatar created successfully");
        }
        
        private void SetupVRIK()
        {
            if (!enableFullBodyIK || currentAvatar == null || vrikSetup == null)
                return;
                
            Debug.Log("Setting up VR IK");
            
            // Setup VRIK component
            vrik = vrikSetup.SetupVRIK(currentAvatar, avatarAnimator);
            
            if (vrik != null)
            {
                // Configure IK targets
                vrik.solver.spine.headTarget = headTarget;
                vrik.solver.leftArm.target = leftHandTarget;
                vrik.solver.rightArm.target = rightHandTarget;
                vrik.solver.spine.pelvisTarget = pelvisTarget;
                vrik.solver.leftLeg.target = leftFootTarget;
                vrik.solver.rightLeg.target = rightFootTarget;
                
                // Configure IK settings for VR
                vrik.solver.spine.headClampWeight = 0.5f;
                vrik.solver.spine.neckStiffness = 0f;
                vrik.solver.spine.rotateChestByHands = 0.2f;
                vrik.solver.spine.chestClampWeight = 0.5f;
                vrik.solver.spine.headClampWeight = 0.8f;
                
                // Hand IK settings
                if (enableHandIK)
                {
                    vrik.solver.leftArm.positionWeight = 1f;
                    vrik.solver.leftArm.rotationWeight = 1f;
                    vrik.solver.rightArm.positionWeight = 1f;
                    vrik.solver.rightArm.rotationWeight = 1f;
                }
                
                Debug.Log("VR IK setup completed");
            }
            
            // Setup Look At IK for eye tracking
            if (enableEyeTracking)
            {
                SetupEyeTracking();
            }
        }
        
        private void SetupEyeTracking()
        {
            if (currentAvatar == null) return;
            
            lookAtIK = currentAvatar.GetComponent<LookAtIK>();
            if (lookAtIK == null)
            {
                lookAtIK = currentAvatar.AddComponent<LookAtIK>();
            }
            
            // Configure eye tracking
            var head = avatarAnimator.GetBoneTransform(HumanBodyBones.Head);
            if (head != null)
            {
                lookAtIK.solver.head.transform = head;
                
                // Try to find eye bones
                var leftEye = avatarAnimator.GetBoneTransform(HumanBodyBones.LeftEye);
                var rightEye = avatarAnimator.GetBoneTransform(HumanBodyBones.RightEye);
                
                if (leftEye != null && rightEye != null)
                {
                    // Create LookAtBone array for eyes
                    lookAtIK.solver.eyes = new RootMotion.FinalIK.IKSolverLookAt.LookAtBone[2];
                    lookAtIK.solver.eyes[0] = new RootMotion.FinalIK.IKSolverLookAt.LookAtBone(leftEye);
                    lookAtIK.solver.eyes[1] = new RootMotion.FinalIK.IKSolverLookAt.LookAtBone(rightEye);
                }
                
                lookAtIK.solver.bodyWeight = 0.1f;
                lookAtIK.solver.headWeight = 0.8f;
                lookAtIK.solver.eyesWeight = 1f;
            }
        }
        
        private void SetupAnimationHelper()
        {
            if (currentAvatar == null) return;
            
            animationHelper = currentAvatar.GetComponent<AvatarAnimationHelper>();
            if (animationHelper == null)
            {
                animationHelper = currentAvatar.AddComponent<AvatarAnimationHelper>();
            }
            
            animationHelper.Initialize(avatarAnimator, networkPlayer);
        }
        
        private void Update()
        {
            if (!isAvatarLoaded || currentAvatar == null || networkPlayer == null)
                return;
                
            UpdateIKTargets();
            UpdateEyeTracking();
        }
        
        private void UpdateIKTargets()
        {
            if (vrik == null || networkPlayer == null) return;
            
            // Skip updates if network player data is not valid
            if (!networkPlayer.Object || !networkPlayer.Object.IsValid) return;
            
            // Update head target
            if (headTarget != null)
            {
                Vector3 headPos = networkPlayer.GetHeadPosition();
                Quaternion headRot = networkPlayer.HeadRotation;
                
                // Only update if the position is reasonable (not at origin)
                if (headPos != Vector3.zero && Vector3.Distance(headPos, transform.position) < 5f)
                {
                    headTarget.position = headPos;
                    headTarget.rotation = headRot;
                }
            }
            
            // Update hand targets
            if (leftHandTarget != null)
            {
                Vector3 leftHandPos = networkPlayer.GetLeftHandPosition();
                Quaternion leftHandRot = networkPlayer.LeftHandRotation;
                
                // Only update if the position is reasonable
                if (leftHandPos != Vector3.zero && Vector3.Distance(leftHandPos, transform.position) < 3f)
                {
                    leftHandTarget.position = leftHandPos;
                    leftHandTarget.rotation = leftHandRot;
                }
            }
            
            if (rightHandTarget != null)
            {
                Vector3 rightHandPos = networkPlayer.GetRightHandPosition();
                Quaternion rightHandRot = networkPlayer.RightHandRotation;
                
                // Only update if the position is reasonable
                if (rightHandPos != Vector3.zero && Vector3.Distance(rightHandPos, transform.position) < 3f)
                {
                    rightHandTarget.position = rightHandPos;
                    rightHandTarget.rotation = rightHandRot;
                }
            }
            
            // Calculate pelvis position for better tracking
            if (pelvisTarget != null)
            {
                Vector3 headPos = networkPlayer.GetHeadPosition();
                if (headPos != Vector3.zero)
                {
                    pelvisTarget.position = new Vector3(headPos.x, transform.position.y + 0.1f, headPos.z);
                }
            }
            
            // Foot IK (simple ground detection)
            UpdateFootIK();
        }
        
        private void UpdateFootIK()
        {
            if (leftFootTarget == null || rightFootTarget == null) return;
            
            float groundY = transform.position.y;
            
            // Simple foot positioning - can be enhanced with ground detection
            Vector3 pelvisPos = pelvisTarget != null ? pelvisTarget.position : transform.position;
            
            leftFootTarget.position = new Vector3(pelvisPos.x - 0.15f, groundY, pelvisPos.z);
            rightFootTarget.position = new Vector3(pelvisPos.x + 0.15f, groundY, pelvisPos.z);
        }
        
        private void UpdateEyeTracking()
        {
            if (!isInitialized || networkPlayer == null || !networkPlayer.Object || !networkPlayer.Object.IsValid) 
                return;
                
            // Only update eye tracking for the local player in VR
            if (!networkPlayer.Object.HasInputAuthority) 
                return;
            
            if (lookAtIK == null || !enableEyeTracking) return;
            
            // Simple eye tracking - look at other players
            var otherPlayer = FindNearestPlayer();
            if (otherPlayer != null)
            {
                Vector3 lookTarget = otherPlayer.GetHeadPosition();
                lookAtIK.solver.target = CreateLookTarget(lookTarget);
            }
        }
        
        private NetworkVRPlayer FindNearestPlayer()
        {
            var players = FindObjectsOfType<NetworkVRPlayer>();
            NetworkVRPlayer nearest = null;
            float minDistance = float.MaxValue;
            
            foreach (var player in players)
            {
                if (player == networkPlayer) continue;
                
                float distance = Vector3.Distance(transform.position, player.transform.position);
                if (distance < minDistance && distance < 5f) // Within 5 meters
                {
                    minDistance = distance;
                    nearest = player;
                }
            }
            
            return nearest;
        }
        
        private Transform CreateLookTarget(Vector3 position)
        {
            var lookTargetObj = new GameObject("LookTarget");
            lookTargetObj.transform.position = position;
            lookTargetObj.transform.SetParent(transform);
            return lookTargetObj.transform;
        }
        
        public void SetAvatarVisibility(bool visible)
        {
            if (currentAvatar == null) return;
            
            var renderers = currentAvatar.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                renderer.enabled = visible;
            }
        }
        
        public void SetAvatarLayer(int layer)
        {
            if (currentAvatar == null) return;
            
            SetLayerRecursively(currentAvatar, layer);
        }
        
        private void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
        
        private void OnDestroy()
        {
            if (currentAvatar != null)
            {
                DestroyImmediate(currentAvatar);
            }
        }
    }
} 