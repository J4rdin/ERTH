using UnityEngine;
using UnityEngine.UI;

public class CombatUI : MonoBehaviour
{
    [Header("References")]
    public CombatTransitionController transitionController;
    public GridMovement gridMovement;
    public GameObject actionPanelRoot;

    [Header("Action Buttons")]
    public Button moveButton;
    public Button attackPlaceholderButton;
    public Button itemPlaceholderButton;
    public Button leaveCombatButton;

    private GameMode lastObservedMode = GameMode.FreeRoam;

    void Awake()
    {
        if (transitionController == null)
            transitionController = FindFirstObjectByType<CombatTransitionController>();

        if (gridMovement == null)
            gridMovement = FindFirstObjectByType<GridMovement>();

        if (moveButton != null)
            moveButton.onClick.AddListener(OnMoveClicked);

        if (attackPlaceholderButton != null)
            attackPlaceholderButton.onClick.AddListener(OnAttackPlaceholderClicked);

        if (itemPlaceholderButton != null)
            itemPlaceholderButton.onClick.AddListener(OnItemPlaceholderClicked);

        if (leaveCombatButton != null)
            leaveCombatButton.onClick.AddListener(OnLeaveCombatClicked);

        SetPanelVisible(false);
    }

    void Update()
    {
        if (transitionController == null)
            return;

        if (transitionController.currentMode != lastObservedMode)
        {
            lastObservedMode = transitionController.currentMode;

            if (lastObservedMode == GameMode.GridCombat)
            {
                SetPanelVisible(true);
                if (gridMovement != null)
                    gridMovement.SetMoveSelectionActive(false);
            }
            else if (lastObservedMode == GameMode.FreeRoam || lastObservedMode == GameMode.Transitioning)
            {
                if (gridMovement != null)
                    gridMovement.SetMoveSelectionActive(false);
                SetPanelVisible(false);
            }
        }
    }

    public void OnMoveClicked()
    {
        if (transitionController == null || transitionController.currentMode != GameMode.GridCombat)
            return;

        if (gridMovement != null)
        {
            gridMovement.SetMoveSelectionActive(true);
            Debug.Log("[Combat UI] Move selected — reachable tiles highlighted; hover to preview and click to move.");
        }
    }

    public void OnAttackPlaceholderClicked()
    {
        if (gridMovement != null)
            gridMovement.SetMoveSelectionActive(false);

        Debug.Log("[Combat UI] Attack action selected (Placeholder).");
    }

    public void OnItemPlaceholderClicked()
    {
        if (gridMovement != null)
            gridMovement.SetMoveSelectionActive(false);

        Debug.Log("[Combat UI] Item/Ability action selected (Placeholder).");
    }

    public void OnLeaveCombatClicked()
    {
        if (gridMovement != null)
            gridMovement.SetMoveSelectionActive(false);

        SetPanelVisible(false);

        if (transitionController != null)
            transitionController.ReturnToFreeRoam();
    }

    private void SetPanelVisible(bool visible)
    {
        if (actionPanelRoot != null)
            actionPanelRoot.SetActive(visible);
    }
}