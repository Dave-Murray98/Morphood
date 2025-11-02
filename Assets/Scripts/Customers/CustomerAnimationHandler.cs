using Sirenix.OdinInspector;
using UnityEngine;

public class CustomerAnimationHandler : MonoBehaviour
{
    [SerializeField] private Animator animator;

    [SerializeField] private string walkAnimationName;
    [SerializeField] private string idleAnimationName;
    [SerializeField] private string eatAnimationName;
    [SerializeField] private string waitingAnimationName1;
    [SerializeField] private string waitingAnimationName2;
    [SerializeField] private string waitingAnimationName3;

    [SerializeField] private float waitingAnimationInterlude = 2f;
    [ShowInInspector] private float waitAnimTimer = 0f;

    //anim hashs
    private int walkHash;
    private int idleHash;
    private int eatHash;
    private int waitingHash1;
    private int waitingHash2;
    private int waitingHash3;

    public CustomerState currentState;

    [ShowInInspector] private bool isRandomWaitingAnimationPlaying = false;

    private void Awake()
    {
        walkHash = Animator.StringToHash(walkAnimationName);
        idleHash = Animator.StringToHash(idleAnimationName);
        eatHash = Animator.StringToHash(eatAnimationName);

        waitingHash1 = Animator.StringToHash(waitingAnimationName1);
        waitingHash2 = Animator.StringToHash(waitingAnimationName2);
        waitingHash3 = Animator.StringToHash(waitingAnimationName3);
    }

    public void UpdateAnimationState(CustomerState customerState)
    {
        currentState = customerState;

        switch (customerState)
        {
            case CustomerState.Idle:
                SetAnimation(idleHash);
                break;
            case CustomerState.MovingToTable:
                SetAnimation(walkHash);
                break;
            case CustomerState.WaitingForFood:
                SetAnimation(idleHash);
                break;
            case CustomerState.Eating:
                SetAnimation(eatHash);
                break;
            case CustomerState.Leaving:
                SetAnimation(walkHash);
                break;
            case CustomerState.ReadyToDespawn:
                SetAnimation(idleHash);
                break;
        }
    }

    private void Update()
    {
        //Add a little bit of randomness to the waiting idle animation
        if (currentState == CustomerState.WaitingForFood || currentState == CustomerState.Idle)
        {
            //if we're not already playing a random animation, wait for the interlude and play a random one
            if (!isRandomWaitingAnimationPlaying)
            {
                waitAnimTimer += Time.deltaTime;
                if (waitAnimTimer >= waitingAnimationInterlude)
                {
                    PlayRandomWaitingAnimation();
                    waitAnimTimer = 0f;
                }

            }
        }
        else
        {
            waitAnimTimer = 0f;
        }
    }

    private void PlayRandomWaitingAnimation()
    {
        isRandomWaitingAnimationPlaying = true;
        int randomAnimation = Random.Range(1, 4);
        switch (randomAnimation)
        {
            case 1:
                SetAnimation(waitingHash1);
                break;
            case 2:
                SetAnimation(waitingHash2);
                break;
            case 3:
                SetAnimation(waitingHash3);
                break;
        }
    }

    //this is called by a animation event at the end of the random waiting animations
    public void OnRandomWaitingAnimationFinished()
    {
        //Debug.Log($"Random waiting animation finished, last animation name was {animator.GetCurrentAnimatorStateInfo(0).fullPathHash}");
        isRandomWaitingAnimationPlaying = false;
    }

    private void SetAnimation(int animHash)
    {
        animator.CrossFade(animHash, 0.1f);
    }
}
