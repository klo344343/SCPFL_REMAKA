using Mirror;
using UnityEngine;
// UnityEngine.Networking is generally not needed when using Mirror directly for networking logic
// but may be present in Dissonance's original UNet integration files.
// Remove if not strictly necessary for Dissonance's internal workings.
// using UnityEngine.Networking; 

namespace Dissonance.Integrations.UNet_HLAPI
{
    // Ensure this script is on a GameObject with a NetworkIdentity component
    [RequireComponent(typeof(NetworkIdentity))]
    public class HlapiPlayer : NetworkBehaviour, IDissonancePlayer
    {
        private static readonly Log Log = Log.GetLog("HlapiPlayer"); 

        private DissonanceComms _dissonanceComms;

        [SyncVar(hook = nameof(OnPlayerIdChanged))]
        private string _networkPlayerId;

        public bool IsTracking { get; private set; }

        public string PlayerId => _networkPlayerId;

        public Vector3 Position => transform.position;

        public Quaternion Rotation => transform.rotation;

        public NetworkPlayerType Type => isLocalPlayer ? NetworkPlayerType.Local : NetworkPlayerType.Remote;

        // --- Unity Lifecycle Methods ---

        private void Awake()
        {
            // Try to find the DissonanceComms instance early, if it's already available.
            // OnEnable also does this, but Awake is good for ensuring dependencies are met before other scripts start.
            _dissonanceComms = FindObjectOfType<DissonanceComms>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // When a client starts and receives the initial SyncVar state,
            // the OnPlayerIdChanged hook will be called, which handles StartTracking.
        }

        public override void OnStartAuthority()
        {
            base.OnStartAuthority();

            // This code runs only on the client that owns this NetworkBehaviour (local player).

            // Ensure DissonanceComms is available. If not, something is wrong with the scene setup.
            if (_dissonanceComms == null)
            {
                throw Log.CreateUserErrorException(
                    "Cannot find DissonanceComms component in scene.",
                    "Ensure a DissonanceComms component is placed on a GameObject in the scene.",
                    "https://dissonance.readthedocs.io/en/latest/Basics/Quick-Start-UNet-HLAPI/", // Check if this URL is still valid for Mirror
                    "9A79FDCB-263E-4124-B54D-67EDA39C09A5"
                );
            }

            // Immediately set the player's name if Dissonance already has it.
            if (_dissonanceComms.LocalPlayerName != null)
            {
                // We use a Command to send the local player's name to the server.
                CmdSetPlayerName(_dissonanceComms.LocalPlayerName);
            }

            // Subscribe to name changes. If the local player's name changes in Dissonance,
            // we'll update it on the network.
            _dissonanceComms.LocalPlayerNameChanged += CmdSetPlayerName; // Directly call Command on change
        }

        public override void OnStopAuthority()
        {
            base.OnStopAuthority();

            // When local authority is lost (e.g., disconnecting), unsubscribe.
            if (_dissonanceComms != null)
            {
                _dissonanceComms.LocalPlayerNameChanged -= CmdSetPlayerName;
            }
        }

        public void OnDestroy()
        {
            // Ensure we unsubscribe from the event to prevent memory leaks if the object is destroyed.
            if (_dissonanceComms != null)
            {
                _dissonanceComms.LocalPlayerNameChanged -= CmdSetPlayerName;
            }
        }

        public void OnEnable()
        {
            // Try to find DissonanceComms if not already found (e.g., if scene loads late).
            if (_dissonanceComms == null)
            {
                _dissonanceComms = FindObjectOfType<DissonanceComms>();
            }
        }

        public void OnDisable()
        {
            // If the component is disabled, stop tracking this player's position with Dissonance.
            if (IsTracking)
            {
                StopTracking();
            }
        }

        // --- SyncVar Hook for _networkPlayerId ---

        /// <summary>
        /// Called on all clients (including the local client) when the _networkPlayerId SyncVar changes.
        /// </summary>
        /// <param name="oldId">The previous player ID.</param>
        /// <param name="newId">The new player ID.</param>
        private void OnPlayerIdChanged(string oldId, string newId)
        {
            // If the ID has changed to a non-empty string, ensure tracking is started.
            // If it changed from a tracked ID, ensure old tracking is stopped.
            if (!string.IsNullOrEmpty(newId) && _dissonanceComms != null)
            {
                // Stop tracking with the old ID if it was active
                if (IsTracking && !string.IsNullOrEmpty(oldId) && oldId != newId)
                {
                    StopTracking(); // Will set IsTracking to false
                }

                // Start tracking with the new ID
                if (!IsTracking) // Only if not already tracking (StopTracking might have set it to false)
                {
                    _dissonanceComms.TrackPlayerPosition(this);
                    IsTracking = true;
                }
            }
            else if (IsTracking && string.IsNullOrEmpty(newId)) // If new ID is empty and we were tracking
            {
                StopTracking();
            }
        }

        // --- Commands (Client -> Server) ---

        /// <summary>
        /// Command sent from the client (with authority) to the server to set the player's Dissonance ID.
        /// </summary>
        /// <param name="playerName">The desired player name/ID.</param>
        [Command]
        private void CmdSetPlayerName(string playerName)
        {
            // Server sets the SyncVar. Mirror automatically handles synchronization to all clients,
            // which will trigger OnPlayerIdChanged on each client.
            _networkPlayerId = playerName;
            // No need for an Rpc here; the SyncVar hook handles client updates.
        }

        // --- Tracking Management ---

        /// <summary>
        /// Starts tracking this player's position and voice with Dissonance.
        /// </summary>
        private void StartTracking()
        {
            if (IsTracking)
            {
                Log.Warn("Attempting to start player tracking, but tracking is already started.");
                return; // Already tracking, do nothing
            }

            if (_dissonanceComms != null && !string.IsNullOrEmpty(PlayerId))
            {
                _dissonanceComms.TrackPlayerPosition(this);
                IsTracking = true;
            }
            else
            {
                // Log a warning if we can't start tracking (e.g., comms not found, or PlayerId is empty)
                Log.Error($"Failed to start tracking player. Comms: {_dissonanceComms != null}, PlayerId empty: {string.IsNullOrEmpty(PlayerId)}");
            }
        }

        /// <summary>
        /// Stops tracking this player's position and voice with Dissonance.
        /// </summary>
        private void StopTracking()
        {
            if (!IsTracking)
            {
                Log.Warn("Attempting to stop player tracking, but tracking is not started.");
                return; // Not tracking, do nothing
            }

            if (_dissonanceComms != null)
            {
                _dissonanceComms.StopTracking(this);
                IsTracking = false;
            }
            else
            {
                Log.Error("Failed to stop tracking player. DissonanceComms not found.");
            }
        }
    }
}