using UnityEngine;
using Pathfinding;
using Sirenix.OdinInspector;

/// <summary>
/// Represents a customer in the restaurant.
/// Moves to a serving station, places an order, waits to be served, eats, and leaves.
/// </summary>
public class Customer : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private FollowerEntity followerEntity;
    [SerializeField] private CustomerAnimationHandler animationHandler;
    [SerializeField] private CustomerAppearanceManager appearanceManager;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Internal state
    private ServingStation assignedStation;
    [ShowInInspector] private FoodItemData orderRequest;
    [ShowInInspector] private CustomerState currentState = CustomerState.Idle;
    private GameObject servedFood;

    [Header("SpeechBubble")]
    [SerializeField] private CustomerUI customerUI;

    // State tracking
    private bool hasReachedDestination = false;

    // Public properties
    public ServingStation AssignedStation => assignedStation;
    public FoodItemData OrderRequest => orderRequest;
    public CustomerState CurrentState => currentState;

    private void Awake()
    {
        if (followerEntity == null)
        {
            followerEntity = GetComponent<FollowerEntity>();
        }

        if (followerEntity == null)
        {
            Debug.LogError($"[Customer] {name} requires a FollowerEntity component!");
        }

        if (customerUI == null)
            customerUI = GetComponentInChildren<CustomerUI>();

        if (customerUI != null)
        {
            customerUI.HideSpeechBubble();
            customerUI.HideMoneyUI();
        }

        if (animationHandler == null)
            animationHandler = GetComponent<CustomerAnimationHandler>();

        // Auto-find the appearance manager if not assigned
        if (appearanceManager == null)
            appearanceManager = GetComponent<CustomerAppearanceManager>();
    }

    private void OnEnable()
    {
        if (followerEntity == null)
        {
            followerEntity = GetComponent<FollowerEntity>();
        }

        followerEntity.enabled = true;

        if (customerUI != null)
        {
            customerUI.HideSpeechBubble();
            customerUI.HideMoneyUI();
        }

        // Randomize appearance when customer becomes active
        if (appearanceManager != null)
        {
            appearanceManager.RandomizeAppearance();
            DebugLog($"Randomized customer appearance to index {appearanceManager.GetCurrentAppearanceIndex()}");
        }
    }

    private void OnDisable()
    {
        if (followerEntity == null)
            followerEntity = GetComponent<FollowerEntity>();

        followerEntity.enabled = false;

    }

    private void Update()
    {
        // Check if we've reached our destination
        if (currentState == CustomerState.MovingToTable || currentState == CustomerState.Leaving)
        {
            if (!hasReachedDestination && followerEntity.reachedDestination)
            {
                hasReachedDestination = true;
                OnDestinationReached();
            }
        }
    }

    /// <summary>
    /// Assign this customer to a serving station and give them an order
    /// </summary>
    public void AssignToStation(ServingStation station, FoodItemData order, Transform doorPosition)
    {
        assignedStation = station;
        orderRequest = order;
        currentState = CustomerState.MovingToTable;
        hasReachedDestination = false;

        animationHandler.UpdateAnimationState(currentState);

        // Move to the station
        if (followerEntity != null && station != null)
        {
            followerEntity.destination = station.CustomerPosition.position;
            DebugLog($"Assigned to station {station.StationName}, ordering {order.DisplayName}");
        }
    }

    /// <summary>
    /// Called when the customer reaches their destination
    /// </summary>
    private void OnDestinationReached()
    {
        if (currentState == CustomerState.MovingToTable)
        {
            // Arrived at table, now waiting for service
            currentState = CustomerState.WaitingForFood;

            if (customerUI != null)
            {
                customerUI.ShowSpeechBubble(orderRequest.Icon);
            }

            animationHandler.UpdateAnimationState(currentState);

            DebugLog($"Arrived at table, waiting for {orderRequest.DisplayName}");
        }
        else if (currentState == CustomerState.Leaving)
        {
            // Reached the door, ready to despawn
            currentState = CustomerState.ReadyToDespawn;
            DebugLog("Reached door, ready to despawn");
        }
    }

    /// <summary>
    /// Called when the customer is served their food
    /// </summary>
    public void OnServed(GameObject food)
    {
        if (currentState != CustomerState.WaitingForFood)
        {
            DebugLog("Customer was served but is not waiting for food");
            return;
        }

        servedFood = food;
        currentState = CustomerState.Eating;

        animationHandler.UpdateAnimationState(currentState);

        if (customerUI != null)
        {
            customerUI.HideSpeechBubble();
            customerUI.StartCoroutine(customerUI.ShowMoneyUICoroutine(orderRequest.foodValue));
        }

        DebugLog($"Started eating {orderRequest.DisplayName}");

        // The CustomerManager will handle the eating duration and calling FinishEating
    }

    /// <summary>
    /// Called when the customer finishes eating
    /// </summary>
    public void FinishEating()
    {
        if (currentState != CustomerState.Eating)
        {
            DebugLog("FinishEating called but customer is not eating");
            return;
        }

        // Clean up the food and notify the station
        if (servedFood != null)
        {
            // FIXED: Clear item from station first to reset its state (currentItem, isOccupied)
            if (assignedStation != null && assignedStation.CurrentItem == servedFood)
            {
                assignedStation.ClearItem(); // Use ClearItem for system cleanup without player
                DebugLog("Cleared food from station");
            }

            // Then destroy the food item
            FoodItem foodItem = servedFood.GetComponent<FoodItem>();
            if (foodItem != null && FoodManager.Instance != null)
            {
                FoodManager.Instance.DestroyFoodItem(foodItem);
            }

            servedFood = null;
        }

        DebugLog("Finished eating, leaving restaurant");
    }

    /// <summary>
    /// Tell the customer to leave the restaurant
    /// </summary>
    public void Leave(Transform doorPosition)
    {
        currentState = CustomerState.Leaving;
        hasReachedDestination = false;

        // Release from serving station
        if (assignedStation != null)
        {
            assignedStation.ReleaseCustomer();
        }

        // Move to door
        if (followerEntity != null && doorPosition != null)
        {
            followerEntity.destination = doorPosition.position;
            DebugLog("Leaving restaurant");
        }

        animationHandler.UpdateAnimationState(currentState);

        // Hide speech bubble (as customers will leave when the round ends, if they haven't been served yet, we'll need to hide the bubble)
        if (customerUI != null)
        {
            customerUI.HideSpeechBubble();
            customerUI.HideMoneyUI();
        }
    }

    /// <summary>
    /// Reset this customer for reuse (pooling)
    /// </summary>
    public void ResetForPooling()
    {
        assignedStation = null;
        orderRequest = null;
        servedFood = null;
        currentState = CustomerState.Idle;
        hasReachedDestination = false;

        // Reset appearance tracking
        if (appearanceManager != null)
        {
            appearanceManager.ResetForPooling();
        }

        DebugLog("Reset for pooling");
    }

    /// <summary>
    /// Manually change the customer's appearance (useful for testing or special cases)
    /// </summary>
    public void ChangeAppearance(int appearanceIndex)
    {
        if (appearanceManager != null)
        {
            appearanceManager.ApplyAppearance(appearanceIndex);
            DebugLog($"Manually changed appearance to index {appearanceIndex}");
        }
        else
        {
            DebugLog("Cannot change appearance - no CustomerAppearanceManager found");
        }
    }

    /// <summary>
    /// Get the current appearance index for debugging or save purposes
    /// </summary>
    public int GetCurrentAppearanceIndex()
    {
        if (appearanceManager != null)
        {
            return appearanceManager.GetCurrentAppearanceIndex();
        }
        return -1;
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[Customer] {message}");
    }
}

/// <summary>
/// Represents the current state of a customer
/// </summary>
public enum CustomerState
{
    Idle,
    MovingToTable,
    WaitingForFood,
    Eating,
    Leaving,
    ReadyToDespawn
}