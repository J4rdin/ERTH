using UnityEngine;

public class EnemyAggro : MonoBehaviour
{
    [Header("Detection Settings")]
    public float detectionRadius = 6f;
    private Transform playerTransform;
    private bool combatTriggered = false;

    void Start()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }
    }

    void Update()
    {
        if (combatTriggered || playerTransform == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        if (distanceToPlayer <= detectionRadius)
        {
            TriggerCombat();
        }
    }

    private void TriggerCombat()
    {
        combatTriggered = true;
        Debug.Log($"Enemy '{gameObject.name}' spotted the player!");

        CombatTransitionController controller = playerTransform.GetComponent<CombatTransitionController>();
        if (controller != null && controller.currentMode == GameMode.FreeRoam)
        {
            controller.StartCoroutine(controller.TransitionToGridCombat());
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}