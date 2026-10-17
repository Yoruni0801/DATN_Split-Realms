using UnityEngine;

[RequireComponent(typeof(Animator))]
public class AcidRainParent : MonoBehaviour
{
    public Animator animator;
    private void Awake()
    {
        animator = GetComponent<Animator>();
    }
    void OnEnable()
    {
        animator.SetTrigger("AcidRainParent_Enable");
    }
}
