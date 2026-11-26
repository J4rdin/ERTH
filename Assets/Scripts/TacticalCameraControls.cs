using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

public class TacticalCameraControls : MonoBehaviour
{
    [Header("Pan & Zoom Settings")]
    public float panSpeed = 25f;
    public float zoomSpeed = 15f;
    public float minZoom = 4f;
    public float maxZoom = 25f;

    private CombatTransitionController transitionController;
    private CinemachineCamera cinemachineCamera;

    void Start()
    {
        transitionController = FindFirstObjectByType<CombatTransitionController>();
        cinemachineCamera = GetComponent<CinemachineCamera>();
    }

    void Update()
    {
        if (transitionController == null || transitionController.currentMode != GameMode.GridCombat)
            return;

        if (Mouse.current == null) return;

        if (mouseDragActive())
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            Vector3 moveDirection = new Vector3(-mouseDelta.x, 0f, -mouseDelta.y);
            transform.position += moveDirection * panSpeed * Time.deltaTime;
        }

        float scroll = Mouse.current.scroll.y.ReadValue();
        if (scroll != 0f)
        {
            Vector3 pos = transform.position;
            pos.y -= scroll * zoomSpeed * Time.deltaTime;
            pos.y = Mathf.Clamp(pos.y, minZoom, maxZoom);
            transform.position = pos;
        }
    }

    private bool mouseDragActive()
    {
        return Mouse.current.rightButton.isPressed;
    }
}