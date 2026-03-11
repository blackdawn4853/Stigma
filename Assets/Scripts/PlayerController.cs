using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;

    private Vector3 targetPosition;
    private bool isMoving = false;
    private System.Action onReachedTarget;
    private Animator animator;

    void Start()
    {
        targetPosition = transform.position;
        animator = GetComponent<Animator>();
        Debug.Log($"Animator: {animator}");
    }

    void Update()
    {
        if (isMoving)
        {
            transform.position = Vector3.MoveTowards(
                transform.position, targetPosition, moveSpeed * Time.deltaTime);

            Vector3 dir = (targetPosition - transform.position).normalized;

            if (animator != null)
            {
                animator.SetFloat("MoveX", dir.x);
                animator.SetFloat("MoveY", dir.y);
                animator.SetBool("isMoving", true);
                Debug.Log($"dir: {dir} MoveX:{dir.x} MoveY:{dir.y} isMoving:true");
            }
            else
            {
                Debug.LogError("Animator가 null이야!");
            }

            if (Vector3.Distance(transform.position, targetPosition) < 0.05f)
            {
                transform.position = targetPosition;
                isMoving = false;

                if (animator != null)
                {
                    animator.SetFloat("MoveX", 0);
                    animator.SetFloat("MoveY", 0);
                    animator.SetBool("isMoving", false);
                }

                onReachedTarget?.Invoke();
                onReachedTarget = null;
            }
        }
    }

    public void MoveTo(Vector3 target, System.Action callback = null)
    {
        targetPosition = target;
        isMoving = true;
        onReachedTarget = callback;
    }

    public bool IsMoving() => isMoving;
}