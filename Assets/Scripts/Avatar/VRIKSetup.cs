using UnityEngine;
using RootMotion.FinalIK;

namespace VRMultiplayer.Avatar
{
    /// <summary>
    /// VR IK Setup utility for configuring Final IK with Ready Player Me avatars
    /// Handles automatic bone detection and VRIK configuration for optimal VR experience
    /// </summary>
    public class VRIKSetup : MonoBehaviour
    {
        [Header("IK Configuration")]
        [SerializeField] private bool autoDetectReferences = true;
        [SerializeField] private bool enablePlantFeet = true;
        [SerializeField] private bool enableLocomotion = true;
        
        [Header("IK Weights")]
        [Range(0f, 1f)]
        [SerializeField] private float headWeight = 1f;
        [Range(0f, 1f)]
        [SerializeField] private float leftArmWeight = 1f;
        [Range(0f, 1f)]
        [SerializeField] private float rightArmWeight = 1f;
        [Range(0f, 1f)]
        [SerializeField] private float pelvisWeight = 0.5f;
        [Range(0f, 1f)]
        [SerializeField] private float leftLegWeight = 0.3f;
        [Range(0f, 1f)]
        [SerializeField] private float rightLegWeight = 0.3f;
        
        [Header("Spine Settings")]
        [Range(0f, 1f)]
        [SerializeField] private float headClampWeight = 0.8f;
        [Range(0f, 1f)]
        [SerializeField] private float neckStiffness = 0.2f;
        [Range(0f, 1f)]
        [SerializeField] private float rotateChestByHands = 0.3f;
        [Range(0f, 1f)]
        [SerializeField] private float chestClampWeight = 0.5f;
        
        [Header("Arm Settings")]
        [Range(0f, 1f)]
        [SerializeField] private float shoulderRotationWeight = 0.2f;
        [Range(0f, 1f)]
        [SerializeField] private float shoulderTwistWeight = 1f;
        [Range(0f, 1f)]
        [SerializeField] private float bendGoalWeight = 0.5f;
        
        [Header("Leg Settings")]
        [Range(0f, 1f)]
        [SerializeField] private float legBendGoalWeight = 0.8f;
        [Range(0f, 1f)]
        [SerializeField] private float footRotationWeight = 0.5f;
        [SerializeField] private float stepThreshold = 0.3f;
        [SerializeField] private float stepSpeed = 3f;
        
        public VRIK SetupVRIK(GameObject avatar, Animator animator)
        {
            if (avatar == null || animator == null)
            {
                Debug.LogError("Cannot setup VRIK: Avatar or Animator is null");
                return null;
            }
            
            Debug.Log("Setting up VRIK for avatar");
            
            // Remove existing VRIK if present
            var existingVRIK = avatar.GetComponent<VRIK>();
            if (existingVRIK != null)
            {
                DestroyImmediate(existingVRIK);
            }
            
            // Add VRIK component
            VRIK vrik = avatar.AddComponent<VRIK>();
            
            // Auto-detect references if enabled
            if (autoDetectReferences)
            {
                AutoDetectReferences(vrik, animator);
            }
            
            // Configure VRIK settings
            ConfigureVRIKSettings(vrik);
            
            // Setup additional components
            SetupLegIK(vrik);
            
            Debug.Log("VRIK setup completed successfully");
            
            return vrik;
        }
        
