using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CustomerUI : MonoBehaviour
{
    [Header("Order Speech Bubble UI")]
    [SerializeField] private GameObject speechBubble;
    [SerializeField] private Image orderImage;

    [Header("Money UI")]
    [Tooltip("The amount of time the money UI is shown for")]
    [SerializeField] private float moneyUIShowDuration = 2f;
    [SerializeField] private GameObject moneyUI;
    [SerializeField] private TextMeshProUGUI moneyText;

    [Header("UI Feedback")]
    [SerializeField] private CustomerFeedbackManager customerFeedbackManager;


    public void ShowSpeechBubble(Sprite orderSprite)
    {
        orderImage.sprite = orderSprite;
        speechBubble.SetActive(true);
        customerFeedbackManager.PlayOrderSpeechBubbleFeedback();
    }

    public void HideSpeechBubble()
    {
        speechBubble.SetActive(false);
    }

    public IEnumerator ShowMoneyUICoroutine(float amount)
    {
        ShowMoneyUI(amount);
        yield return new WaitForSeconds(moneyUIShowDuration);
        HideMoneyUI();
    }

    private void ShowMoneyUI(float amount)
    {
        moneyUI.SetActive(true);
        moneyText.text = $"+${amount}";
    }

    public void HideMoneyUI()
    {
        moneyUI.SetActive(false);
    }
}
