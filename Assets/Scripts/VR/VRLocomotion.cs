using UnityEngine;
using UnityEngine.XR;
using Unity.XR.CoreUtils;
using VRMultiplayer.Network;


namespace VRMultiplayer.VR
{
    /// <summary>
    /// VR Locomotion Controller that handles movement, rotation, and teleportation in VR
    /// Integrated with network synchronization for multiplayer
    /// </summary>
    public class VRLocomotion : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private float rotationSpeed = 90f;
        [SerializeField] private bool enableSmoothMovement = true;
        [SerializeField] private bool enableSnapTurn = true;
        [SerializeField] private float snapTurnAngle = 30f;
        
        [Header("Teleportation")]
        [SerializeField] private bool enableTeleportation = true;
        [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationProvider teleportationProvider;
        [SerializeField] private LineRenderer teleportLine;
        [SerializeField] private GameObject teleportReticle;
        [SerializeField] private LayerMask teleportLayerMask = 1;
        [SerializeField] private float teleportMaxDistance = 10f;
        
        [Header("Comfort Settings")]
        [SerializeField] private bool enableVignette = true;
        [SerializeField] private float vignetteIntensity = 0.5f;
        [SerializeField] private bool enableGroundDetection = true;
        [SerializeField] private float groundCheckDistance = 0.1f;
        
        [Header("Input")]
        [SerializeField] private InputDeviceCharacteristics leftControllerCharacteristics = 
            InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller;
        [SerializeField] private InputDeviceCharacteristics rightControllerCharacteristics = 
            InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller;
        
        // Components
        private NetworkVRPlayer networkPlayer;
        private CharacterController characterController;
        private XROrigin xrOrigin;
        private Camera vrCamera;
        
        // Input devices
        private InputDevice leftController;
        private InputDevice rightController;
        private bool leftControllerFound = false;
        private bool rightControllerFound = false;
        
        // Movement state
        private Vector2 moveInput = Vector2.zero;
        private Vector2 turnInput = Vector2.zero;
        private bool teleportButtonPressed = false;
        private bool lastTeleportButtonState = false;
        
        // Teleportation state
        private bool isTeleporting = false;
        private Vector3 teleportDestination;
        private bool validTeleportDestination = false;
        
        // Ground detection
        private bool isGrounded = true;
        private float groundLevel = 0f;
        
        public bool IsGrounded => isGrounded;
        public Vector2 MoveInput => moveInput;
        public Vector2 TurnInput => turnInput;
        
        public void Initialize(NetworkVRPlayer player)
        {
            networkPlayer = player;
            SetupComponents();
            SetupTeleportation();
        }
        
        private void SetupComponents()
        {
            // Get XR Origin
            xrOrigin = FindObjectOfType<XROrigin>();
            if (xrOrigin == null)
            {
                Debug.LogError("No XR Origin found in scene!");
                return;
            }
            
            // Get VR Camera
            vrCamera = xrOrigin.Camera;
            
            // Setup Character Controller
            characterController = GetComponent<CharacterController>();
            if (characterController == null)
            {
                characterController = gameObject.AddComponent<CharacterController>();
                characterController.height = 1.8f;
                characterController.radius = 0.3f;
                characterController.center = new Vector3(0, 0.9f, 0);
            }
            
            // Setup Teleportation Provider
            if (teleportationProvider == null)
            {
                teleportationProvider = FindObjectOfType<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationProvider>();
                if (teleportationProvider == null)
                {
                    var teleportProviderObj = new GameObject("TeleportationProvider");
                    teleportationProvider = teleportProviderObj.AddComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationProvider>();
                }
            }
            
            Debug.Log("VRLocomotion components setup completed");
        }
        
        private void SetupTeleportation()
        {
            if (!enableTeleportation) return;
            
            // Create teleport line renderer
            if (teleportLine == null)
            {
                var teleportLineObj = new GameObject("TeleportLine");
                teleportLineObj.transform.SetParent(transform);
                teleportLine = teleportLineObj.AddComponent<LineRenderer>();
                
                // Configure line renderer
                teleportLine.material = new Material(Shader.Find("Sprites/Default"));
                teleportLine.material.color = Color.blue;
                teleportLine.startWidth = 0.02f;
                teleportLine.endWidth = 0.02f;
                teleportLine.positionCount = 0;
                teleportLine.enabled = false;
            }
            
            // Create teleport reticle
            if (teleportReticle == null)
            {
                teleportReticle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                teleportReticle.name = "TeleportReticle";
                teleportReticle.transform.SetParent(transform);
                teleportReticle.transform.localScale = new Vector3(1f, 0.01f, 1f);
                
                // Configure reticle material
                var reticleMaterial = new Material(Shader.Find("Standard"));
                reticleMaterial.color = Color.green;
                reticleMaterial.SetFloat("_Mode", 3); // Transparent mode
                reticleMaterial.color = new Color(0, 1, 0, 0.5f);
                teleportReticle.GetComponent<Renderer>().material = reticleMaterial;
                
                // Remove collider
                var collider = teleportReticle.GetComponent<Collider>();
                if (collider != null)
                {
                    DestroyImmediate(collider);
                }
                
                teleportReticle.SetActive(false);
            }
        }
        
        private void Update()
        {
            UpdateInputDevices();
            UpdateMovementInput();
            UpdateTeleportation();
            UpdateGroundDetection();
        }
        
        private void UpdateInputDevices()
        {
            // Only search for input devices for the local player
            if (networkPlayer == null || !networkPlayer.Object || !networkPlayer.Object.HasInputAuthority)
                return;
            
            // Find left controller
            if (!leftControllerFound)
            {
                var leftDevices = new System.Collections.Generic.List<InputDevice>();
                InputDevices.GetDevicesWithCharacteristics(leftControllerCharacteristics, leftDevices);
                if (leftDevices.Count > 0)
                {
                    leftController = leftDevices[0];
                    leftControllerFound = true;
                }
            }
            
            // Find right controller
            if (!rightControllerFound)
            {
                var rightDevices = new System.Collections.Generic.List<InputDevice>();
                InputDevices.GetDevicesWithCharacteristics(rightControllerCharacteristics, rightDevices);
                if (rightDevices.Count > 0)
                {
                    rightController = rightDevices[0];
                    rightControllerFound = true;
                }
            }
        }
        
        private void UpdateMovementInput()
        {
            // Only gather input for the local player
            if (networkPlayer == null || !networkPlayer.Object || !networkPlayer.Object.HasInputAuthority)
                return;
            
            // Get movement input from left controller
            if (leftControllerFound)
            {
                Vector2 leftThumbstick = Vector2.zero;
                if (leftController.TryGetFeatureValue(CommonUsages.primary2DAxis, out leftThumbstick))
                {
                    moveInput = leftThumbstick;
                }
            }
            
            // Get rotation input from right controller
            if (rightControllerFound)
            {
                Vector2 rightThumbstick = Vector2.zero;
                if (rightController.TryGetFeatureValue(CommonUsages.primary2DAxis, out rightThumbstick))
                {
                    turnInput = rightThumbstick;
                }
                
                // Get teleport button input
                bool currentTeleportButton = false;
                if (rightController.TryGetFeatureValue(CommonUsages.primaryButton, out currentTeleportButton))
                {
                    teleportButtonPressed = currentTeleportButton;
                }
            }
        }
        
        private void UpdateTeleportation()
        {
            if (!enableTeleportation) return;
            
            // Only handle teleportation for the local player
            if (networkPlayer == null || !networkPlayer.Object || !networkPlayer.Object.HasInputAuthority)
                return;
            
            // Handle teleport button press
            if (teleportButtonPressed && !lastTeleportButtonState)
            {
                StartTeleportation();
            }
            else if (!teleportButtonPressed && lastTeleportButtonState)
            {
                EndTeleportation();
            }
            else if (teleportButtonPressed)
            {
                UpdateTeleportAiming();
            }
            
            lastTeleportButtonState = teleportButtonPressed;
        }
        
        private void StartTeleportation()
        {
            isTeleporting = true;
            if (teleportLine != null)
            {
                teleportLine.enabled = true;
            }
            Debug.Log("Started teleportation aiming");
        }
        
        private void UpdateTeleportAiming()
        {
            if (!isTeleporting || rightController.Equals(default(InputDevice))) return;
            
            // Get controller position and rotation
            Vector3 controllerPosition;
            Quaternion controllerRotation;
            
            if (rightController.TryGetFeatureValue(CommonUsages.devicePosition, out controllerPosition) &&
                rightController.TryGetFeatureValue(CommonUsages.deviceRotation, out controllerRotation))
            {
                // Convert to world space
                Vector3 worldControllerPos = xrOrigin.transform.TransformPoint(controllerPosition);
                Vector3 worldControllerForward = xrOrigin.transform.TransformDirection(controllerRotation * Vector3.forward);
                
                // Perform raycast for teleportation
                RaycastHit hit;
                if (Physics.Raycast(worldControllerPos, worldControllerForward, out hit, teleportMaxDistance, teleportLayerMask))
                {
                    teleportDestination = hit.point;
                    validTeleportDestination = true;
                    
                    // Update teleport line
                    UpdateTeleportLine(worldControllerPos, hit.point);
                    
                    // Show reticle
                    if (teleportReticle != null)
                    {
                        teleportReticle.SetActive(true);
                        teleportReticle.transform.position = hit.point + Vector3.up * 0.01f;
                    }
                }
                else
                {
                    validTeleportDestination = false;
                    
                    // Update teleport line to max distance
                    Vector3 endPoint = worldControllerPos + worldControllerForward * teleportMaxDistance;
                    UpdateTeleportLine(worldControllerPos, endPoint);
                    
                    // Hide reticle
                    if (teleportReticle != null)
                    {
                        teleportReticle.SetActive(false);
                    }
                }
            }
        }
        
        private void UpdateTeleportLine(Vector3 startPoint, Vector3 endPoint)
        {
            if (teleportLine == null) return;
            
            // Create parabolic arc
            int linePoints = 20;
            teleportLine.positionCount = linePoints;
            
            for (int i = 0; i < linePoints; i++)
            {
                float t = i / (float)(linePoints - 1);
                Vector3 point = Vector3.Lerp(startPoint, endPoint, t);
                
                // Add gravity effect to the arc
                float height = 4f * t * (1f - t);
                point.y += height;
                
                teleportLine.SetPosition(i, point);
            }
            
            // Change color based on validity
            teleportLine.material.color = validTeleportDestination ? Color.green : Color.red;
        }
        
        private void EndTeleportation()
        {
            if (isTeleporting && validTeleportDestination)
            {
                PerformTeleport();
            }
            
            // Clean up teleportation UI
            isTeleporting = false;
            validTeleportDestination = false;
            
            if (teleportLine != null)
            {
                teleportLine.enabled = false;
                teleportLine.positionCount = 0;
            }
            
            if (teleportReticle != null)
            {
                teleportReticle.SetActive(false);
            }
            
            Debug.Log("Ended teleportation");
        }
        
        private void PerformTeleport()
        {
            if (teleportationProvider != null)
            {
                var teleportRequest = new UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportRequest()
                {
                    destinationPosition = teleportDestination,
                    destinationRotation = transform.rotation
                };
                
                teleportationProvider.QueueTeleportRequest(teleportRequest);
                Debug.Log($"Teleported to: {teleportDestination}");
            }
            else
            {
                // Fallback teleportation
                transform.position = teleportDestination;
                Debug.Log($"Direct teleport to: {teleportDestination}");
            }
        }
        
        public void ProcessMovementInput(Vector2 move, Vector2 turn)
        {
            // Only process movement for players who have input authority
            if (networkPlayer == null || !networkPlayer.Object || !networkPlayer.Object.HasInputAuthority) 
                return;
            
            // Handle smooth movement
            if (enableSmoothMovement && move.magnitude > 0.1f)
            {
                Vector3 moveDirection = CalculateMoveDirection(move);
                MoveCharacter(moveDirection);
            }
            
            // Handle snap turn
            if (enableSnapTurn && Mathf.Abs(turn.x) > 0.8f)
            {
                PerformSnapTurn(turn.x > 0 ? snapTurnAngle : -snapTurnAngle);
            }
        }
        
        private Vector3 CalculateMoveDirection(Vector2 input)
        {
            // Get camera forward direction for movement relative to head direction
            Vector3 cameraForward = vrCamera.transform.forward;
            Vector3 cameraRight = vrCamera.transform.right;
            
            // Remove Y component for ground-based movement
            cameraForward.y = 0;
            cameraRight.y = 0;
            cameraForward.Normalize();
            cameraRight.Normalize();
            
            // Calculate movement direction
            Vector3 moveDirection = (cameraForward * input.y + cameraRight * input.x) * moveSpeed;
            
            return moveDirection;
        }
        
        private void MoveCharacter(Vector3 moveDirection)
        {
            if (characterController != null)
            {
                // Add gravity if not grounded
                if (!isGrounded)
                {
                    moveDirection.y -= 9.81f * Time.deltaTime;
                }
                
                characterController.Move(moveDirection * Time.deltaTime);
            }
            else
            {
                // Fallback movement
                transform.Translate(moveDirection * Time.deltaTime, Space.World);
            }
        }
        
        private void PerformSnapTurn(float angle)
        {
            transform.Rotate(0, angle, 0);
        }
        
        private void UpdateGroundDetection()
        {
            if (!enableGroundDetection) return;
            
            // Perform ground check
            Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;
            RaycastHit hit;
            
            if (Physics.Raycast(rayOrigin, Vector3.down, out hit, groundCheckDistance + 0.1f))
            {
                isGrounded = true;
                groundLevel = hit.point.y;
                
                // Adjust character position to ground level
                if (transform.position.y < groundLevel)
                {
                    Vector3 correctedPosition = transform.position;
                    correctedPosition.y = groundLevel;
                    transform.position = correctedPosition;
                }
            }
            else
            {
                isGrounded = false;
            }
        }
        
        public void GetMovementInput(out Vector2 move, out Vector2 turn)
        {
            move = moveInput;
            turn = turnInput;
        }
        
        public void SetMovementEnabled(bool enabled)
        {
            enableSmoothMovement = enabled;
            enableSnapTurn = enabled;
        }
        
        public void SetTeleportationEnabled(bool enabled)
        {
            enableTeleportation = enabled;
        }
        
        private void OnDrawGizmosSelected()
        {
            // Draw movement bounds
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position, new Vector3(1f, 2f, 1f));
            
            // Draw ground detection ray
            if (enableGroundDetection)
            {
                Gizmos.color = isGrounded ? Color.green : Color.red;
                Vector3 rayStart = transform.position + Vector3.up * 0.1f;
                Gizmos.DrawRay(rayStart, Vector3.down * (groundCheckDistance + 0.1f));
            }
        }
    }
} 