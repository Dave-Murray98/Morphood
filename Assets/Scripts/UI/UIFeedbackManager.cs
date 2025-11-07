using MoreMountains.Feedbacks;
using UnityEngine;

public class UIFeedbackManager : MonoBehaviour
{
    [SerializeField] private MMF_Player buttonFeedback;

    public void PlayButtonFeedback()
    {
        buttonFeedback?.PlayFeedbacks();
    }
}
