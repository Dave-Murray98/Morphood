using Sirenix.OdinInspector;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// Base class for all stations in the game (cooking, chopping, serving, plain storage, etc.)
/// Handles item placement, basic station functionality, and player interaction permissions.
/// </summary>
public abstract class BaseStation : MonoBehaviour
{
    [Header("Station Configuration")]
    [SerializeField] protected string stationName = "Station";
    [Tooltip("Display name for this station")]

    [SerializeField] protected StationType stationType = StationType.Plain;
    [Tooltip("What type of station this is")]

    [Header("Item Placement")]
    [SerializeField] protected Transform itemPlacementPoint;
    [Tooltip("Where items will be positioned when placed on this station")]

    [SerializeField] protected Vector3 placementOffset = Vector3.zero;
    [Tooltip("Additional offset from the placement point")]

    [SerializeField] protected int maxItemCapacity = 1;
    [Tooltip("Maximum number of items this station can hold")]

    [Header("Player Permissions")]
    [SerializeField] protected bool allowPlayer1Interaction = true;
    [SerializeField] protected bool allowPlayer2Interaction = true;
    [Tooltip("Which players can interact with this station")]

    [Header("Debug")]
    [SerializeField] protected bool enableDebugLogs = false;
    [SerializeField] protected bool showStationGizmos = true;

    // Internal state
    [ShowInInspector] protected GameObject currentItem;
    protected PickupableItem currentPickupableItem; // Reference to the PickupableItem component
    [ShowInInspector] protected bool isOccupied = false;

    // Events for other systems to react to
    public System.Action<GameObject, PlayerEnd> OnItemPlaced;
    public System.Action<GameObject, PlayerEnd> OnItemRemoved;

    // Public properties
    public string StationName => stationName;
    public StationType Type => stationType;
    public bool IsOccupied => isOccupied;
    public GameObject CurrentItem => currentItem;
    public int ItemCount => isOccupied ? 1 : 0;
    public bool HasSpace => !isOccupied && ItemCount < maxItemCapacity;

    protected virtual void Start()
    {
        Initialize();
    }

    protected virtual void Initialize()
    {
        // Set up placement point if not assigned
        if (itemPlacementPoint == null)
        {
            GameObject placementObj = new GameObject($"{stationName}_PlacementPoint");
            placementObj.transform.SetParent(transform);
            placementObj.transform.localPosition = Vector3.up * 1f; // Default height
            itemPlacementPoint = placementObj.transform;

            DebugLog("Created default placement point");
        }

        // Ensure the station has a collider for PlayerEnd detection
        if (GetComponent<Collider>() == null)
        {
            Debug.LogWarning($"[{stationName}] Station has no collider! PlayerEnds won't be able to detect it. Add a collider component.");
        }

        DebugLog($"Station '{stationName}' initialized as type: {stationType}");
    }

    #region Player Interaction

    /// <summary>
    /// Check if a specific player can interact with this station
    /// </summary>
    public virtual bool CanPlayerInteract(PlayerEnd playerEnd)
    {
        switch (playerEnd.PlayerNumber)
        {
            case 1: return allowPlayer1Interaction;
            case 2: return allowPlayer2Interaction;
            default: return false;
        }
    }

    /// <summary>
    /// Check if this station can accept a specific item from a specific player
    /// </summary>
    public virtual bool CanAcceptItem(GameObject item, PlayerEnd playerEnd)
    {
        // Check player permissions
        if (!CanPlayerInteract(playerEnd))
        {
            DebugLog($"Player {playerEnd.PlayerNumber} not allowed to interact with this station");
            return false;
        }

        // Check if station has space
        if (!HasSpace)
        {
            DebugLog($"Station is full, cannot accept more items. isOccupied: {isOccupied}, ItemCount: {ItemCount}, MaxCapacity: {maxItemCapacity}");
            return false;
        }

        // Allow derived classes to add custom logic
        bool customCheck = CanAcceptItemCustom(item, playerEnd);
        if (!customCheck)
        {
            DebugLog($"Custom acceptance check failed for {item.name}");
            return false;
        }

        DebugLog($"Station can accept {item.name} from Player {playerEnd.PlayerNumber}");
        return true;
    }

