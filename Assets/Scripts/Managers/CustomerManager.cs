using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages customer spawning, assignment to serving stations, and customer lifecycle.
/// Handles object pooling for customers.
/// </summary>
public class CustomerManager : MonoBehaviour
{

    public static CustomerManager Instance { get; private set; }

    [Header("Restaurant Configuration")]
    [SerializeField] private List<ServingStation> servingStations = new List<ServingStation>();
    [Tooltip("All serving stations in the restaurant")]

    [SerializeField] private Transform customerSpawnPosition;
    [Tooltip("Position where customers spawn and leave")]

    [Header("Menu Configuration")]
    [SerializeField] private List<FoodItemData> menu = new List<FoodItemData>();
    [Tooltip("Available food items that customers can order")]

    [Header("Customer Prefab")]
    [SerializeField] private GameObject customerPrefab;
    [Tooltip("Prefab used to spawn customers (must have Customer component)")]

    [Header("Timing Configuration")]
    [SerializeField] private float spawnInterval = 3f;
    [Tooltip("Time between customer spawns when multiple tables are free")]

    [SerializeField] private float customerEatTime = 5f;
    [Tooltip("How long customers take to eat their food")]

    [Header("Pooling Configuration")]
    [SerializeField] private int initialPoolSize = 5;
    [Tooltip("Number of customers to pre-create in the pool")]

    [SerializeField] private int maxPoolSize = 20;
    [Tooltip("Maximum number of customers in the pool")]

    [SerializeField] private Transform poolParent;
    [Tooltip("Parent transform for pooled customers")]

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Events - Using C# events instead of UnityEvents for better reliability
    public event Action<float> OnCustomerServedSuccessfully;

    /// <summary>
    /// Public getter for the door transform position
    /// Used by customers when they need to leave after celebration animations
    /// </summary>
    public Transform DoorTransform => customerSpawnPosition;

    // Pooling
    private Queue<Customer> availableCustomers = new Queue<Customer>();
    private HashSet<Customer> allCustomers = new HashSet<Customer>();
    private List<Customer> activeCustomers = new List<Customer>();

    // Event handler tracking - needed to properly unsubscribe C# events
    private Dictionary<Customer, Action> customerEventHandlers = new Dictionary<Customer, Action>();

    // Spawn tracking
    private float lastSpawnTime = -999f;
    private bool isSpawningCustomers = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        FindAllServingStations();
        ValidateConfiguration();
        SetupPoolParent();
        PreCreateCustomers();
        // Note: Don't auto-start spawning - let RoundManager control this
        // StartCustomerSpawning();

        OnCustomerServedSuccessfully += RoundManager.Instance.OnCustomerServed;

