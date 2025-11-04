using Sirenix.OdinInspector;
using UnityEngine;
using System.Collections.Generic;

public class CustomerAnimationHandler : MonoBehaviour
{
    [SerializeField] private Animator animator;

    [Header("Basic Animations")]
    [SerializeField] private string walkAnimationName;
    [SerializeField] private string idleAnimationName;
    [SerializeField] private string eatAnimationName;

    [Header("Random Animations")]
    [SerializeField] private List<string> waitingAnimationNames = new List<string>();
    [SerializeField] private List<string> greetingAnimationNames = new List<string>();
    [SerializeField] private List<string> celebrationAnimationNames = new List<string>();

    [Header("Animation Timing")]
    [SerializeField] private float waitingAnimationInterlude = 2f;
    [ShowInInspector] private float waitAnimTimer = 0f;

    //anim hashes
    private int walkHash;
    private int idleHash;
    private int eatHash;
    private List<int> waitingHashes = new List<int>();
    private List<int> greetingHashes = new List<int>();
    private List<int> celebrationHashes = new List<int>();

    public CustomerState currentState;

    [ShowInInspector] public bool isRandomWaitingAnimationPlaying = false;
    [ShowInInspector] public bool isGreetingAnimationPlaying = false;
    [ShowInInspector] public bool isCelebrationAnimationPlaying = false;

    private void Awake()
    {
        // Cache basic animation hashes
        walkHash = Animator.StringToHash(walkAnimationName);
        idleHash = Animator.StringToHash(idleAnimationName);
        eatHash = Animator.StringToHash(eatAnimationName);

        // Cache waiting animation hashes
        waitingHashes.Clear();
        foreach (string animName in waitingAnimationNames)
        {
            if (!string.IsNullOrEmpty(animName))
            {
                waitingHashes.Add(Animator.StringToHash(animName));
            }
        }

        // Cache greeting animation hashes
        greetingHashes.Clear();
        foreach (string animName in greetingAnimationNames)
        {
            if (!string.IsNullOrEmpty(animName))
            {
                greetingHashes.Add(Animator.StringToHash(animName));
            }
        }

        // Cache celebration animation hashes
        celebrationHashes.Clear();
        foreach (string animName in celebrationAnimationNames)
        {
            if (!string.IsNullOrEmpty(animName))
            {
                celebrationHashes.Add(Animator.StringToHash(animName));
            }
        }
    }

    public void UpdateAnimationState(CustomerState customerState)
    {
        currentState = customerState;

        // Reset animation flags when changing states
        ResetAnimationFlags();

        switch (customerState)
        {
            case CustomerState.Idle:
                SetAnimation(idleHash);
                break;
            case CustomerState.MovingToTable:
                SetAnimation(walkHash);
                break;
            case CustomerState.OrderingFood:
                PlayRandomGreetingAnimation();
                break;
            case CustomerState.WaitingForFood:
                SetAnimation(idleHash);
                break;
            case CustomerState.Eating:
                SetAnimation(eatHash);
                break;
            case CustomerState.Celebrating:
                PlayRandomCelebrationAnimation();
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
        // Add a little bit of randomness to the waiting idle animation
        if (currentState == CustomerState.WaitingForFood || currentState == CustomerState.Idle)
        {
            // Only play random waiting animations if we're not playing other special animations
            if (!isRandomWaitingAnimationPlaying && !isGreetingAnimationPlaying && !isCelebrationAnimationPlaying)
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
            isRandomWaitingAnimationPlaying = false;
        }
    }

    /// <summary>
    /// Play a random greeting animation when customer is in OrderingFood state
    /// </summary>
    private void PlayRandomGreetingAnimation()
    {
        if (greetingHashes.Count == 0)
        {
            Debug.LogWarning("[CustomerAnimationHandler] No greeting animations available, using idle");
            SetAnimation(idleHash);
            return;
        }

        isGreetingAnimationPlaying = true;
        int randomIndex = Random.Range(0, greetingHashes.Count);
        SetAnimation(greetingHashes[randomIndex]);
    }

    /// <summary>
    /// Play a random celebration animation when customer is in Celebrating state
    /// </summary>
    private void PlayRandomCelebrationAnimation()
    {
        if (celebrationHashes.Count == 0)
        {
            Debug.LogWarning("[CustomerAnimationHandler] No celebration animations available, using idle");
            SetAnimation(idleHash);
            return;
        }

        isCelebrationAnimationPlaying = true;
        int randomIndex = Random.Range(0, celebrationHashes.Count);
        SetAnimation(celebrationHashes[randomIndex]);
    }

    private void PlayRandomWaitingAnimation()
    {
        if (waitingHashes.Count == 0)
        {
            Debug.LogWarning("[CustomerAnimationHandler] No waiting animations available");
            return;
        }

        isRandomWaitingAnimationPlaying = true;
        int randomIndex = Random.Range(0, waitingHashes.Count);
        SetAnimation(waitingHashes[randomIndex]);
    }

    #region Animation Events
    // These methods are called by animation events at the end of animations

    /// <summary>
    /// Called by animation event at the end of random waiting animations
    /// </summary>
    public void OnRandomWaitingAnimationFinished()
    {
        //Debug.Log($"Random waiting animation finished, last animation name was {animator.GetCurrentAnimatorStateInfo(0).fullPathHash}");
        isRandomWaitingAnimationPlaying = false;
    }

    /// <summary>
    /// Called by animation event at the end of greeting animations
    /// </summary>
    public void OnGreetingAnimationFinished()
    {
        isGreetingAnimationPlaying = false;

        // Notify Customer component that greeting is done
        Customer customer = GetComponent<Customer>();
        if (customer != null)
        {
            customer.OnGreetingComplete();
        }
    }

    /// <summary>
    /// Called by animation event at the end of celebration animations
    /// </summary>
    public void OnCelebrationAnimationFinished()
    {
        isCelebrationAnimationPlaying = false;

        // Notify Customer component that celebration is done
        Customer customer = GetComponent<Customer>();
        if (customer != null)
        {
            customer.OnCelebrationComplete();
        }
    }

    #endregion

    private void SetAnimation(int animHash)
    {
        animator.CrossFade(animHash, 0.1f);
    }

    /// <summary>
    /// Reset all animation flags
    /// </summary>
    private void ResetAnimationFlags()
    {
        isRandomWaitingAnimationPlaying = false;
        isGreetingAnimationPlaying = false;
        isCelebrationAnimationPlaying = false;
        waitAnimTimer = 0f;
    }

    /// <summary>
    /// Reset animation flags when customer is returned to pool
    /// </summary>
    public void ResetForPooling()
    {
        ResetAnimationFlags();
    }
}