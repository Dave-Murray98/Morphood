using MoreMountains.Feedbacks;
using UnityEngine;

public class CookingStationFeedbackManager : MonoBehaviour
{
    public MMF_Player cookingLoopFeedback;
    public MMF_Player cookingStartFeedback;
    public MMF_Player cookingCompleteFeedback;

    public MMF_Player cancelCookingFeedback;

    public void PlayCookingFeedback()
    {
        cookingLoopFeedback?.PlayFeedbacks();
    }

    public void StopCookingFeedback()
    {
        cookingLoopFeedback?.StopFeedbacks();
    }

    public void PlayCookingStartFeedback()
    {
        cookingStartFeedback?.PlayFeedbacks();
    }

    public void PlayCookingCompleteFeedback()
    {
        cookingCompleteFeedback?.PlayFeedbacks();
    }

    public void PlayCancelCookingFeedback()
    {
        cancelCookingFeedback?.PlayFeedbacks();
    }

}
