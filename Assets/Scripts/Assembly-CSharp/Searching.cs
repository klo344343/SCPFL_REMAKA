using Mirror;
using System;
using System.Collections.Generic; // Required for Dictionary
using UnityEngine;
using UnityEngine.UI; // Required for UI elements

public class Searching : NetworkBehaviour
{
    // --- Dependencies ---
    [Header("Dependencies")]
    [SerializeField] private CharacterClassManager _characterClassManager;
    [SerializeField] private Inventory _inventory;
    [SerializeField] private AmmoBox _ammoBox;
    [SerializeField] private FirstPersonController _firstPersonController;
    [SerializeField] private PlayerInteract _playerInteract; // Assuming this has the mask

    // Directly assignable references for UI components in the Inspector
    [Header("UI References")]
    [SerializeField] private GameObject _overloadErrorMessageGO; // Reference to the error message GameObject
    [SerializeField] private Slider _pickupProgressBar;        // Reference to the progress slider
    [SerializeField] private GameObject _pickupProgressGO;     // Reference to the progress UI GameObject

    // --- Client-Side State ---
    private Transform _playerCameraTransform; // Cached camera transform
    private GameObject _currentClientPickupTarget; // The GameObject the local client is currently trying to pick up
    private float _clientPickupTimer; // Tracks client's local progress for UI
    private float _clientErrorMessageDuration; // Tracks how long the error message should be shown

    private readonly Dictionary<int, ServerPickupState> _serverPickupStates = new Dictionary<int, ServerPickupState>();

    private struct ServerPickupState
    {
        public GameObject TargetObject;
        public float RemainingTime; // Time remaining for pickup
        public bool IsActive;

        public ServerPickupState(GameObject target, float time)
        {
            TargetObject = target;
            RemainingTime = time;
            IsActive = true;
        }
    }

    // --- Configurable Values ---
    [Header("Config")]
    public float maxRayDistance = 3.5f; // Max distance for raycast interaction

    // --- Internal State Flags ---
    private bool _isHuman; // Cached from CharacterClassManager

    // --- Static Log (Assuming Dissonance.Log is available) ---
    private static readonly Dissonance.Log Log = Dissonance.Log.GetLog("Searching");


    private void Awake()
    {
        _firstPersonController = GetComponent<FirstPersonController>();
        _characterClassManager = GetComponent<CharacterClassManager>();
        _inventory = GetComponent<Inventory>();
        _ammoBox = GetComponent<AmmoBox>();
        _playerInteract = GetComponent<PlayerInteract>(); // Assuming this is on the same GameObject

        if (UserMainInterface.singleton != null)
        {
            _overloadErrorMessageGO = UserMainInterface.singleton.overloadMsg;
            _pickupProgressBar = UserMainInterface.singleton.searchProgress;
            _pickupProgressGO = UserMainInterface.singleton.searchOBJ;
        }
        else
        {
            Log.Warn("UserMainInterface.singleton not found. UI references will be null.");
        }
    }

    private void Start()
    {
        // PlayerCameraGameObject is likely initialized in Start or later, so get its transform here
        Scp049PlayerScript scp049Script = GetComponent<Scp049PlayerScript>();
        if (scp049Script != null)
        {
            _playerCameraTransform = scp049Script.PlayerCameraGameObject?.transform;
        }
        else
        {
            Log.Error("Scp049PlayerScript not found on player. Cannot get camera transform for interaction.");
        }
    }

    // This method sets the initial human state, called by external logic.
    public void InitPlayerState(bool isHumanState)
    {
        _isHuman = isHumanState;
    }

    private void Update()
    {
        if (isLocalPlayer)
        {
            HandleClientRaycastInteraction();
            HandleClientPickupProgress();
            HandleClientErrorMessageDisplay();
        }

        if (isServer)
        {
            UpdateServerPickupStates();
        }
    }

