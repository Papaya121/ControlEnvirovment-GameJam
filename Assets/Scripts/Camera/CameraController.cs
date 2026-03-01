using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string moveActionName = "Move";

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 12f;
    [SerializeField, Min(0f)] private float smoothness = 0f;
    [SerializeField] private bool keepHeight = true;

    [Header("Camera Angle")]
    [SerializeField, Range(10f, 89f)] private float pitchAngle = 50f;
    [SerializeField] private float yawAngle = 0f;
    [SerializeField] private bool applyAngleOnStart = true;

    [Header("Bounds")]
    [SerializeField] private bool clampToBounds = true;
    [SerializeField] private float minX = -25f;
    [SerializeField] private float maxX = 25f;
    [SerializeField] private float minZ = -25f;
    [SerializeField] private float maxZ = 25f;

    private InputAction moveAction;
    private bool ownsAction;
    private float lockedY;
    private Vector3 desiredPosition;

    private void Awake()
    {
        Camera sceneCamera = GetComponent<Camera>();
        if (sceneCamera != null)
        {
            sceneCamera.orthographic = false;
        }

        if (applyAngleOnStart)
        {
            transform.rotation = Quaternion.Euler(pitchAngle, yawAngle, 0f);
        }

        lockedY = transform.position.y;
        desiredPosition = transform.position;
        SetupMoveAction();
    }

    private void OnEnable()
    {
        desiredPosition = transform.position;
        moveAction?.Enable();
    }

    private void OnDisable()
    {
        moveAction?.Disable();
    }

    private void OnDestroy()
    {
        if (ownsAction)
        {
            moveAction.Dispose();
        }
    }

    private void Update()
    {
        if (GameStateManager.IsGameplayInputBlocked)
        {
            return;
        }

        if (moveAction == null)
        {
            return;
        }

        Vector2 input = moveAction.ReadValue<Vector2>();
        if (input.sqrMagnitude >= 0.0001f)
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            forward.Normalize();

            Vector3 right = transform.right;
            right.y = 0f;
            right.Normalize();

            Vector3 movement = (right * input.x + forward * input.y) * moveSpeed * Time.deltaTime;
            desiredPosition += movement;
        }

        if (keepHeight)
        {
            desiredPosition.y = lockedY;
        }

        if (clampToBounds)
        {
            desiredPosition.x = Mathf.Clamp(desiredPosition.x, minX, maxX);
            desiredPosition.z = Mathf.Clamp(desiredPosition.z, minZ, maxZ);
        }

        if (smoothness <= 0f)
        {
            transform.position = desiredPosition;
            return;
        }

        float lerpFactor = 1f - Mathf.Exp(-Time.deltaTime / smoothness);
        transform.position = Vector3.Lerp(transform.position, desiredPosition, lerpFactor);
    }

    private void SetupMoveAction()
    {
        if (inputActions != null)
        {
            InputActionMap actionMap = inputActions.FindActionMap(actionMapName, false);
            moveAction = actionMap?.FindAction(moveActionName, false);
        }

        if (moveAction != null)
        {
            return;
        }

        // Fallback so the controller still works without manual asset wiring.
        moveAction = new InputAction("CameraMove", InputActionType.Value);
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        ownsAction = true;
    }

    private void OnDrawGizmosSelected()
    {
        if (!clampToBounds)
        {
            return;
        }

        Vector3 center = new Vector3((minX + maxX) * 0.5f, transform.position.y, (minZ + maxZ) * 0.5f);
        Vector3 size = new Vector3(Mathf.Abs(maxX - minX), 0.1f, Mathf.Abs(maxZ - minZ));

        Gizmos.color = new Color(0f, 1f, 0.2f, 0.35f);
        Gizmos.DrawCube(center, size);
        Gizmos.color = new Color(0f, 1f, 0.2f, 1f);
        Gizmos.DrawWireCube(center, size);
    }

    private void OnValidate()
    {
        if (minX > maxX)
        {
            (minX, maxX) = (maxX, minX);
        }

        if (minZ > maxZ)
        {
            (minZ, maxZ) = (maxZ, minZ);
        }

        if (smoothness < 0f)
        {
            smoothness = 0f;
        }
    }
}
