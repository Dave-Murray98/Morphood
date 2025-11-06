using UnityEngine;
using Pathfinding;
using Sirenix.OdinInspector;
using Unity.VisualScripting;

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
    [SerializeField] private CustomerFeedbackManager feedbackManager;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true; // Enable by default for debugging

    // Internal state
    private ServingStation assignedStation;
    [ShowInInspector] private FoodItemData orderRequest;
    [ShowInInspector] private CustomerState currentState = CustomerState.Idle;
    private GameObject servedFood;

    [Header("SpeechBubble")]
    [SerializeField] private CustomerUI customerUI;

    // State tracking
    private bool hasReachedDestination = false;
    private float stateTransitionTime; // Track when we enter states to add delays

    // Public properties
    public ServingStation AssignedStation => assignedStation;
    public FoodItemData OrderRequest => orderRequest;
    public CustomerState CurrentState => currentState;

    //leaving backup timer in case customers get stuck when leaving the restaurant
    private float leavingTimer = 0f;
    private float leavingTimerMax = 10f;

    private void Awake()
    {
        InitializeComponents();
    }

    private void InitializeComponents()
    {
        // Get FollowerEntity
        if (followerEntity == null)
        {
            followerEntity = GetComponent<FollowerEntity>();
        }

        if (followerEntity == null)
        {
            Debug.LogError($"[Customer] {name} requires a FollowerEntity component!");
        }

        // Get CustomerUI
        if (customerUI == null)
            customerUI = GetComponentInChildren<CustomerUI>();

        if (customerUI != null)
        {
            customerUI.HideSpeechBubbleImmediate();
            customerUI.HideMoneyUI();
        }

        // Get AnimationHandler
        if (animationHandler == null)
            animationHandler = GetComponent<CustomerAnimationHandler>();

        if (animationHandler == null)
        {
            Debug.LogError($"[Customer] {name} requires a CustomerAnimationHandler component!");
        }

        // Get AppearanceManager
        if (appearanceManager == null)
            appearanceManager = GetComponent<CustomerAppearanceManager>();

        DebugLog("Components initialized");
    }

    private void OnEnable()
    {
        if (followerEntity == null)
        {
            followerEntity = GetComponent<FollowerEntity>();
        }

        if (followerEntity != null)
        {
            followerEntity.enabled = true;
        }

        if (customerUI != null)
        {
            if (customerUI.speechBubble.activeInHierarchy)
                customerUI.HideSpeechBubbleImmediate();

            customerUI.HideMoneyUI();
        }

        // Randomize appearance when customer becomes active
        if (appearanceManager != null)
        {
            appearanceManager.RandomizeAppearance();
            DebugLog($"Randomized customer appearance to index {appearanceManager.GetCurrentAppearanceIndex()}");
        }

        DebugLog("Customer enabled");
    }

    private void OnDisable()
    {
        if (followerEntity != null)
        {
            followerEntity.enabled = false;
        }

        DebugLog("Customer disabled");
    }

    private void Update()
    {
        HandleMovementStates();
        HandleAnimationStateTransitions();
    }

    private void HandleMovementStates()
    {
        // Check if we've reached our destination for movement states
        if (currentState == CustomerState.MovingToTable || currentState == CustomerState.Leaving)
        {
            if (!hasReachedDestination && followerEntity != null && followerEntity.reachedDestination)
            {
                hasReachedDestination = true;
                OnDestinationReached();
            }

            if (currentState == CustomerState.Leaving)
            {
                leavingTimer += Time.deltaTime;
                if (leavingTimer > leavingTimerMax)
                {
                    OnDestinationReached();
                    leavingTimer = 0f;
                }
            }
        }
    }

    private void HandleAnimationStateTransitions()
    {
        // Handle automatic transitions for animation states
        if (animationHandler == null) return;

        // Check if greeting animation finished (only transition if not already served)
        if (currentState == CustomerState.OrderingFood &&
            !animationHandler.isGreetingAnimationPlaying)
        {
            // Add a small delay to ensure animation event has been processed
            if (Time.time > stateTransitionTime + 0.1f)
            {
                DebugLog("Greeting animation finished in Update, transitioning to WaitingForFood");
                TransitionToWaitingForFood();
            }
        }

        // Check if celebration animation finished
        if (currentState == CustomerState.Celebrating &&
            !animationHandler.isCelebrationAnimationPlaying)
        {
            // Add a small delay to ensure animation event has been processed
            if (Time.time > stateTransitionTime + 0.1f)
            {
                DebugLog("Celebration animation finished in Update, transitioning to leaving");
                StartLeavingProcess();
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
        ChangeState(CustomerState.MovingToTable);
        hasReachedDestination = false;

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
            // Arrived at table, start ordering (greeting animation)
            DebugLog($"Arrived at table, starting greeting animation and showing order");
            ChangeState(CustomerState.OrderingFood);

            // Show speech bubble immediately when they start ordering
            if (customerUI != null && orderRequest != null)
            {
                customerUI.ShowSpeechBubble(orderRequest.Icon);
                DebugLog("Speech bubble shown immediately upon arrival");
            }
            else
            {
                DebugLog($"Failed to show speech bubble on arrival - customerUI: {customerUI != null}, orderRequest: {orderRequest != null}");
            }
        }
        else if (currentState == CustomerState.Leaving)
        {
            // Reached the door, ready to despawn
            DebugLog("Reached door, ready to despawn");
            ChangeState(CustomerState.ReadyToDespawn);
        }
    }

    /// <summary>
    /// Called by CustomerAnimationHandler when greeting animation finishes (via animation event)
    /// This is a backup - the main transition happens in Update() by checking animation flags
    /// </summary>
    public void OnGreetingComplete()
    {
        DebugLog("OnGreetingComplete called from animation event");

        if (currentState == CustomerState.OrderingFood)
        {
            TransitionToWaitingForFood();
        }
    }

    /// <summary>
    /// Transition from OrderingFood to WaitingForFood
    /// </summary>
    private void TransitionToWaitingForFood()
    {
        if (currentState != CustomerState.OrderingFood) return;

        DebugLog($"Greeting animation finished, transitioning to WaitingForFood (speech bubble already shown)");

        // Transition to waiting for food (speech bubble already shown when they arrived)
        ChangeState(CustomerState.WaitingForFood);

        // No need to show speech bubble again - it's already visible from when they arrived
        DebugLog("Transitioned to WaitingForFood - keeping existing speech bubble");
    }

    /// <summary>
    /// Called by CustomerAnimationHandler when celebration animation finishes (via animation event)
    /// This is a backup - the main transition happens in Update() by checking animation flags
    /// </summary>
    public void OnCelebrationComplete()
    {
        DebugLog("OnCelebrationComplete called from animation event");

        if (currentState == CustomerState.Celebrating)
        {
            StartLeavingProcess();
        }
    }

    /// <summary>
    /// Start the leaving process after celebration
    /// </summary>
    private void StartLeavingProcess()
    {
        if (currentState != CustomerState.Celebrating) return;

        DebugLog("Starting leaving process");

        // Get door position from CustomerManager
        Transform doorPosition = null;
        if (CustomerManager.Instance != null)
        {
            doorPosition = CustomerManager.Instance.DoorTransform;
        }

        Leave(doorPosition);
    }

    /// <summary>
    /// Called when the customer is served their food
    /// </summary>
    public void OnServed(GameObject food)
    {
        // Allow serving during OrderingFood (greeting animation) or WaitingForFood states
        if (currentState != CustomerState.WaitingForFood && currentState != CustomerState.OrderingFood)
        {
            DebugLog($"Customer was served but is not ready (current state: {currentState})");
            return;
        }

        // If served during greeting animation, interrupt it and go straight to eating
        if (currentState == CustomerState.OrderingFood)
        {
            DebugLog("Customer served during greeting animation - interrupting greeting to start eating");
        }

        servedFood = food;
        ChangeState(CustomerState.Eating);

        if (customerUI != null)
        {
            customerUI.HideSpeechBubble();
            //customerUI.StartCoroutine(customerUI.ShowMoneyUICoroutine(orderRequest.foodValue));
        }

        if (feedbackManager != null)
        {
            feedbackManager.PlayMoneyFeedback();
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
            DebugLog($"FinishEating called but customer is not eating (current state: {currentState})");
            return;
        }

        // Clean up the food and notify the station
        CleanupFood();

        // Transition to celebrating state
        DebugLog("Finished eating, starting celebration animation");
        ChangeState(CustomerState.Celebrating);

        feedbackManager.PlayCelebrationFeedback();
    }

    /// <summary>
    /// Clean up the served food item
    /// </summary>
    private void CleanupFood()
    {
        if (servedFood != null)
        {
            // Clear item from station first to reset its state (currentItem, isOccupied)
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
    }

    /// <summary>
    /// Tell the customer to leave the restaurant
    /// </summary>
    public void Leave(Transform doorPosition)
    {
        DebugLog("Leave method called");

        ChangeState(CustomerState.Leaving);
        hasReachedDestination = false;

        // Release from serving station
        if (assignedStation != null)
        {
            assignedStation.ReleaseCustomer();
        }

        // Move to door
        if (followerEntity != null)
        {
            if (doorPosition != null)
            {
                followerEntity.destination = doorPosition.position;
                DebugLog("Moving to door position");
            }
            else
            {
                DebugLog("Warning: No door position provided, customer may not move properly");
            }
        }

        // Hide UI elements
        if (customerUI != null)
        {
            if (customerUI.speechBubble.activeInHierarchy)
                customerUI.HideSpeechBubble();

            customerUI.HideMoneyUI();
        }
    }

    /// <summary>
    /// Change the customer's state and update animations
    /// </summary>
    private void ChangeState(CustomerState newState)
    {
        if (currentState == newState) return;

        CustomerState oldState = currentState;
        currentState = newState;
        stateTransitionTime = Time.time;

        DebugLog($"State changed: {oldState} -> {newState}");

        // Update animation handler
        if (animationHandler != null)
        {
            animationHandler.UpdateAnimationState(currentState);
        }
        else
        {
            DebugLog("Warning: AnimationHandler is null, cannot update animation state");
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
        stateTransitionTime = 0f;

        // Reset appearance tracking
        if (appearanceManager != null)
        {
            appearanceManager.ResetForPooling();
        }

        // Reset animation handler
        if (animationHandler != null)
        {
            animationHandler.ResetForPooling();
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
            Debug.Log($"[Customer {name}] {message}");
    }

    #region Debug Inspector Methods

    [Button("Force Transition to WaitingForFood")]
    private void DebugForceWaitingForFood()
    {
        if (Application.isPlaying)
        {
            TransitionToWaitingForFood();
        }
    }

    [Button("Force Show Speech Bubble")]
    private void DebugShowSpeechBubble()
    {
        if (Application.isPlaying && customerUI != null && orderRequest != null)
        {
            customerUI.ShowSpeechBubble(orderRequest.Icon);
            DebugLog("Debug: Forced speech bubble to show");
        }
    }

    #endregion
}

/// <summary>
/// Represents the current state of a customer
/// </summary>
public enum CustomerState
{
    Idle,
    MovingToTable,
    OrderingFood,    // New: plays greeting animation
    WaitingForFood,
    Eating,
    Celebrating,     // New: plays celebration animation
    Leaving,
    ReadyToDespawn
}