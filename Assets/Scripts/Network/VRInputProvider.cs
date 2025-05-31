using Fusion;
using UnityEngine;
using VRMultiplayer.VR;

namespace VRMultiplayer.Network
{
    /// <summary>
    /// VR Input Provider that implements INetworkInput for Photon Fusion
    /// Gathers all VR input data and provides it to the network system
    /// </summary>
    public class VRInputProvider : MonoBehaviour, INetworkInput
    {
        [SerializeField] private bool logOnInput = false; // For conditional logging

        // Network VR Player reference
        private NetworkVRPlayer networkPlayer;
        
        public void Initialize(NetworkVRPlayer player)
        {
            networkPlayer = player;
            Debug.Log("[VRInputProvider] Initialized with NetworkVRPlayer: " + (player != null ? player.Object.InputAuthority.ToString() : "null"));
        }
        
        // INetworkInput implementation
        public void OnInput(NetworkRunner runner, NetworkInputData input)
        {
            if (networkPlayer == null)
            {
                if (logOnInput) Debug.LogWarning("[VRInputProvider] OnInput called but networkPlayer is null.");
                return;
            }

            if (logOnInput)
            {
                Debug.Log($"[VRInputProvider] OnInput called for runner. Tick: {runner.Tick}. Providing input for player: {networkPlayer.Object.InputAuthority}");
            }
            
            // Gather the most recent input data
            NetworkVRPlayer.VRNetworkInputData currentInputData = networkPlayer.GatherInput();
            
            // Set the input data for the network
            input.Set(currentInputData);
        }
    }
    
    // TODO: This enum should be moved to a more appropriate location if it's used elsewhere.
    // For now, keeping it here to avoid breaking compilation if it's only used by the removed methods.
    // If NetworkVRPlayer.GatherInput() handles button input directly, this might be obsolete or live with button definitions.
    public enum VRInputButton
    {
        Teleport,
        Menu,
        Primary,
        Secondary
    }
}