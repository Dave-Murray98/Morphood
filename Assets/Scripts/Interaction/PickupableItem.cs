using UnityEngine;

/// <summary>
/// A basic pickupable item like ingredients or dishes.
/// Players can pick these up and drop them. This is a universal interaction.
/// </summary>
public class PickupableItem : BaseInteractable
{
    [Header("Pickupable Item Settings")]
    public string itemName = "Item";
    [Tooltip("Display name for this item")]

    public ItemType itemType = ItemType.Ingredient;

    public string ItemName => itemName;
    public ItemType Type => itemType;
    public bool IsPickedUp => isPickedUp;


    [Tooltip("What type of item this is")]

    [SerializeField] private bool canBeDroppedAnywhere = true;
    [Tooltip("If false, can only be placed on specific surfaces")]

    // Internal state
    private Vector3 originalPosition;
    private bool isPickedUp = false;

    protected override void Start()
    {
        base.Start();

        // Set up as universal interaction
        interactionType = InteractionType.Universal;
        interactionPrompt = $"Pick up {itemName}";

        // Store original position for hover effect
        originalPosition = transform.position;

        DebugLog($"Pickupable item '{itemName}' initialized");
    }

    #region Interaction Implementation

    protected override bool CanInteractCustom(PlayerEnd playerEnd)
    {
        // Can't pick up if already picked up
        if (isPickedUp)
        {
            DebugLog("Item is already picked up");
            return false;
        }

        // Can't pick up if player's hands are full
        if (!playerEnd.HasFreeHands)
        {
            DebugLog($"Player {playerEnd.PlayerNumber} hands are full");
            return false;
        }

        return true;
    }

    protected override bool PerformInteraction(PlayerEnd playerEnd)
    {
        // Attempt to pick up the item
        bool pickupSuccessful = playerEnd.PickUpObject(gameObject);

        if (pickupSuccessful)
        {
            isPickedUp = true;
            SetAvailable(false); // No longer available while being carried

            DebugLog($"Item '{itemName}' picked up by Player {playerEnd.PlayerNumber}");

            // Subscribe to the player's drop event to know when we're dropped
            playerEnd.OnItemDropped += OnItemDropped;

            return true;
        }
        else
        {
            DebugLog($"Failed to pick up item '{itemName}'");
            return false;
        }
    }

    protected override void OnInteractionStopped(PlayerEnd playerEnd)
    {
        // For pickupable items, interaction stopping doesn't usually do anything
        // The item is either picked up (successful) or not (failed)
        // Dropping is handled through the PlayerEnd's drop methods
    }

    protected override string GetUnavailablePrompt(PlayerEnd playerEnd)
    {
        if (isPickedUp)
            return "Already picked up";

        if (!playerEnd.HasFreeHands)
            return "Hands full";

        return base.GetUnavailablePrompt(playerEnd);
    }

    #endregion

    #region Event Handlers

    private void OnItemDropped(GameObject droppedItem)
    {
        // Check if this is our item being dropped
        if (droppedItem == gameObject)
        {
            isPickedUp = false;
            SetAvailable(true); // Available for pickup again

            // Update our position reference for hover effect
            originalPosition = transform.position;

            // Unsubscribe from the event
            if (currentInteractingPlayer != null)
            {
                currentInteractingPlayer.OnItemDropped -= OnItemDropped;
            }

            DebugLog($"Item '{itemName}' was dropped and is now available again");
        }
    }

    #endregion

    #region Visual Effects

    private void Update()
    {
        if (isPickedUp) return; // Don't animate while being carried
    }

    /// <summary>
    /// Called by UI or other systems to highlight this item
    /// </summary>
    public void SetHighlighted(bool highlighted)
    {
        isHighlighted = highlighted;

        if (!highlighted && !isPickedUp)
        {
            // Return to original position when not highlighted
            transform.position = originalPosition;
        }
    }

    #endregion

    #region Public Properties and Methods



    /// <summary>
    /// Force drop this item at a specific position (useful for placing on surfaces)
    /// </summary>
    public void ForceDropAt(Vector3 position)
    {
        if (!isPickedUp) return;

        if (currentInteractingPlayer != null)
        {
            currentInteractingPlayer.DropObject(gameObject, position);
        }
    }

    /// <summary>
    /// Check if this item can be placed on a specific surface or container
    /// </summary>
    public bool CanBePlacedOn(GameObject target)
    {
        if (canBeDroppedAnywhere) return true;

        // Add specific placement logic here in the future
        // For example, only certain items can go on certain surfaces
        return true;
    }

    /// <summary>
    /// Set whether this item can be directly interacted with
    /// Used by stations to prevent direct pickup when item is placed on them
    /// </summary>
    public void SetDirectInteractionEnabled(bool enabled)
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = enabled;
            DebugLog($"Direct interaction {(enabled ? "enabled" : "disabled")} for {itemName}");
        }
    }

    /// <summary>
    /// Get whether this item can currently be directly interacted with
    /// </summary>
    public bool IsDirectInteractionEnabled()
    {
        Collider col = GetComponent<Collider>();
        return col != null && col.enabled;
    }

    #endregion

    #region Debug and Gizmos

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        // Draw item type indicator
        Gizmos.color = GetItemTypeColor();
        Gizmos.DrawCube(transform.position + Vector3.up * 2.5f, Vector3.one * 0.2f);

        // Draw name in scene view (only if selected)
        if (isHighlighted || (currentInteractingPlayer != null))
        {
            // This would show in scene view
#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 3f, itemName);
#endif
        }
    }

    private Color GetItemTypeColor()
    {
        switch (itemType)
        {
            case ItemType.Ingredient: return Color.green;
            case ItemType.Dish: return Color.white;
            case ItemType.CookedFood: return Color.red;
            default: return Color.gray;
        }
    }

    #endregion
}

/// <summary>
/// Defines the different types of pickupable items
/// </summary>
public enum ItemType
{
    Ingredient,   // Raw ingredients that can be chopped or cooked
    Dish,         // Empty dishes that can hold ingredients
    CookedFood,   // Completed dishes ready for delivery
    Order,        // Order papers
    Recipe        // Recipe papers
}