        DebugLog("CustomerManager initialized");
    }

    private void FindAllServingStations()
    {
        foreach (ServingStation station in FindObjectsByType<ServingStation>(FindObjectsSortMode.None))
        {
            servingStations.Add(station);
        }
    }

    private void ValidateConfiguration()
    {
        if (servingStations.Count == 0)
        {
            Debug.LogWarning("[CustomerManager] No serving stations assigned!");
        }

        if (customerSpawnPosition == null)
        {
            Debug.LogWarning("[CustomerManager] No door transform assigned!");
        }

        if (menu.Count == 0)
        {
            Debug.LogWarning("[CustomerManager] Menu is empty! Customers won't be able to order anything.");
        }

        if (customerPrefab == null)
        {
            Debug.LogError("[CustomerManager] No customer prefab assigned!");
        }
        else
        {
            Customer customer = customerPrefab.GetComponent<Customer>();
            if (customer == null)
            {
                Debug.LogError("[CustomerManager] Customer prefab must have a Customer component!");
            }
        }
    }

    private void SetupPoolParent()
    {
        if (poolParent == null)
        {
            GameObject poolParentObj = new GameObject("Customer Pool");
            poolParentObj.transform.SetParent(transform);
            poolParent = poolParentObj.transform;
        }
    }

    private void PreCreateCustomers()
    {
        for (int i = 0; i < initialPoolSize; i++)
        {
            Customer customer = CreateNewCustomer();
            if (customer != null)
            {
                ReturnToPool(customer);
            }
        }

        DebugLog($"Pre-created {initialPoolSize} customers in pool");
    }

    private void Update()
    {
        // Check for customers that have finished eating
        for (int i = activeCustomers.Count - 1; i >= 0; i--)
        {
            Customer customer = activeCustomers[i];
            if (customer.CurrentState == CustomerState.ReadyToDespawn)
            {
                DespawnCustomer(customer);
            }
        }
    }

    #region Customer Spawning

    /// <summary>
    /// Start the customer spawning coroutine
    /// </summary>
    public void StartCustomerSpawning()
    {
        if (!isSpawningCustomers)
        {
            isSpawningCustomers = true;
            StartCoroutine(CustomerSpawnCoroutine());
            DebugLog("Started customer spawning");
        }
    }

    /// <summary>
    /// Stop the customer spawning coroutine
    /// </summary>
    public void StopCustomerSpawning()
    {
        isSpawningCustomers = false;
        DebugLog("Stopped customer spawning");
    }

    /// <summary>
    /// Coroutine that continuously checks for free tables and spawns customers
    /// </summary>
    private IEnumerator CustomerSpawnCoroutine()
    {
        while (isSpawningCustomers)
        {
            // Check for free serving stations
            ServingStation freeStation = GetFreeServingStation();

            if (freeStation != null && Time.time >= lastSpawnTime + spawnInterval)
            {
                SpawnCustomer(freeStation);
                lastSpawnTime = Time.time;
            }

            // Wait a bit before checking again
            yield return new WaitForSeconds(1f);
        }
    }

    /// <summary>
    /// Get a free serving station that doesn't have a customer or a leaving customer
    /// </summary>
    private ServingStation GetFreeServingStation()
    {
        foreach (ServingStation station in servingStations)
        {
            if (station == null)
                continue;

            // Check if station has a customer assigned
            if (station.HasCustomer)
                continue;

            // UPDATED: Check for customers in new states (OrderingFood, Celebrating) as well
            bool hasLeavingCustomer = false;
            foreach (Customer customer in activeCustomers)
            {
                if (customer != null &&
                    customer.AssignedStation == station &&
                    (customer.CurrentState == CustomerState.Celebrating ||
                     customer.CurrentState == CustomerState.Leaving ||
                     customer.CurrentState == CustomerState.ReadyToDespawn))
                {
                    hasLeavingCustomer = true;
                    break;
                }
            }

            if (hasLeavingCustomer)
                continue;

            // Station is truly free - no current customer and no one leaving
            return station;
        }
        return null;
    }

    /// <summary>
    /// Spawn a customer and assign them to a serving station
    /// </summary>
    private void SpawnCustomer(ServingStation station)
    {
        if (station == null || customerSpawnPosition == null || menu.Count == 0)
        {
            DebugLog("Cannot spawn customer - missing configuration");
            return;
        }

        // Get a customer from the pool
        Customer customer = GetFromPool();
        if (customer == null)
        {
            DebugLog("Cannot spawn customer - pool exhausted");
            return;
        }

        // Position at door
        customer.transform.position = customerSpawnPosition.position;
        customer.transform.rotation = customerSpawnPosition.rotation;
        Debug.Log($"Spawned customer at {customer.transform.position}, door at {customerSpawnPosition.position}");

        // Set active
        customer.gameObject.SetActive(true);

        // Pick a random item from the menu
        FoodItemData orderRequest = menu[UnityEngine.Random.Range(0, menu.Count)];

        // Assign to station
        station.AssignCustomer(customer, orderRequest);

        // Tell customer to move to the station
        customer.AssignToStation(station, orderRequest, customerSpawnPosition);

        // Subscribe to serving events - Store the handler so we can properly unsubscribe later
        Action eventHandler = () => HandleCustomerServed(customer);
        customerEventHandlers[customer] = eventHandler;
        station.OnCustomerServedSuccessfully += eventHandler;

        // Track active customer
        activeCustomers.Add(customer);

        DebugLog($"Spawned customer at {station.StationName}, ordering {orderRequest.DisplayName}");
    }

    /// <summary>
    /// Called when a customer at a specific station is served
    /// This is a wrapper to handle the event with the specific customer reference
    /// </summary>
    private void HandleCustomerServed(Customer customer)
    {
        if (customer == null)
        {
            DebugLog("HandleCustomerServed called with null customer");
            return;
        }

        DebugLog($"Customer served successfully, will eat for {customerEatTime} seconds");

        if (OnCustomerServedSuccessfully == null)
            Debug.LogWarning("[CustomerManager] OnCustomerServedSuccessfully == null!");

        // Fire global event
        OnCustomerServedSuccessfully?.Invoke(customer.OrderRequest.foodValue);

        // Start eating coroutine
        StartCoroutine(CustomerEatingCoroutine(customer));
    }

    /// <summary>
    /// Coroutine that handles customer eating duration
    /// </summary>
    private IEnumerator CustomerEatingCoroutine(Customer customer)
    {
        yield return new WaitForSeconds(customerEatTime);

        if (customer != null && customer.CurrentState == CustomerState.Eating)
        {
            customer.FinishEating();
            // UPDATED: Don't call Leave() here - let the customer handle it after celebration
            DebugLog("Customer finished eating, starting celebration");
        }
    }

    /// <summary>
    /// Despawn a customer and return them to the pool
    /// </summary>
    private void DespawnCustomer(Customer customer)
    {
        if (customer == null) return;

        // Remove from active list
        activeCustomers.Remove(customer);

        // Unsubscribe from events - Use the stored handler to properly unsubscribe
        if (customer.AssignedStation != null && customerEventHandlers.ContainsKey(customer))
        {
            Action eventHandler = customerEventHandlers[customer];
            customer.AssignedStation.OnCustomerServedSuccessfully -= eventHandler;
            customerEventHandlers.Remove(customer);
            DebugLog("Unsubscribed from station events");
        }

        // Return to pool
        ReturnToPool(customer);

        DebugLog("Despawned customer");
    }

    #endregion

    #region Object Pooling

    /// <summary>
    /// Create a new customer for the pool
    /// </summary>
    private Customer CreateNewCustomer()
    {
        if (customerPrefab == null) return null;

        GameObject newObj = Instantiate(customerPrefab, poolParent);
        newObj.name = $"PooledCustomer_{allCustomers.Count + 1}";

        Customer customer = newObj.GetComponent<Customer>();
        if (customer == null)
        {
            Debug.LogError("[CustomerManager] Created customer doesn't have Customer component!");
            Destroy(newObj);
            return null;
        }

        allCustomers.Add(customer);
        newObj.SetActive(false);

        return customer;
    }

    /// <summary>
    /// Get a customer from the pool
    /// </summary>
    private Customer GetFromPool()
    {
        Customer customer = null;

        // Try to get from available pool
        if (availableCustomers.Count > 0)
        {
            customer = availableCustomers.Dequeue();
        }
        // Create new if pool is empty and we haven't hit max
        else if (allCustomers.Count < maxPoolSize)
        {
            customer = CreateNewCustomer();
        }

        // if (customer != null)
        // {
        //     customer.gameObject.SetActive(true);
        // }

        return customer;
    }

    /// <summary>
    /// Return a customer to the pool
    /// </summary>
    private void ReturnToPool(Customer customer)
    {
        if (customer == null) return;

        if (!allCustomers.Contains(customer))
        {
            Debug.LogWarning("[CustomerManager] Tried to return customer that doesn't belong to pool");
            return;
        }

        // Reset the customer
        customer.ResetForPooling();

        // Deactivate and return to pool
        customer.gameObject.SetActive(false);
        customer.transform.SetParent(poolParent);
        customer.transform.localPosition = Vector3.zero;

        availableCustomers.Enqueue(customer);
    }

    #endregion

    #region Public Utility Methods

    /// <summary>
    /// Get the number of active customers in the restaurant
    /// </summary>
    public int GetActiveCustomerCount()
    {
        return activeCustomers.Count;
    }

    /// <summary>
    /// Get the number of free serving stations
    /// </summary>
    public int GetFreeStationCount()
    {
        int count = 0;
        foreach (ServingStation station in servingStations)
        {
            if (station != null && !station.HasCustomer)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Despawn all active customers (makes them leave immediately)
    /// Used when a round ends
    /// </summary>
    public void DespawnAllCustomers()
    {
        // Create a copy of the list to avoid modification during iteration
        List<Customer> customersToRemove = new List<Customer>(activeCustomers);

        foreach (Customer customer in customersToRemove)
        {
            if (customer != null)
            {
                // Make the customer leave if they're not already leaving
                if (customer.CurrentState != CustomerState.Celebrating &&
                    customer.CurrentState != CustomerState.Leaving &&
                    customer.CurrentState != CustomerState.ReadyToDespawn)
                {
                    customer.Leave(customerSpawnPosition);
                }
            }
        }

        DebugLog($"Despawning all {customersToRemove.Count} active customers");
    }

    /// <summary>
    /// Wrapper for StartCustomerSpawning for consistency
    /// </summary>
    public void StartSpawning()
    {
        StartCustomerSpawning();
    }

    /// <summary>
    /// Wrapper for StopCustomerSpawning for consistency
    /// </summary>
    public void StopSpawning()
    {
        StopCustomerSpawning();
    }

    #endregion

    #region Debug

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[CustomerManager] {message}");
    }

    private void OnValidate()
    {
        spawnInterval = Mathf.Max(0.1f, spawnInterval);
        customerEatTime = Mathf.Max(0.1f, customerEatTime);
        initialPoolSize = Mathf.Max(1, initialPoolSize);
        maxPoolSize = Mathf.Max(initialPoolSize, maxPoolSize);
    }

    #endregion
}