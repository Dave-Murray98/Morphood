using UnityEngine;

public class CustomerAnimationHandler : MonoBehaviour
{
    [SerializeField] private Animator animator;

    [SerializeField] private string walkAnimationName;
    [SerializeField] private string idleAnimationName;
    [SerializeField] private string eatAnimationName;

    //anim hashs
    private int walkHash;
    private int idleHash;
    private int eatHash;

    private void Awake()
    {
        walkHash = Animator.StringToHash(walkAnimationName);
        idleHash = Animator.StringToHash(idleAnimationName);
        eatHash = Animator.StringToHash(eatAnimationName);
    }

    public void UpdateAnimation(CustomerState customerState)
    {
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

    private void SetAnimation(int animHash)
    {
        animator.CrossFade(animHash, 0.1f);
    }
}
