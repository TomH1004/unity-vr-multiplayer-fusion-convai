using UnityEngine;
using ReadyPlayerMe.Core;
using VRMultiplayer.Network;
using RootMotion.FinalIK;
using System.Collections;

namespace VRMultiplayer.Avatar
{
    /// <summary>
    /// VR Avatar Controller that handles Ready Player Me avatar loading and Final IK setup
    /// Provides full body tracking for VR users
    /// </summary>
    public class VRAvatarController : MonoBehaviour
    {
        [Header("Avatar Loading")]
        [SerializeField] private AvatarLoader avatarLoader;
        [SerializeField] private string fallbackAvatarUrl = "";
        [SerializeField] private bool useAvatarCaching = true;
        
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
        public GameObject CurrentAvatar => currentAvatar;
        public Animator AvatarAnimator => avatarAnimator;
        
        public void Initialize(NetworkVRPlayer player)
        {
            networkPlayer = player;
            
            // Initialize avatar loader
            if (avatarLoader == null)
            {
                avatarLoader = gameObject.AddComponent<AvatarLoader>();
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
            
            if (string.IsNullOrEmpty(avatarUrl))
            {
                Debug.LogWarning("Avatar URL is empty, using fallback");
                avatarUrl = fallbackAvatarUrl;
            }
            
            if (string.IsNullOrEmpty(avatarUrl))
            {
                Debug.LogError("No valid avatar URL provided");
                OnAvatarLoadFailed?.Invoke();
                return;
            }
            
            currentAvatarUrl = avatarUrl;
            Debug.Log($"Loading avatar: {avatarUrl}");
            
            // Configure avatar loader
            var config = new AvatarConfig();
            config.UseEyeAnimations = enableEyeTracking;
            config.UseEyeBones = enableEyeTracking;
            config.UseDracoMeshCompression = true;
            
            avatarLoader.LoadAvatar(avatarUrl, OnAvatarLoadCompleted, OnAvatarLoadError);
        }
        
        private void OnAvatarLoadCompleted(CompletionEventArgs args)
        {
            Debug.Log("Avatar loaded successfully");
            
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
                return;
            }
            
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
            
            Debug.Log("Avatar setup completed");
        }
        
        private void OnAvatarLoadError(FailureEventArgs args)
        {
            Debug.LogError($"Failed to load avatar: {args.Message}");
            OnAvatarLoadFailed?.Invoke();
            
            // Try loading fallback avatar
            if (!string.IsNullOrEmpty(fallbackAvatarUrl) && currentAvatarUrl != fallbackAvatarUrl)
            {
                Debug.Log("Attempting to load fallback avatar");
                LoadAvatar(fallbackAvatarUrl);
            }
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
                lookAtIK.solver.eyes = new Transform[2];
                
                // Try to find eye bones
                var leftEye = avatarAnimator.GetBoneTransform(HumanBodyBones.LeftEye);
                var rightEye = avatarAnimator.GetBoneTransform(HumanBodyBones.RightEye);
                
                if (leftEye != null && rightEye != null)
                {
                    lookAtIK.solver.eyes[0] = leftEye;
                    lookAtIK.solver.eyes[1] = rightEye;
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
            
            // Update head target
            if (headTarget != null)
            {
                headTarget.position = networkPlayer.GetHeadPosition();
                headTarget.rotation = networkPlayer.HeadRotation;
            }
            
            // Update hand targets
            if (leftHandTarget != null)
            {
                leftHandTarget.position = networkPlayer.GetLeftHandPosition();
                leftHandTarget.rotation = networkPlayer.LeftHandRotation;
            }
            
            if (rightHandTarget != null)
            {
                rightHandTarget.position = networkPlayer.GetRightHandPosition();
                rightHandTarget.rotation = networkPlayer.RightHandRotation;
            }
            
            // Calculate pelvis position for better tracking
            if (pelvisTarget != null)
            {
                Vector3 headPos = networkPlayer.GetHeadPosition();
                pelvisTarget.position = new Vector3(headPos.x, transform.position.y + 0.1f, headPos.z);
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