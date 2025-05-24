using UnityEngine;
using VRMultiplayer.Network;

namespace VRMultiplayer.Avatar
{
    /// <summary>
    /// Avatar Animation Helper that manages animations for Ready Player Me avatars
    /// Works with Final IK and network synchronization
    /// </summary>
    public class AvatarAnimationHelper : MonoBehaviour
    {
        [Header("Animation Settings")]
        [SerializeField] private bool enableIdleAnimations = true;
        [SerializeField] private bool enableHandGestures = true;
        [SerializeField] private bool enableFacialExpressions = true;
        [SerializeField] private bool enableBlinking = true;
        
        [Header("Gesture Thresholds")]
        [SerializeField] private float pointingThreshold = 0.3f;
        [SerializeField] private float grippingThreshold = 0.7f;
        [SerializeField] private float thumbsUpThreshold = 0.5f;
        
        [Header("Facial Animation")]
        [SerializeField] private float blinkFrequency = 3f;
        [SerializeField] private float blinkDuration = 0.15f;
        [SerializeField] private float expressionTransitionSpeed = 2f;
        
        // Components
        private Animator animator;
        private NetworkVRPlayer networkPlayer;
        private SkinnedMeshRenderer faceMeshRenderer;
        
        // Animation state
        private float lastBlinkTime;
        private bool isBlinking = false;
        private float currentBlinkWeight = 0f;
        
        // Gesture state
        private HandGesture leftHandGesture = HandGesture.Open;
        private HandGesture rightHandGesture = HandGesture.Open;
        private FacialExpression currentExpression = FacialExpression.Neutral;
        
        // Animation parameter hashes
        private readonly int PARAM_LEFT_HAND_POSE = Animator.StringToHash("LeftHandPose");
        private readonly int PARAM_RIGHT_HAND_POSE = Animator.StringToHash("RightHandPose");
        private readonly int PARAM_EXPRESSION = Animator.StringToHash("Expression");
        private readonly int PARAM_BLINK = Animator.StringToHash("Blink");
        
        // Blend shape indices (for facial expressions)
        private int blinkLeftIndex = -1;
        private int blinkRightIndex = -1;
        private int smileIndex = -1;
        private int frownIndex = -1;
        private int surpriseIndex = -1;
        
        public void Initialize(Animator avatarAnimator, NetworkVRPlayer player)
        {
            animator = avatarAnimator;
            networkPlayer = player;
            
            SetupFacialAnimation();
            InitializeAnimationParameters();
            
            Debug.Log("AvatarAnimationHelper initialized");
        }
        
        private void SetupFacialAnimation()
        {
            // Find the head mesh renderer for facial expressions
            var meshRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var renderer in meshRenderers)
            {
                if (renderer.name.ToLower().Contains("head") || renderer.name.ToLower().Contains("face"))
                {
                    faceMeshRenderer = renderer;
                    break;
                }
            }
            
            if (faceMeshRenderer != null)
            {
                // Cache blend shape indices for performance
                var mesh = faceMeshRenderer.sharedMesh;
                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    string shapeName = mesh.GetBlendShapeName(i).ToLower();
                    
                    if (shapeName.Contains("blink") && shapeName.Contains("left"))
                        blinkLeftIndex = i;
                    else if (shapeName.Contains("blink") && shapeName.Contains("right"))
                        blinkRightIndex = i;
                    else if (shapeName.Contains("smile"))
                        smileIndex = i;
                    else if (shapeName.Contains("frown"))
                        frownIndex = i;
                    else if (shapeName.Contains("surprise"))
                        surpriseIndex = i;
                }
                
