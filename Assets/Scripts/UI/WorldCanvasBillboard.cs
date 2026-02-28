using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
public class WorldCanvasBillboard : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool keepVertical = true;

    private Canvas cachedCanvas;

    private void Awake()
    {
        cachedCanvas = GetComponent<Canvas>();

        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    private void OnEnable()
    {
        FaceCamera();
    }

    private void LateUpdate()
    {
        FaceCamera();
    }

    private void FaceCamera()
    {
        if (cachedCanvas == null || cachedCanvas.renderMode != RenderMode.WorldSpace)
        {
            return;
        }

        Camera cameraToUse = targetCamera != null ? targetCamera : Camera.main;
        if (cameraToUse == null)
        {
            return;
        }

        Vector3 forward = cameraToUse.transform.forward;
        if (keepVertical)
        {
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                return;
            }
        }

        transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
    }
}