    /// <summary>
    /// Override this in derived classes to add custom acceptance logic
    /// </summary>
    protected virtual bool CanAcceptItemCustom(GameObject item, PlayerEnd playerEnd)
    {
        return true;
    }

    #endregion

    #region Item Management

    /// <summary>
    /// Place an item on this station
    /// </summary>
    public virtual bool PlaceItem(GameObject item, PlayerEnd playerEnd)
    {
        DebugLog($"Attempting to place {item.name}. Current state - isOccupied: {isOccupied}, HasSpace: {HasSpace}");

        if (!CanAcceptItem(item, playerEnd))
        {
            DebugLog($"Cannot place {item.name} on station");
            return false;
        }

        // Position the item
        item.transform.SetParent(itemPlacementPoint);
        item.transform.localPosition = placementOffset;
        item.transform.localRotation = Quaternion.identity;

        FoodItem foodItem = item.GetComponent<FoodItem>();
        if (foodItem != null)
        {
            item.transform.localPosition += Vector3.up * foodItem.FoodData.yPositionPlacementOffset;
        }

        // Enable physics but make it kinematic (station controls position)
        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
        }

        // Get reference to PickupableItem component and disable direct interaction
        PickupableItem pickupableItem = item.GetComponent<PickupableItem>();
        if (pickupableItem != null)
        {
            pickupableItem.SetDirectInteractionEnabled(false);
            currentPickupableItem = pickupableItem;
            DebugLog($"Disabled direct interaction for {item.name} while on station");
        }
        else
        {
            // For non-pickupable items, just enable the collider normally
            Collider col = item.GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = true;
            }
            currentPickupableItem = null;
        }

        // Update station state
        currentItem = item;
        isOccupied = true;

        // Call derived class logic
        OnItemPlacedInternal(item, playerEnd);

        // Fire events
        OnItemPlaced?.Invoke(item, playerEnd);

        DebugLog($"Successfully placed {item.name} on station by Player {playerEnd.PlayerNumber}. Station state - isOccupied: {isOccupied}, HasSpace: {HasSpace}");
        return true;
    }

    /// <summary>
    /// Remove the current item from this station
    /// </summary>
    public virtual GameObject RemoveItem(PlayerEnd playerEnd)
    {
        if (!isOccupied || currentItem == null)
        {
            DebugLog("No item to remove from station");
            return null;
        }

        if (!CanPlayerInteract(playerEnd))
        {
            DebugLog($"Player {playerEnd.PlayerNumber} cannot remove items from this station");
            return null;
        }

        GameObject itemToRemove = currentItem;

        // Re-enable direct interaction for PickupableItems
        if (currentPickupableItem != null)
        {
            currentPickupableItem.SetDirectInteractionEnabled(true);
            DebugLog($"Re-enabled direct interaction for {itemToRemove.name}");
            currentPickupableItem = null;
        }

        // Call derived class logic before removal
        OnItemRemovedInternal(itemToRemove, playerEnd);

        // Reset station state FIRST
        currentItem = null;
        isOccupied = false;

        // Fire events
        OnItemRemoved?.Invoke(itemToRemove, playerEnd);

        DebugLog($"Removed {itemToRemove.name} from station by Player {playerEnd.PlayerNumber}. Station is now available: {HasSpace}");
        return itemToRemove;
    }

    /// <summary>
    /// Clear the current item from the station without player interaction (for system cleanup)
    /// Used when customers finish eating, items are destroyed externally, etc.
    /// </summary>
    public virtual void ClearItem()
    {
        if (!isOccupied || currentItem == null)
        {
            DebugLog("No item to clear from station");
            return;
        }

        GameObject itemBeingCleared = currentItem;

        // Re-enable direct interaction for PickupableItems
        if (currentPickupableItem != null)
        {
            currentPickupableItem.SetDirectInteractionEnabled(true);
            DebugLog($"Re-enabled direct interaction for {itemBeingCleared.name}");
            currentPickupableItem = null;
        }

        // Call derived class logic before removal (pass null for playerEnd)
        OnItemRemovedInternal(itemBeingCleared, null);

        // Reset station state
        currentItem = null;
        isOccupied = false;

        // Fire events (pass null for playerEnd)
        OnItemRemoved?.Invoke(itemBeingCleared, null);

        DebugLog($"Cleared {itemBeingCleared.name} from station. Station is now available: {HasSpace}");
    }

    /// <summary>
    /// Get the position where items should be placed
    /// </summary>
    public virtual Vector3 GetPlacementPosition()
    {
        return itemPlacementPoint.position + placementOffset;
    }

    #endregion

    #region Virtual Methods for Derived Classes

    /// <summary>
    /// Called when an item is successfully placed on this station
    /// Override in derived classes to add specific behavior
    /// </summary>
    protected virtual void OnItemPlacedInternal(GameObject item, PlayerEnd playerEnd)
    {
        FoodItem foodItem = item.GetComponent<FoodItem>();
        if (foodItem != null)
        {
            foodItem.OnFoodItemPlaced();
        }
    }

    /// <summary>
    /// Called when an item is removed from this station
    /// Override in derived classes to add specific behavior
    /// </summary>
    protected virtual void OnItemRemovedInternal(GameObject item, PlayerEnd playerEnd)
    {
        // Default: do nothing
    }

    #endregion

    #region Debug and Gizmos

    protected virtual void OnDrawGizmos()
    {
        if (!showStationGizmos) return;

        // Draw station bounds
        Collider stationCollider = GetComponent<Collider>();
        if (stationCollider != null)
        {
            Gizmos.color = GetStationColor();

            if (stationCollider is BoxCollider boxCollider)
            {
                Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
                Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);
                Gizmos.matrix = Matrix4x4.identity;
            }
            else
            {
                Gizmos.DrawWireCube(transform.position, Vector3.one);
            }
        }

        // Draw placement point
        if (itemPlacementPoint != null)
        {
            Gizmos.color = isOccupied ? Color.red : Color.green;
            Gizmos.DrawWireCube(GetPlacementPosition(), Vector3.one * 0.3f);
        }

        // Draw station type indicator
        Gizmos.color = GetStationColor();
        Vector3 indicatorPos = transform.position + Vector3.up * 2f;
        Gizmos.DrawCube(indicatorPos, Vector3.one * 0.2f);

#if UNITY_EDITOR
        // Show station name in scene view
        UnityEditor.Handles.Label(indicatorPos + Vector3.up * 0.5f, stationName);
#endif
    }

    protected virtual Color GetStationColor()
    {
        switch (stationType)
        {
            case StationType.Plain: return Color.white;
            case StationType.Cooking: return Color.red;
            case StationType.Chopping: return Color.blue;
            case StationType.Serving: return Color.yellow;
            default: return Color.gray;
        }
    }

    protected virtual void OnValidate()
    {
        // Ensure reasonable values
        maxItemCapacity = Mathf.Max(1, maxItemCapacity);

        // Ensure station name is not empty
        if (string.IsNullOrEmpty(stationName))
        {
            stationName = $"{stationType}Station";
        }
    }

    protected void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[{stationName}] {message}");
    }

    #endregion
}

/// <summary>
/// Defines the different types of stations
/// </summary>
public enum StationType
{
    Plain,     // Simple storage surface
    Cooking,   // Frying/boiling stations
    Chopping,  // Chopping station
    Serving    // Order delivery station
}