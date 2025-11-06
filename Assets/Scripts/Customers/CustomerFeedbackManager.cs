using MoreMountains.Feedbacks;
using UnityEngine;

public class CustomerFeedbackManager : MonoBehaviour
{
    [SerializeField] private MMF_Player celebrationFeedback;
    [SerializeField] private MMF_Player showOrderSpeechBubbleFeedback;
    [SerializeField] private MMF_Player hideOrderSpeechBubbleFeedback;

    [SerializeField] private MMF_Player moneyUIFeedback;

    public void PlayCelebrationFeedback()
    {
        celebrationFeedback?.PlayFeedbacks();
    }

    public void PlayShowSpeechBubbleFeedback()
    {
        showOrderSpeechBubbleFeedback?.PlayFeedbacks();
    }

    public void PlayHideSpeechBubbleFeedback()
    {
        hideOrderSpeechBubbleFeedback?.PlayFeedbacks();
    }

    public void PlayMoneyFeedback()
    {
        moneyUIFeedback?.PlayFeedbacks();
    }
}
