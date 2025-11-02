using UnityEngine;
using MoreMountains.Feedbacks;

public class FoodItemFeedbackManager : MonoBehaviour
{
    public MMF_Player placementFeedback;

    public void PlayPlacementFeedback()
    {
        placementFeedback.PlayFeedbacks();
    }
}
