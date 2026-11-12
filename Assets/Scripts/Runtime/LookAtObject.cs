using UnityEngine;

public class LookAtObject : MonoBehaviour
{
    public Transform target;
    public float headWeight = 0.8f;
    public float bodyWeight = 0.32f;
    private Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    private void OnAnimatorIK(int layer)
    {
        animator.SetLookAtPosition(target.position);
        animator.SetLookAtWeight(1, bodyWeight, headWeight);
    }
}
