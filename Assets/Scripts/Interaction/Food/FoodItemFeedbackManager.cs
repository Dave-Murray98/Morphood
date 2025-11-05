using UnityEngine;
using MoreMountains.Feedbacks;
using System;

public class FoodItemFeedbackManager : MonoBehaviour
{
    public MMF_Player placementFeedback;

    public MMF_Player cookingFeedback;

    public MMF_Player choppingFeedback;

    public void PlayPlacementFeedback()
    {
        placementFeedback.PlayFeedbacks();
    }

    public void PlayCookingFeedback()
    {
        cookingFeedback.PlayFeedbacks();
    }

    public void StopCookingFeedback()
    {
        cookingFeedback.StopFeedbacks();
    }

    public void PlayChoppingFeedback()
    {
        choppingFeedback.PlayFeedbacks();
    }

    public void StopChoppingFeedback()
    {
        choppingFeedback.StopFeedbacks();
    }
}
