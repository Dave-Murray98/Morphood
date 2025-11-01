using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class StationManager : MonoBehaviour
{
    public static StationManager Instance { get; private set; }

    [ShowInInspector] private List<BaseStation> stations = new List<BaseStation>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Find all stations in the scene and add them to the list
        stations.AddRange(FindObjectsByType<BaseStation>(FindObjectsSortMode.None));

    }

    public void ClearAllStations()
    {
        Debug.Log("Clearing all stations");
        foreach (var station in stations)
        {
            station.ClearItem();
            Debug.Log("Cleared " + station.gameObject.name);
        }
    }
}