                Debug.Log($"Found facial mesh renderer with {mesh.blendShapeCount} blend shapes");
            }
        }
        
        private void InitializeAnimationParameters()
        {
            if (animator == null) return;
            
            // Initialize animation parameters to default values
            animator.SetInteger(PARAM_LEFT_HAND_POSE, (int)HandGesture.Open);
            animator.SetInteger(PARAM_RIGHT_HAND_POSE, (int)HandGesture.Open);
            animator.SetInteger(PARAM_EXPRESSION, (int)FacialExpression.Neutral);
            animator.SetFloat(PARAM_BLINK, 0f);
        }
        
        private void Update()
        {
            if (animator == null || networkPlayer == null) return;
            
            UpdateHandGestures();
            UpdateFacialExpressions();
            UpdateBlinking();
        }
        
        private void UpdateHandGestures()
        {
            if (!enableHandGestures) return;
            
            // Analyze left hand input for gestures
            HandGesture newLeftGesture = AnalyzeHandGesture(true);
            if (newLeftGesture != leftHandGesture)
            {
                leftHandGesture = newLeftGesture;
                animator.SetInteger(PARAM_LEFT_HAND_POSE, (int)leftHandGesture);
            }
            
            // Analyze right hand input for gestures
            HandGesture newRightGesture = AnalyzeHandGesture(false);
            if (newRightGesture != rightHandGesture)
            {
                rightHandGesture = newRightGesture;
                animator.SetInteger(PARAM_RIGHT_HAND_POSE, (int)rightHandGesture);
            }
        }
        
        private HandGesture AnalyzeHandGesture(bool isLeftHand)
        {
            // This would typically get input from the hand controllers
            // For now, we'll use placeholder logic
            
            // In a real implementation, you would:
            // 1. Get hand tracking data from VR controllers
            // 2. Analyze finger positions and gestures
            // 3. Return the appropriate gesture
            
            // Placeholder gesture detection based on controller input
            bool gripPressed = false; // Get from hand controller
            bool triggerPressed = false; // Get from hand controller
            bool primaryButton = false; // Get from hand controller
            
            if (gripPressed && triggerPressed)
                return HandGesture.Fist;
            else if (triggerPressed)
                return HandGesture.Point;
            else if (primaryButton)
                return HandGesture.ThumbsUp;
            else if (gripPressed)
                return HandGesture.Grip;
            else
                return HandGesture.Open;
        }
        
        private void UpdateFacialExpressions()
        {
            if (!enableFacialExpressions || animator == null) return;
            
            // Simple expression logic - could be enhanced with voice analysis, etc.
            FacialExpression targetExpression = DetermineFacialExpression();
            
            if (targetExpression != currentExpression)
            {
                currentExpression = targetExpression;
                animator.SetInteger(PARAM_EXPRESSION, (int)currentExpression);
                
                // Update blend shapes if available
                UpdateFacialBlendShapes(targetExpression);
            }
        }
        
        private FacialExpression DetermineFacialExpression()
        {
            // Placeholder logic for facial expressions
            // In a real implementation, you might use:
            // - Voice analysis for emotion detection
            // - Face tracking from cameras
            // - Predefined expression triggers
            
            return FacialExpression.Neutral;
        }
        
        private void UpdateFacialBlendShapes(FacialExpression expression)
        {
            if (faceMeshRenderer == null) return;
            
            // Reset all expression blend shapes
            if (smileIndex >= 0) faceMeshRenderer.SetBlendShapeWeight(smileIndex, 0);
            if (frownIndex >= 0) faceMeshRenderer.SetBlendShapeWeight(frownIndex, 0);
            if (surpriseIndex >= 0) faceMeshRenderer.SetBlendShapeWeight(surpriseIndex, 0);
            
            // Apply the target expression
            float weight = 100f; // Full expression weight
            
            switch (expression)
            {
                case FacialExpression.Happy:
                    if (smileIndex >= 0)
                        faceMeshRenderer.SetBlendShapeWeight(smileIndex, weight);
                    break;
                    
                case FacialExpression.Sad:
                    if (frownIndex >= 0)
                        faceMeshRenderer.SetBlendShapeWeight(frownIndex, weight);
                    break;
                    
                case FacialExpression.Surprised:
                    if (surpriseIndex >= 0)
                        faceMeshRenderer.SetBlendShapeWeight(surpriseIndex, weight);
                    break;
                    
                case FacialExpression.Neutral:
                default:
                    // Already reset above
                    break;
            }
        }
        
        private void UpdateBlinking()
        {
            if (!enableBlinking || faceMeshRenderer == null) return;
            
            float timeSinceLastBlink = Time.time - lastBlinkTime;
            
            // Trigger random blinks
            if (!isBlinking && timeSinceLastBlink > (60f / blinkFrequency) + Random.Range(-0.5f, 0.5f))
            {
                StartBlink();
            }
            
            // Update blink animation
            if (isBlinking)
            {
                float blinkProgress = (Time.time - lastBlinkTime) / blinkDuration;
                
                if (blinkProgress <= 1f)
                {
                    // Use a smooth curve for natural blinking
                    currentBlinkWeight = Mathf.Sin(blinkProgress * Mathf.PI) * 100f;
                    
                    // Apply blink to both eyes
                    if (blinkLeftIndex >= 0)
                        faceMeshRenderer.SetBlendShapeWeight(blinkLeftIndex, currentBlinkWeight);
                    if (blinkRightIndex >= 0)
                        faceMeshRenderer.SetBlendShapeWeight(blinkRightIndex, currentBlinkWeight);
                    
                    // Update animator parameter
                    animator.SetFloat(PARAM_BLINK, currentBlinkWeight / 100f);
                }
                else
                {
                    // Blink finished
                    isBlinking = false;
                    currentBlinkWeight = 0f;
                    
                    if (blinkLeftIndex >= 0)
                        faceMeshRenderer.SetBlendShapeWeight(blinkLeftIndex, 0);
                    if (blinkRightIndex >= 0)
                        faceMeshRenderer.SetBlendShapeWeight(blinkRightIndex, 0);
                    
                    animator.SetFloat(PARAM_BLINK, 0f);
                }
            }
        }
        
        private void StartBlink()
        {
            isBlinking = true;
            lastBlinkTime = Time.time;
        }
        
        public void TriggerExpression(FacialExpression expression, float duration = 2f)
        {
            // Manually trigger a facial expression
            currentExpression = expression;
            animator.SetInteger(PARAM_EXPRESSION, (int)expression);
            UpdateFacialBlendShapes(expression);
            
            // Optionally revert to neutral after duration
            if (duration > 0)
            {
                Invoke(nameof(RevertToNeutral), duration);
            }
        }
        
        private void RevertToNeutral()
        {
            TriggerExpression(FacialExpression.Neutral, 0);
        }
        
        public void TriggerHandGesture(HandGesture gesture, bool isLeftHand, float duration = 1f)
        {
            if (isLeftHand)
            {
                leftHandGesture = gesture;
                animator.SetInteger(PARAM_LEFT_HAND_POSE, (int)gesture);
            }
            else
            {
                rightHandGesture = gesture;
                animator.SetInteger(PARAM_RIGHT_HAND_POSE, (int)gesture);
            }
            
            // Optionally revert to open hand after duration
            if (duration > 0)
            {
                if (isLeftHand)
                {
                    Invoke(nameof(RevertLeftHandToOpen), duration);
                }
                else
                {
                    Invoke(nameof(RevertRightHandToOpen), duration);
                }
            }
        }
        
        private void RevertLeftHandToOpen()
        {
            TriggerHandGesture(HandGesture.Open, true, 0);
        }
        
        private void RevertRightHandToOpen()
        {
            TriggerHandGesture(HandGesture.Open, false, 0);
        }
        
        public void SetAnimationSpeed(float speed)
        {
            if (animator != null)
            {
                animator.speed = speed;
            }
        }
        
        public void EnableGestures(bool enable)
        {
            enableHandGestures = enable;
        }
        
        public void EnableFacialExpressions(bool enable)
        {
            enableFacialExpressions = enable;
        }
        
        public void EnableBlinking(bool enable)
        {
            enableBlinking = enable;
        }
    }
    
    public enum HandGesture
    {
        Open = 0,
        Fist = 1,
        Point = 2,
        ThumbsUp = 3,
        Peace = 4,
        Grip = 5
    }
    
    public enum FacialExpression
    {
        Neutral = 0,
        Happy = 1,
        Sad = 2,
        Surprised = 3,
        Angry = 4,
        Confused = 5
    }
} 