        private void AutoDetectReferences(VRIK vrik, Animator animator)
        {
            Debug.Log("Auto-detecting VRIK references");
            
            try
            {
                // Get references from the Animator
                var references = new VRIK.References();
                
                // Root
                references.root = animator.transform;
                
                // Pelvis
                references.pelvis = animator.GetBoneTransform(HumanBodyBones.Hips);
                
                // Spine
                references.spine = animator.GetBoneTransform(HumanBodyBones.Spine);
                references.chest = animator.GetBoneTransform(HumanBodyBones.Chest);
                references.neck = animator.GetBoneTransform(HumanBodyBones.Neck);
                references.head = animator.GetBoneTransform(HumanBodyBones.Head);
                
                // Left Arm
                references.leftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
                references.leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                references.leftForearm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                references.leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                
                // Right Arm
                references.rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightShoulder);
                references.rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                references.rightForearm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                references.rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                
                // Left Leg
                references.leftThigh = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                references.leftCalf = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                references.leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                references.leftToes = animator.GetBoneTransform(HumanBodyBones.LeftToes);
                
                // Right Leg
                references.rightThigh = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                references.rightCalf = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
                references.rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                references.rightToes = animator.GetBoneTransform(HumanBodyBones.RightToes);
                
                // Assign references to VRIK
                vrik.references = references;
                
                // Auto-detect and assign references
                vrik.solver.SetToReferences(references);
                
                Debug.Log("VRIK references auto-detected successfully");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to auto-detect VRIK references: {e.Message}");
            }
        }
        
        private void ConfigureVRIKSettings(VRIK vrik)
        {
            Debug.Log("Configuring VRIK settings");
            
            // Spine configuration
            var spine = vrik.solver.spine;
            spine.headClampWeight = headClampWeight;
            spine.neckStiffness = neckStiffness;
            spine.rotateChestByHands = rotateChestByHands;
            spine.chestClampWeight = chestClampWeight;
            spine.maintainPelvisPosition = 0.8f;
            spine.maxRootAngle = 25f;
            
            // Left arm configuration
            var leftArm = vrik.solver.leftArm;
            leftArm.positionWeight = leftArmWeight;
            leftArm.rotationWeight = leftArmWeight;
            leftArm.shoulderRotationWeight = shoulderRotationWeight;
            leftArm.shoulderTwistWeight = shoulderTwistWeight;
            leftArm.bendGoalWeight = bendGoalWeight;
            leftArm.swivelOffset = 0f;
            
            // Right arm configuration
            var rightArm = vrik.solver.rightArm;
            rightArm.positionWeight = rightArmWeight;
            rightArm.rotationWeight = rightArmWeight;
            rightArm.shoulderRotationWeight = shoulderRotationWeight;
            rightArm.shoulderTwistWeight = shoulderTwistWeight;
            rightArm.bendGoalWeight = bendGoalWeight;
            rightArm.swivelOffset = 0f;
            
            // Left leg configuration
            var leftLeg = vrik.solver.leftLeg;
            leftLeg.positionWeight = leftLegWeight;
            leftLeg.rotationWeight = leftLegWeight;
            leftLeg.bendGoalWeight = legBendGoalWeight;
            leftLeg.swivelOffset = 0f;
            
            // Right leg configuration
            var rightLeg = vrik.solver.rightLeg;
            rightLeg.positionWeight = rightLegWeight;
            rightLeg.rotationWeight = rightLegWeight;
            rightLeg.bendGoalWeight = legBendGoalWeight;
            rightLeg.swivelOffset = 0f;
            
            // Locomotion settings
            if (enableLocomotion)
            {
                var locomotion = vrik.solver.locomotion;
                locomotion.weight = 1f;
                locomotion.footDistance = 0.3f;
                locomotion.stepThreshold = stepThreshold;
                locomotion.angleThreshold = 45f;
                locomotion.comAngleMlp = 1f;
                locomotion.maxVelocity = 0.4f;
                locomotion.velocityFactor = 0.4f;
                locomotion.maxLegStretch = 1f;
                locomotion.rootSpeed = stepSpeed;
                locomotion.stepSpeed = stepSpeed;
                locomotion.relaxLegTwistMinAngle = 20f;
                locomotion.relaxLegTwistSpeed = 400f;
            }
            
            // Plant feet settings
            if (enablePlantFeet)
            {
                var plantFeet = vrik.solver.plantFeet;
                plantFeet.weight = 1f;
                plantFeet.minWeight = 0.7f;
                plantFeet.speed = 3f;
                plantFeet.unplantDistance = 0.2f;
                plantFeet.heightOffset = 0.02f;
                plantFeet.heelOffset = 0.06f;
                plantFeet.blendSpeed = 3f;
            }
        }
        