    /// <summary>
    /// Server-side: Updates all active pickup timers.
    /// </summary>
    private void UpdateServerPickupStates()
    {
        // Create a list of connection IDs to process to avoid modifying _serverPickupStates during iteration
        List<int> connectionsToUpdate = new List<int>(_serverPickupStates.Keys);

        foreach (int connectionId in connectionsToUpdate)
        {
            if (_serverPickupStates.TryGetValue(connectionId, out ServerPickupState state))
            {
                if (state.IsActive)
                {
                    state.RemainingTime -= Time.deltaTime;
                    _serverPickupStates[connectionId] = state; // Update the struct in the dictionary

                    // If time runs out, attempt to complete the pickup
                    if (state.RemainingTime <= 0f)
                    {
                        // Set inactive immediately to prevent multiple attempts for same item
                        state.IsActive = false;
                        _serverPickupStates[connectionId] = state;

                        // Attempt to pick up the item
                        TryPickupItemOnServer(connectionToClient, state.TargetObject);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Client-side: Handles raycasting for interaction input.
    /// </summary>
    private void HandleClientRaycastInteraction()
    {
        // Only react on interaction key down if not already picking something up
        if (Input.GetKeyDown(NewInput.GetKey("Interact")) && _currentClientPickupTarget == null)
        {
            if (!AllowClientPickup())
            {
                ShowErrorMessageClient();
                return;
            }

            if (_playerCameraTransform == null || _playerInteract == null)
            {
                Log.Warn("Camera transform or PlayerInteract component not assigned. Cannot raycast.");
                return;
            }

            RaycastHit hitInfo;
            if (Physics.Raycast(new Ray(_playerCameraTransform.position, _playerCameraTransform.forward), out hitInfo, maxRayDistance, _playerInteract.mask))
            {
                Pickup pickupComponent = hitInfo.transform.GetComponentInParent<Pickup>();
                Locker lockerComponent = hitInfo.transform.GetComponentInParent<Locker>();

                if (pickupComponent != null)
                {
                    // Check if inventory has space or if it's a non-equipable item (like ammo)
                    // This is a client-side *prediction* and will be re-verified on the server.
                    if (_inventory.items.Count < 8 || _inventory.availableItems[pickupComponent.info.itemId].noEquipable)
                    {
                        _currentClientPickupTarget = pickupComponent.gameObject;
                        _clientPickupTimer = pickupComponent.searchTime;

                        // Start progress bar UI
                        if (_pickupProgressBar != null)
                        {
                            _pickupProgressBar.maxValue = pickupComponent.searchTime;
                            _pickupProgressBar.value = _pickupProgressBar.maxValue - _clientPickupTimer;
                            _pickupProgressGO?.SetActive(true);
                        }

                        _firstPersonController.isSearching = true; // Update character state
                        CmdStartPickup(_currentClientPickupTarget); // Inform server to start tracking
                    }
                    else
                    {
                        ShowErrorMessageClient(); // Inventory full
                    }
                }
                else if (lockerComponent != null)
                {
                    // Immediately try to open locker (no progress bar for lockers in this implementation)
                    CmdOpenLocker(lockerComponent.gameObject);
                }
            }
        }
    }

    /// <summary>
    /// Client-side: Manages the pickup progress and cancellation.
    /// </summary>
    private void HandleClientPickupProgress()
    {
        if (_currentClientPickupTarget != null)
        {
            // If the interact key is released, cancel the pickup
            if (!Input.GetKey(NewInput.GetKey("Interact")))
            {
                CancelClientPickup();
                CmdAbortPickup(); // Inform server of cancellation
                return;
            }

            _clientPickupTimer -= Time.deltaTime;

            // Update UI progress bar
            if (_pickupProgressBar != null)
            {
                _pickupProgressBar.value = _pickupProgressBar.maxValue - _clientPickupTimer;
                _pickupProgressGO?.SetActive(true);
            }

            _firstPersonController.isSearching = true; // Keep character in searching state

            // The actual pickup completion is handled by the server.
            // Client UI will be reset when ServerRpcPickupCompleted or ServerRpcPickupFailed is received.
        }
        else
        {
            // If nothing is being picked up, ensure UI and state are reset
            if (_firstPersonController.isSearching)
            {
                _firstPersonController.isSearching = false;
            }
            _pickupProgressGO?.SetActive(false);
        }
    }

    /// <summary>
    /// Client-side: Resets client-side pickup state and UI.
    /// </summary>
    private void CancelClientPickup()
    {
        _currentClientPickupTarget = null;
        _firstPersonController.isSearching = false;
        _pickupProgressGO?.SetActive(false);
        _clientPickupTimer = 0f;
    }

    /// <summary>
    /// Client-side: Shows the "inventory full" error message for a duration.
    /// </summary>
    public void ShowErrorMessageClient()
    {
        _clientErrorMessageDuration = 2f; // Show for 2 seconds
    }

    /// <summary>
    /// Client-side: Manages the display of the error message.
    /// </summary>
    private void HandleClientErrorMessageDisplay()
    {
        if (_clientErrorMessageDuration > 0f)
        {
            _clientErrorMessageDuration -= Time.deltaTime;
        }
        _overloadErrorMessageGO?.SetActive(_clientErrorMessageDuration > 0f); // Activate/deactivate based on timer
    }

    /// <summary>
    /// Client-side: Checks if the player is allowed to initiate a pickup.
    /// </summary>
    /// <returns>True if pickup is allowed, false otherwise.</returns>
    private bool AllowClientPickup()
    {
        if (!_isHuman) // Check if the player is in a human class
        {
            return false;
        }

        // Check for handcuffs (assuming Handcuffs component exists and logic is correct)
        // This loop can be optimized if player objects are easily accessible.
        GameObject[] players = PlayerManager.singleton?.players; // Ensure PlayerManager.singleton is not null
        if (players != null)
        {
            foreach (GameObject player in players)
            {
                Handcuffs cuffs = player.GetComponent<Handcuffs>();
                if (cuffs != null && cuffs.cuffTarget == gameObject)
                {
                    return false; // Player is handcuffed
                }
            }
        }
        return true;
    }

    // --- Commands (Client to Server) ---

    /// <summary>
    /// Command: Notifies the server that the client has started a pickup attempt.
    /// Server will begin tracking progress for this client.
    /// </summary>
    /// <param name="targetItem">The GameObject of the item the client is trying to pick up.</param>
    [Command(channel = Channels.Reliable)] // Use Channels.Reliable for critical commands
    private void CmdStartPickup(GameObject targetItem)
    {
        // Basic server-side validation
        if (targetItem == null || !_characterClassManager.IsHuman() || Vector3.Distance(connectionToClient.identity.transform.position, targetItem.transform.position) > maxRayDistance + 0.5f) // Add a small buffer
        {
            Log.Debug($"CmdStartPickup: Invalid target, not human, or too far. Client: {connectionToClient.connectionId}");
            return;
        }

        Pickup pickupComponent = targetItem.GetComponent<Pickup>();
        if (pickupComponent == null)
        {
            Log.Debug($"CmdStartPickup: Target {targetItem.name} is not a Pickup. Client: {connectionToClient.connectionId}");
            return;
        }

        // Check inventory space on server (server authoritative)
        if (_inventory.items.Count >= 8 && !_inventory.availableItems[pickupComponent.info.itemId].noEquipable)
        {
            Log.Debug($"CmdStartPickup: Inventory full for client {connectionToClient.connectionId}");
            // Inform client about the full inventory
            TargetRpcShowErrorMessage(connectionToClient);
            return;
        }

        // Store or update this client's pickup state on the server
        _serverPickupStates[connectionToClient.connectionId] = new ServerPickupState(targetItem, pickupComponent.searchTime);
        Log.Debug($"CmdStartPickup: Client {connectionToClient.connectionId} started pickup for {targetItem.name}. Time: {pickupComponent.searchTime}");
    }

    /// <summary>
    /// Command: Notifies the server that the client has aborted a pickup attempt.
    /// </summary>
    [Command(channel = Channels.Reliable)]
    private void CmdAbortPickup()
    {
        // Remove or mark as inactive this client's pickup state on the server
        if (_serverPickupStates.ContainsKey(connectionToClient.connectionId))
        {
            // Instead of removing, mark as inactive to prevent immediate re-start in the same frame if logic allows
            ServerPickupState state = _serverPickupStates[connectionToClient.connectionId];
            state.IsActive = false;
            _serverPickupStates[connectionToClient.connectionId] = state;
            Log.Debug($"CmdAbortPickup: Client {connectionToClient.connectionId} aborted pickup.");
        }
    }

    /// <summary>
    /// Command: Notifies the server that the client *thinks* a pickup is complete,
    /// but the server will perform the actual validation and item transfer.
    /// This command is deprecated as server now triggers pickup completion by timer.
    /// </summary>
    // [Command(channel = Channels.Reliable)]
    // private void CmdPickupItem(GameObject targetItem)
    // {
    //     // This command is no longer needed as the server will automatically trigger pickup completion
    //     // when _serverPickupStates[connId].RemainingTime reaches zero.
    //     // The TryPickupItemOnServer method now handles the logic.
    // }

    /// <summary>
    /// Server-side: Attempts to complete the pickup for a client.
    /// This is called internally on the server when the pickup timer runs out.
    /// </summary>
    /// <param name="conn">The connection of the client performing the pickup.</param>
    /// <param name="targetObject">The GameObject being picked up.</param>
    [Server]
    private void TryPickupItemOnServer(NetworkConnectionToClient conn, GameObject targetObject)
    {
        if (targetObject == null)
        {
            Log.Debug($"TryPickupItemOnServer: Target object is null for client {conn.connectionId}.");
            TargetRpcPickupFailed(conn);
            return;
        }

        // Re-validate all conditions on the server, for security and consistency
        if (!_characterClassManager.IsHuman() || Vector3.Distance(conn.identity.transform.position, targetObject.transform.position) > maxRayDistance + 0.5f)
        {
            Log.Debug($"TryPickupItemOnServer: Validation failed for client {conn.connectionId}. Not human or too far.");
            TargetRpcPickupFailed(conn);
            return;
        }

        Pickup pickupComponent = targetObject.GetComponent<Pickup>();
        if (pickupComponent != null)
        {
            // Final inventory check on server
            if (_inventory.items.Count >= 8 && !_inventory.availableItems[pickupComponent.info.itemId].noEquipable)
            {
                Log.Debug($"TryPickupItemOnServer: Inventory full on server for client {conn.connectionId}.");
                TargetRpcShowErrorMessage(conn); // Tell client inventory is full
                TargetRpcPickupFailed(conn); // Tell client pickup failed
                return;
            }

            int itemId = pickupComponent.info.itemId;
            // The item must be deleted on the server, which will synchronize its removal to clients.
            NetworkServer.Destroy(targetObject); // Or call a specific method like pickupComponent.Delete(); if it handles NetworkServer.Destroy internally.

            if (itemId != -1)
            {
                // Add item to client's inventory on the server (SyncVars/SyncLists handle replication)
                AddItemToServer(conn.identity.gameObject, itemId, pickupComponent.info.durability, pickupComponent.info.weaponMods);

                // Achievement should be given by the server after successful pickup
                TargetRpcGrantAchievement(conn, "thatcanbeusefull"); // RPC to client to grant achievement

                Log.Info($"Client {conn.connectionId} successfully picked up item {itemId}.");
                TargetRpcPickupCompleted(conn); // Notify client of success to reset UI
            }
        }
    }


    /// <summary>
    /// Command: Opens a locker. Separate command for clarity.
    /// </summary>
    /// <param name="targetLocker">The GameObject of the locker to open.</param>
    [Command(channel = Channels.Reliable)]
    private void CmdOpenLocker(GameObject targetLocker)
    {
        if (targetLocker == null || !_characterClassManager.IsHuman() || Vector3.Distance(connectionToClient.identity.transform.position, targetLocker.transform.position) > maxRayDistance + 0.5f)
        {
            Log.Debug($"CmdOpenLocker: Invalid target, not human, or too far. Client: {connectionToClient.connectionId}");
            return;
        }

        Locker lockerComponent = targetLocker.GetComponent<Locker>();
        if (lockerComponent != null && !lockerComponent.isOpen)
        {
            lockerComponent.Open(); // Assuming Open() is a [Server] method that synchronizes its state.
            Log.Info($"Client {connectionToClient.connectionId} opened locker {targetLocker.name}.");
        }
    }

    // --- RPCs (Server to Client) ---

    /// <summary>
    /// RPC: Informs a specific client that their inventory is full.
    /// </summary>
    /// <param name="target">The connection of the client to send the message to.</param>
    [TargetRpc]
    private void TargetRpcShowErrorMessage(NetworkConnection target)
    {
        ShowErrorMessageClient();
    }

    /// <summary>
    /// RPC: Informs a specific client that their pickup attempt was successful.
    /// Used to reset client-side UI.
    /// </summary>
    /// <param name="target">The connection of the client to send the message to.</param>
    [TargetRpc]
    private void TargetRpcPickupCompleted(NetworkConnection target)
    {
        Log.Debug("TargetRpcPickupCompleted: Resetting client pickup UI.");
        CancelClientPickup(); // Reset local UI and state
    }

    /// <summary>
    /// RPC: Informs a specific client that their pickup attempt failed.
    /// Used to reset client-side UI and potentially provide feedback.
    /// </summary>
    /// <param name="target">The connection of the client to send the message to.</param>
    [TargetRpc]
    private void TargetRpcPickupFailed(NetworkConnection target)
    {
        Log.Debug("TargetRpcPickupFailed: Resetting client pickup UI.");
        CancelClientPickup(); // Reset local UI and state
        // Optionally show a "pickup failed" message if distinct from "inventory full"
    }

    /// <summary>
    /// RPC: Grants an achievement on a specific client.
    /// </summary>
    /// <param name="target">The connection of the client to grant the achievement to.</param>
    /// <param name="achievementId">The ID of the achievement.</param>
    [TargetRpc]
    private void TargetRpcGrantAchievement(NetworkConnection target, string achievementId)
    {
        AchievementManager.Achieve(achievementId);
    }

    // --- Server-Side Add Item Logic ---

    /// <summary>
    /// Server-side: Adds an item to the player's inventory.
    /// Assumes Inventory and AmmoBox components have SyncVars/SyncLists
    /// that will automatically synchronize the changes to clients.
    /// </summary>
    /// <param name="playerGameObject">The player GameObject (on the server).</param>
    /// <param name="id">The item ID.</param>
    /// <param name="dur">Durability of the item.</param>
    /// <param name="mods">Weapon mods for the item.</param>
    [Server]
    public void AddItemToServer(GameObject playerGameObject, int id, float dur, int[] mods)
    {
        // Get server-side components for the player
        Inventory playerInventory = playerGameObject.GetComponent<Inventory>();
        AmmoBox playerAmmoBox = playerGameObject.GetComponent<AmmoBox>();

        if (playerInventory == null)
        {
            Log.Error($"AddItemToServer: Inventory component not found on player {playerGameObject.name}.");
            return;
        }

        if (mods == null)
        {
            mods = new int[3]; // Initialize to default if null
        }
        if (mods.Length != 3)
        {
            Array.Resize(ref mods, 3); // Ensure correct size
        }

        if (id == -1) // Invalid item ID
        {
            return;
        }

        // Check if it's an equipable item
        if (!playerInventory.availableItems[id].noEquipable)
        {
            WeaponManager weaponManager = playerGameObject.GetComponent<WeaponManager>();
            if (weaponManager != null)
            {
                foreach (WeaponManager.Weapon weapon in weaponManager.weapons)
                {
                    if (weapon.inventoryID == id)
                    {
                        // Clamp mods to valid ranges based on weapon properties
                        mods[0] = Mathf.Clamp(mods[0], 0, weapon.mod_sights.Length - 1);
                        mods[1] = Mathf.Clamp(mods[1], 0, weapon.mod_barrels.Length - 1);
                        mods[2] = Mathf.Clamp(mods[2], 0, weapon.mod_others.Length - 1);
                        break; // Found the weapon type, no need to continue loop
                    }
                }
            }
            // Add the item to the server's inventory (this will then sync to clients)
            playerInventory.AddNewItem(id, (dur != -1f) ? dur : playerInventory.availableItems[id].durability, mods[0], mods[1], mods[2]);
            Log.Debug($"Server: Added equipable item {id} to {playerGameObject.name}'s inventory.");
        }
        else // It's a non-equipable item, likely ammo
        {
            if (playerAmmoBox == null)
            {
                Log.Error($"AddItemToServer: AmmoBox component not found on player {playerGameObject.name} for non-equipable item {id}.");
                return;
            }

            // Parse current ammo amounts (assuming NetworkAmount is a SyncVar<string> or similar)
            string[] ammoAmounts = playerAmmoBox.NetworkAmount.Split(':');
            int[] currentAmmo = new int[3];
            for (int i = 0; i < 3; i++)
            {
                if (int.TryParse(ammoAmounts[i], out int amount))
                {
                    currentAmmo[i] = amount;
                }
            }

            // Find the correct ammo type and add amount
            for (int j = 0; j < playerAmmoBox.types.Length; j++)
            {
                if (playerAmmoBox.types[j].inventoryID == id)
                {
                    currentAmmo[j] += Mathf.RoundToInt(dur); // Assuming dur is the amount of ammo
                    break;
                }
            }
            playerAmmoBox.NetworkAmount = $"{currentAmmo[0]}:{currentAmmo[1]}:{currentAmmo[2]}";
            Log.Debug($"Server: Added ammo {id} (amount {dur}) to {playerGameObject.name}'s ammo box. New amounts: {playerAmmoBox.NetworkAmount}");
        }
    }
}