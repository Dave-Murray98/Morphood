using UnityEngine;

/// <summary>
/// Core interface that all interactable objects must implement.
/// Defines the basic contract for player interaction in our multiplayer system.
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// Check if a specific player end can interact with this object.
    /// Used to enforce player-specific restrictions (e.g., only Player 1 can cook).
    /// </summary>
    /// <param name="playerEnd">The player end attempting the interaction</param>
    /// <returns>True if interaction is allowed</returns>
    bool CanInteract(PlayerEnd playerEnd);

    /// <summary>
    /// Perform the interaction. This is called when the player presses the interact button.
    /// For simple interactions (pickup/drop), this happens instantly.
    /// For complex interactions (cooking/chopping), this starts the process.
    /// </summary>
    /// <param name="playerEnd">The player end performing the interaction</param>
    /// <returns>True if the interaction was successful</returns>
    bool Interact(PlayerEnd playerEnd);

    /// <summary>
    /// Stop an ongoing interaction. Called when the player releases the interact button
    /// or moves away from the object during a hold-to-interact action.
    /// </summary>
    /// <param name="playerEnd">The player end stopping the interaction</param>
    void StopInteracting(PlayerEnd playerEnd);

    /// <summary>
    /// Get the priority of this interaction. Used when multiple objects are in range.
    /// Higher numbers = higher priority.
    /// </summary>
    int GetInteractionPriority();

    /// <summary>
    /// Get a short description of what this interaction does.
    /// Used for UI prompts like "Press to pick up" or "Hold to chop".
    /// </summary>
    string GetInteractionPrompt(PlayerEnd playerEnd);

    /// <summary>
    /// The transform of this interactable object (for distance calculations, etc.)
    /// </summary>
    Transform Transform { get; }

    /// <summary>
    /// Whether this object is currently available for interaction.
    /// False if it's being used by someone else or is in an invalid state.
    /// </summary>
    bool IsAvailable { get; }
}