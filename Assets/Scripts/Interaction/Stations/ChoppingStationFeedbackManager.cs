using MoreMountains.Feedbacks;
using UnityEngine;

public class ChoppingStationFeedbackManager : MonoBehaviour
{

    public MMF_Player choppingFeedback;


    public void PlayChoppingFeedback()
    {
        choppingFeedback?.PlayFeedbacks();
    }

    public void StopChoppingFeedback()
    {
        choppingFeedback?.StopFeedbacks();
    }

}
