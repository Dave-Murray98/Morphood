using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CustomerUI : MonoBehaviour
{
    [Header("Order Speech Bubble UI")]
    public GameObject speechBubble;
    [SerializeField] private Image orderImage;

    [Header("Money UI")]
    [Tooltip("The amount of time the money UI is shown for")]
    // [SerializeField] private float moneyUIShowDuration = 2f;
    [SerializeField] private GameObject moneyUI;
    // [SerializeField] private TextMeshProUGUI moneyText;

    [Header("UI Feedback")]
    [SerializeField] private CustomerFeedbackManager customerFeedbackManager;


    public void ShowSpeechBubble(Sprite orderSprite)
    {
        orderImage.sprite = orderSprite;
        //speechBubble.SetActive(true);
        customerFeedbackManager.PlayShowSpeechBubbleFeedback();
    }

    public void HideSpeechBubble()
    {
        customerFeedbackManager.PlayHideSpeechBubbleFeedback();
    }

    public void HideSpeechBubbleImmediate()
    {
        speechBubble.SetActive(false);
    }

    public void HideMoneyUI()
    {
        moneyUI.SetActive(false);
    }

    private void LateUpdate()
    {
        //ensure that the y roatation is always zero to face the camera properly and not be affected by parent rotation
        Vector3 euler = transform.eulerAngles;
        euler.y = -90f;
        transform.eulerAngles = euler;
    }
}