        private void SetupLegIK(VRIK vrik)
        {
            // Additional leg IK setup for better foot placement
            var leftLeg = vrik.solver.leftLeg;
            var rightLeg = vrik.solver.rightLeg;
            
            // Configure foot rotation weights
            leftLeg.rotationWeight = footRotationWeight;
            rightLeg.rotationWeight = footRotationWeight;
            
            // Setup bend goals if needed
            if (leftLeg.bendGoal == null)
            {
                CreateBendGoal(leftLeg, "LeftKneeBendGoal", Vector3.forward);
            }
            
            if (rightLeg.bendGoal == null)
            {
                CreateBendGoal(rightLeg, "RightKneeBendGoal", Vector3.forward);
            }
        }
        
        private void CreateBendGoal(IKSolverVR.Arm armOrLeg, string name, Vector3 direction)
        {
            var bendGoalObj = new GameObject(name);
            bendGoalObj.transform.SetParent(transform);
            
            // Position the bend goal
            if (armOrLeg is IKSolverVR.Leg leg && leg.target != null)
            {
                bendGoalObj.transform.position = leg.target.position + direction * 0.5f;
            }
            
            // Assign the bend goal
            if (armOrLeg is IKSolverVR.Leg legSolver)
            {
                legSolver.bendGoal = bendGoalObj.transform;
            }
        }
        
        public void SetIKWeights(float head, float leftArm, float rightArm, float pelvis, float leftLeg, float rightLeg)
        {
            headWeight = head;
            leftArmWeight = leftArm;
            rightArmWeight = rightArm;
            pelvisWeight = pelvis;
            leftLegWeight = leftLeg;
            rightLegWeight = rightLeg;
        }
        
        public void EnableFullBodyTracking(VRIK vrik, bool enable)
        {
            if (vrik == null) return;
            
            float weight = enable ? 1f : 0f;
            
            vrik.solver.leftArm.positionWeight = weight * leftArmWeight;
            vrik.solver.leftArm.rotationWeight = weight * leftArmWeight;
            vrik.solver.rightArm.positionWeight = weight * rightArmWeight;
            vrik.solver.rightArm.rotationWeight = weight * rightArmWeight;
            vrik.solver.leftLeg.positionWeight = weight * leftLegWeight;
            vrik.solver.leftLeg.rotationWeight = weight * leftLegWeight;
            vrik.solver.rightLeg.positionWeight = weight * rightLegWeight;
            vrik.solver.rightLeg.rotationWeight = weight * rightLegWeight;
        }
        
        public void CalibrateForPlayer(VRIK vrik, float playerHeight)
        {
            if (vrik == null) return;
            
            // Adjust IK settings based on player height
            float heightRatio = playerHeight / 1.8f; // Assuming 1.8m as default height
            
            var locomotion = vrik.solver.locomotion;
            locomotion.footDistance *= heightRatio;
            locomotion.stepThreshold *= heightRatio;
            
            Debug.Log($"VRIK calibrated for player height: {playerHeight}m (ratio: {heightRatio})");
        }
        
        public void ValidateSetup(VRIK vrik)
        {
            if (vrik == null)
            {
                Debug.LogError("VRIK component is null");
                return;
            }
            
            var references = vrik.references;
            
            // Check essential references
            if (references.root == null) Debug.LogWarning("VRIK: Root reference is missing");
            if (references.pelvis == null) Debug.LogWarning("VRIK: Pelvis reference is missing");
            if (references.head == null) Debug.LogWarning("VRIK: Head reference is missing");
            if (references.leftHand == null) Debug.LogWarning("VRIK: Left hand reference is missing");
            if (references.rightHand == null) Debug.LogWarning("VRIK: Right hand reference is missing");
            if (references.leftFoot == null) Debug.LogWarning("VRIK: Left foot reference is missing");
            if (references.rightFoot == null) Debug.LogWarning("VRIK: Right foot reference is missing");
            
            Debug.Log("VRIK setup validation completed");
        }
    }
} 