using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using StarterAssets;

public enum GameMode { FreeRoam, Transitioning, GridCombat }

public class CombatTransitionController : MonoBehaviour
{
    [Header("References")]
    public GridManager gridManager;
    public CinemachineCamera freeRoamCamera;
    public CinemachineCamera combatCamera;
    public float snapDuration = 0.3f;

    [Header("Current Status")]
    public GameMode currentMode = GameMode.FreeRoam;

    private ThirdPersonController thirdPersonController;
    private CharacterController characterController;
    private StarterAssetsInputs starterInputs;
    private Animator animator;

    void Start()
    {
        thirdPersonController = GetComponent<ThirdPersonController>();
        characterController = GetComponent<CharacterController>();
        starterInputs = GetComponent<StarterAssetsInputs>();
        animator = GetComponent<Animator>();

        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();
    }

    void Update()
    {
        if (Keyboard.current == null) return;

        bool pressedEnterCombat = Keyboard.current.pKey.wasPressedThisFrame || Keyboard.current.tKey.wasPressedThisFrame;

        if (currentMode == GameMode.FreeRoam && pressedEnterCombat)
        {
            StartCoroutine(TransitionToGridCombat());
        }
        else if (currentMode == GameMode.GridCombat && Keyboard.current.rKey.wasPressedThisFrame)
        {
            ReturnToFreeRoam();
        }
    }

    public IEnumerator TransitionToGridCombat()
    {
        if (currentMode != GameMode.FreeRoam) yield break;

        currentMode = GameMode.Transitioning;

        if (starterInputs != null)
        {
            starterInputs.move = Vector2.zero;
            starterInputs.cursorLocked = false;
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
            animator.SetFloat("MotionSpeed", 0f);
        }

        if (thirdPersonController != null) thirdPersonController.enabled = false;
        if (characterController != null) characterController.enabled = false;

        if (combatCamera != null)
        {
            combatCamera.Follow = null;
            Vector3 combatCamPos = transform.position + new Vector3(0f, 12f, -8f);
            combatCamera.transform.position = combatCamPos;
            combatCamera.transform.LookAt(transform.position);
        }

        if (freeRoamCamera != null) freeRoamCamera.Priority = 0;
        if (combatCamera != null) combatCamera.Priority = 20;

        if (gridManager != null) gridManager.SetGridVisibility(true);

        Vector2Int targetCell = gridManager.GetGridPosition(transform.position);
        Vector3 targetPosition = gridManager.GetCellCenterWorld(targetCell);

        Vector3 startPosition = transform.position;
        float elapsedTime = 0f;

        while (elapsedTime < snapDuration)
        {
            transform.position = Vector3.Lerp(startPosition, targetPosition, elapsedTime / snapDuration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPosition;
        currentMode = GameMode.GridCombat;

        Debug.Log($"[Combat Started] Snapped to cell: {targetCell}");
    }

    public void ReturnToFreeRoam()
    {
        if (combatCamera != null)
        {
            combatCamera.Follow = transform;
        }

        if (combatCamera != null) combatCamera.Priority = 0;
        if (freeRoamCamera != null) freeRoamCamera.Priority = 20;

        if (gridManager != null) gridManager.SetGridVisibility(false);

        if (characterController != null) characterController.enabled = true;
        if (thirdPersonController != null) thirdPersonController.enabled = true;
        if (starterInputs != null) starterInputs.cursorLocked = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        currentMode = GameMode.FreeRoam;
        Debug.Log("[Free-Roam Restored]");
    }
}