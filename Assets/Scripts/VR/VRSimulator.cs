using UnityEngine;
using VRMultiplayer.Network;

namespace VRMultiplayer.VR
{
    /// <summary>
    /// VR Simulator for testing avatar movement in Unity Editor without VR headset
    /// Use WASD for head movement, mouse for head rotation, and keys for hand positions
    /// </summary>
    public class VRSimulator : MonoBehaviour
    {
        [Header("Simulation Settings")]
        [SerializeField] private bool enableSimulation = true;
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private float rotationSpeed = 90f;
        [SerializeField] private float handMoveSpeed = 1f;
        
        [Header("Key Bindings")]
        [SerializeField] private KeyCode leftHandUp = KeyCode.Q;
        [SerializeField] private KeyCode leftHandDown = KeyCode.E;
        [SerializeField] private KeyCode rightHandUp = KeyCode.U;
        [SerializeField] private KeyCode rightHandDown = KeyCode.O;
        [SerializeField] private KeyCode resetPose = KeyCode.R;
        
        private NetworkVRPlayer networkPlayer;
        private Vector3 simulatedHeadPos = new Vector3(0, 1.8f, 0);
        private Quaternion simulatedHeadRot = Quaternion.identity;
        private Vector3 simulatedLeftHandPos = new Vector3(-0.3f, 1.5f, 0.3f);
        private Quaternion simulatedLeftHandRot = Quaternion.identity;
        private Vector3 simulatedRightHandPos = new Vector3(0.3f, 1.5f, 0.3f);
        private Quaternion simulatedRightHandRot = Quaternion.identity;
        
        private bool isSimulating = false;
        
        private void Start()
        {
            networkPlayer = GetComponent<NetworkVRPlayer>();
            
            if (enableSimulation && networkPlayer != null && Application.isEditor)
            {
                Debug.Log("VR Simulator enabled - Use WASD to move head, mouse to look around");
                Debug.Log("Q/E - Left hand up/down, U/O - Right hand up/down, R - Reset pose");
                isSimulating = true;
            }
        }
        
        private void Update()
        {
            if (!isSimulating || networkPlayer == null) return;
            
            UpdateHeadSimulation();
            UpdateHandSimulation();
            ApplySimulatedInput();
            
            // Show instructions
            if (Input.GetKeyDown(KeyCode.F1))
            {
                ShowInstructions();
            }
        }
        
        private void UpdateHeadSimulation()
        {
            // Head movement with WASD
            Vector3 moveInput = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) moveInput += Vector3.forward;
            if (Input.GetKey(KeyCode.S)) moveInput += Vector3.back;
            if (Input.GetKey(KeyCode.A)) moveInput += Vector3.left;
            if (Input.GetKey(KeyCode.D)) moveInput += Vector3.right;
            if (Input.GetKey(KeyCode.Space)) moveInput += Vector3.up;
            if (Input.GetKey(KeyCode.LeftControl)) moveInput += Vector3.down;
            
            simulatedHeadPos += moveInput * moveSpeed * Time.deltaTime;
            
            // Head rotation with mouse
            if (Input.GetMouseButton(1)) // Right mouse button
            {
                float mouseX = Input.GetAxis("Mouse X") * rotationSpeed * Time.deltaTime;
                float mouseY = Input.GetAxis("Mouse Y") * rotationSpeed * Time.deltaTime;
                
                simulatedHeadRot *= Quaternion.Euler(-mouseY, mouseX, 0);
            }
        }
        
        private void UpdateHandSimulation()
        {
            // Left hand movement
            if (Input.GetKey(leftHandUp))
                simulatedLeftHandPos += Vector3.up * handMoveSpeed * Time.deltaTime;
            if (Input.GetKey(leftHandDown))
                simulatedLeftHandPos += Vector3.down * handMoveSpeed * Time.deltaTime;
            
            // Right hand movement  
            if (Input.GetKey(rightHandUp))
                simulatedRightHandPos += Vector3.up * handMoveSpeed * Time.deltaTime;
            if (Input.GetKey(rightHandDown))
                simulatedRightHandPos += Vector3.down * handMoveSpeed * Time.deltaTime;
            
            // Reset pose
            if (Input.GetKeyDown(resetPose))
            {
                ResetToDefaultPose();
            }
        }
        
        private void ApplySimulatedInput()
        {
            // Apply simulated VR input to the network player
            // This simulates what would normally come from VR headset/controllers
            
            // Note: This is a simplified simulation
            // In a real implementation, you'd need to modify NetworkVRPlayer
            // to accept simulated input when no VR device is present
            
            transform.position = simulatedHeadPos;
            transform.rotation = simulatedHeadRot;
        }
        
        private void ResetToDefaultPose()
        {
            simulatedHeadPos = new Vector3(0, 1.8f, 0);
            simulatedHeadRot = Quaternion.identity;
            simulatedLeftHandPos = new Vector3(-0.3f, 1.5f, 0.3f);
            simulatedLeftHandRot = Quaternion.identity;
            simulatedRightHandPos = new Vector3(0.3f, 1.5f, 0.3f);
            simulatedRightHandRot = Quaternion.identity;
            
            Debug.Log("VR pose reset to default");
        }
        
        private void ShowInstructions()
        {
            Debug.Log("=== VR SIMULATOR CONTROLS ===");
            Debug.Log("WASD: Move head position");
            Debug.Log("Right Mouse + Mouse Move: Rotate head");
            Debug.Log("Space/Ctrl: Move head up/down");
            Debug.Log("Q/E: Move left hand up/down");
            Debug.Log("U/O: Move right hand up/down");
            Debug.Log("R: Reset to default pose");
            Debug.Log("F1: Show this help");
        }
        
        private void OnGUI()
        {
            if (!isSimulating) return;
            
            GUI.Label(new Rect(10, 10, 300, 20), "VR Simulator Active");
            GUI.Label(new Rect(10, 30, 300, 20), "F1 for controls help");
            GUI.Label(new Rect(10, 50, 300, 20), $"Head: {simulatedHeadPos:F1}");
        }
    }
} 