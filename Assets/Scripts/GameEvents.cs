using UnityEngine;
using System;

public class GameEvents
{
    #region Game State Events
    public static event Action OnGamePaused;
    public static event Action OnGameResumed;

    #endregion

    #region Trigger Methods    

    public static void TriggerGamePaused() => OnGamePaused?.Invoke();

    public static void TriggerGameResumed() => OnGameResumed?.Invoke();

    #endregion


}
