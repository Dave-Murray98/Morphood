using MoreMountains.Feedbacks;
using UnityEngine;

public class CustomerFeedbackManager : MonoBehaviour
{
    [SerializeField] private MMF_Player celebrationFeedback;
    [SerializeField] private MMF_Player orderSpeechBubbleFeedback;
    [SerializeField] private MMF_Player moneyUIFeedback;

    public void PlayCelebrationFeedback()
    {
        celebrationFeedback?.PlayFeedbacks();
    }

    public void PlayOrderSpeechBubbleFeedback()
    {
        orderSpeechBubbleFeedback?.PlayFeedbacks();
    }

    public void PlayMoneyUIFeedback()
    {
        moneyUIFeedback?.PlayFeedbacks();
    }
}
