using UnityEngine;

/// <summary>
/// Utility class to handle PlayerEnd detection refresh when items are transformed or replaced on stations.
/// This fixes the issue where players can't interact with newly created items.
/// </summary>
public static class PlayerEndDetectionRefresher
{

    /// <summary>
    /// Refresh PlayerEnd detection for all players near a specific station
    /// Call this after placing, removing, or transforming items on stations
    /// </summary>
    /// <param name="station">The station that had items changed</param>
    /// <param name="debugName">Name for debug logging</param>
    public static void RefreshNearStation(Transform station, string debugName = "Station")
    {
        if (station == null) return;

        // Find all PlayerEnd components in the scene
        PlayerEnd[] allPlayerEnds = Object.FindObjectsByType<PlayerEnd>(FindObjectsSortMode.None);

        foreach (PlayerEnd playerEnd in allPlayerEnds)
        {
            if (IsPlayerNearStation(playerEnd, station))
            {
                Debug.Log($"[PlayerEndDetectionRefresher] Refreshing detection for {playerEnd.name} near {debugName}");
                playerEnd.RefreshInteractableDetection();
            }
        }
    }

    /// <summary>
    /// Check if a PlayerEnd is close enough to a station to need detection refresh
    /// </summary>
    /// <param name="playerEnd">The PlayerEnd to check</param>
    /// <param name="station">The station transform</param>
    /// <returns>True if the player is close enough to need refresh</returns>
    private static bool IsPlayerNearStation(PlayerEnd playerEnd, Transform station)
    {
        if (playerEnd == null || station == null) return false;

        // Check if PlayerEnd's trigger collider overlaps with the station area
        Collider playerCollider = playerEnd.GetComponent<Collider>();
        Collider stationCollider = station.GetComponent<Collider>();

        if (playerCollider != null && stationCollider != null)
        {
            // Use bounds intersection for quick check
            return stationCollider.bounds.Intersects(playerCollider.bounds);
        }

        // Fallback: distance-based check
        float distance = Vector3.Distance(playerEnd.transform.position, station.position);
        return distance <= 5f; // Reasonable interaction range
    }

    /// <summary>
    /// Refresh detection for a specific PlayerEnd if they're near a station
    /// </summary>
    /// <param name="playerEnd">The specific PlayerEnd to refresh</param>
    /// <param name="station">The station that had changes</param>
    /// <param name="debugName">Name for debug logging</param>
    public static void RefreshSpecificPlayer(PlayerEnd playerEnd, Transform station, string debugName = "Station")
    {
        if (playerEnd == null || station == null) return;

        if (IsPlayerNearStation(playerEnd, station))
        {
            Debug.Log($"[PlayerEndDetectionRefresher] Refreshing detection for {playerEnd.name} near {debugName}");
            playerEnd.RefreshInteractableDetection();
        }
    }

    /// <summary>
    /// Force refresh all PlayerEnds in the scene - use sparingly as this can be expensive
    /// </summary>
    public static void RefreshAllPlayers(string reason = "Unknown")
    {
        PlayerEnd[] allPlayerEnds = Object.FindObjectsByType<PlayerEnd>(FindObjectsSortMode.None);

        Debug.Log($"[PlayerEndDetectionRefresher] Force refreshing all {allPlayerEnds.Length} PlayerEnds - Reason: {reason}");

        foreach (PlayerEnd playerEnd in allPlayerEnds)
        {
            if (playerEnd != null)
            {
                playerEnd.RefreshInteractableDetection();
            }
        }
    }
}