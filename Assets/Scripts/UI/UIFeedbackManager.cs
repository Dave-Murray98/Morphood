using MoreMountains.Feedbacks;
using UnityEngine;

public class UIFeedbackManager : MonoBehaviour
{
    [SerializeField] private MMF_Player buttonFeedback;

    [SerializeField] private MMF_Player RoundStartFeedback;

    [SerializeField] private MMF_Player RoundEndSuccessFeedback;
    [SerializeField] private MMF_Player RoundEndFailureFeedback;

    public void PlayButtonFeedback()
    {
        buttonFeedback?.PlayFeedbacks();
    }

    public void PlayRoundStartFeedback()
    {
        RoundStartFeedback?.PlayFeedbacks();
    }

    public void PlayRoundEndSuccessFeedback()
    {
        RoundEndSuccessFeedback?.PlayFeedbacks();
    }

    public void PlayRoundEndFailureFeedback()
    {
        RoundEndFailureFeedback?.PlayFeedbacks();
    }
}
