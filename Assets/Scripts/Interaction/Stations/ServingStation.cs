using UnityEngine;
using System;

/// <summary>
/// Station where customers sit and get served food.
/// Players bring the requested food to this station to serve the customer.
/// </summary>
public class ServingStation : BaseStation
{
    [Header("Serving Station")]
    [SerializeField] private Transform customerPosition;
    [Tooltip("Where the customer will stand/sit at this table")]

    // Events - Using C# events instead of UnityEvents for better reliability
    public event Action OnCustomerServedSuccessfully;

    // Internal state
    private Customer assignedCustomer;
    private FoodItemData requestedFood;

    // Public properties
    public bool HasCustomer => assignedCustomer != null;
    public Customer AssignedCustomer => assignedCustomer;
    public FoodItemData RequestedFood => requestedFood;
    public Transform CustomerPosition => customerPosition;

    protected override void Initialize()
    {
        base.Initialize();
        stationType = StationType.Serving;

        // Set up customer position if not assigned
        // if (customerPosition == null)
        // {
        //     GameObject customerPosObj = new GameObject($"{stationName}_CustomerPosition");
        //     customerPosObj.transform.SetParent(transform);
        //     customerPosObj.transform.localPosition = Vector3.zero;
        //     customerPosition = customerPosObj.transform;
        //     DebugLog("Created default customer position");
        // }
    }

    /// <summary>
    /// Assign a customer to this serving station
    /// </summary>
    public void AssignCustomer(Customer customer, FoodItemData foodRequest)
    {
        if (HasCustomer)
        {
            Debug.LogWarning($"[{stationName}] Tried to assign customer but station already has one!");
            return;
        }

        assignedCustomer = customer;
        requestedFood = foodRequest;
        DebugLog($"Customer assigned, requesting: {foodRequest.DisplayName}");
    }

    /// <summary>
    /// Release the customer from this serving station
    /// </summary>
    public void ReleaseCustomer()
    {
        if (!HasCustomer)
        {
            DebugLog("Tried to release customer but no customer is assigned");
            return;
        }

        DebugLog($"Customer released");
        assignedCustomer = null;
        requestedFood = null;
    }

    /// <summary>
    /// Try to serve the customer with the given food item
    /// </summary>
    public bool TryServeCustomer(FoodItem foodItem, PlayerEnd playerEnd)
    {
        if (!HasCustomer)
        {
            DebugLog("Cannot serve - no customer at this station");
            return false;
        }

        if (foodItem == null || !foodItem.HasValidFoodData)
        {
            DebugLog("Cannot serve - invalid food item");
            return false;
        }

        // Check if the food matches the customer's order
        if (foodItem.FoodData != requestedFood)
        {
            DebugLog($"Wrong food! Customer wants {requestedFood.DisplayName} but got {foodItem.FoodData.DisplayName}");
            return false;
        }

        // Correct food! Serve the customer
        DebugLog($"Successfully served customer with {foodItem.FoodData.DisplayName}");

        // Place the food on the station
        PlaceItem(foodItem.gameObject, playerEnd);

        // Fire success event
        OnCustomerServedSuccessfully?.Invoke();
        foodItem.OnServed();

        // Notify the customer that they've been served
        if (assignedCustomer != null)
        {
            assignedCustomer.OnServed(foodItem.gameObject);
        }

        return true;
    }

    /// <summary>
    /// Override to only accept the correct food for the customer's order
    /// </summary>
    protected override bool CanAcceptItemCustom(GameObject item, PlayerEnd playerEnd)
    {
        // Only accept food items when a customer is present
        if (!HasCustomer)
        {
            DebugLog("Cannot accept item - no customer at this station");
            return false;
        }

        // Must be a food item
        FoodItem foodItem = item.GetComponent<FoodItem>();
        if (foodItem == null || !foodItem.HasValidFoodData)
        {
            DebugLog("Cannot accept item - not a valid food item");
            return false;
        }

        // FIXED: Only accept the food that matches the customer's order
        // This prevents wrong food from being placed on the station
        if (foodItem.FoodData != requestedFood)
        {
            DebugLog($"Cannot accept item - wrong food (customer wants {requestedFood.DisplayName}, got {foodItem.FoodData.DisplayName})");
            return false;
        }

        return true;
    }

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        // Draw customer position
        if (customerPosition != null)
        {
            Gizmos.color = HasCustomer ? Color.red : Color.green;
            Gizmos.DrawWireSphere(customerPosition.position, 0.5f);
        }

#if UNITY_EDITOR
        // Show requested food in scene view
        if (HasCustomer && requestedFood != null)
        {
            Vector3 labelPos = transform.position + Vector3.up * 2.5f;
            UnityEditor.Handles.Label(labelPos, $"Order: {requestedFood.DisplayName}");
        }
#endif
    }
}
