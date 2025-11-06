using UnityEngine;
using MoreMountains.Feedbacks;
using System;

public class FoodItemFeedbackManager : MonoBehaviour
{
    public MMF_Player placementFeedback;

    public MMF_Player cookingFeedback;

    public MMF_Player choppingFeedback;

    public MMF_Player servedFeedback;

    public void PlayPlacementFeedback()
    {
        if (placementFeedback != null)
            placementFeedback.PlayFeedbacks();
    }

    public void PlayServedFeedback()
    {
        if (servedFeedback != null)
            servedFeedback.PlayFeedbacks();
    }

    public void PlayCookingFeedback()
    {
        if (cookingFeedback != null)
            cookingFeedback.PlayFeedbacks();
    }

    public void StopCookingFeedback()
    {
        if (cookingFeedback != null)
            cookingFeedback.StopFeedbacks();
    }

    public void PlayChoppingFeedback()
    {
        if (choppingFeedback != null)
            choppingFeedback.PlayFeedbacks();
    }

    public void StopChoppingFeedback()
    {
        if (choppingFeedback != null)
            choppingFeedback.StopFeedbacks();
    }
}
