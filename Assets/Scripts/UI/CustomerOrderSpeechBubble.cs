using UnityEngine;
using UnityEngine.UI;

public class CustomerOrderSpeechBubble : MonoBehaviour
{
    [SerializeField] private Canvas canvas;
    [SerializeField] private Image speechBubbleImage;
    [SerializeField] private Image orderImage;


    public void Show(Sprite orderSprite)
    {
        orderImage.sprite = orderSprite;
        canvas.enabled = true;
        speechBubbleImage.enabled = true;
    }

    public void Hide()
    {
        canvas.enabled = false;
        speechBubbleImage.enabled = false;
    }
}
