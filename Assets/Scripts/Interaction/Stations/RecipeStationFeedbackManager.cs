using MoreMountains.Feedbacks;
using UnityEngine;

public class RecipeStationFeedbackManager : MonoBehaviour
{
    [SerializeField] private MMF_Player showRecipeUIFeedback;
    [SerializeField] private MMF_Player hideRecipeUIFeedback;

    public void PlayShowRecipeUIFeedback()
    {
        showRecipeUIFeedback?.PlayFeedbacks();
    }

    public void PlayHideRecipeUIFeedback()
    {
        hideRecipeUIFeedback?.PlayFeedbacks();
    }
}
