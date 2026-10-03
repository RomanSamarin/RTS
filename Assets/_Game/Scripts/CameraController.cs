using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 20f;
    public float fastMultiplier = 2.5f;

    [Header("Rotation Settings")]
    public float rotationSpeed = 120f;
    public float minPitch = 25f; 
    public float maxPitch = 60f; 

    [Header("Zoom Settings")]
    public float zoomSpeed = 10f;
    public float minZoomDist = 5f;  
    public float maxZoomDist = 60f; 

    [Header("Ground Prevention")]
    public float minHeightAboveGround = 2f; 

    private Transform cameraTransform; 
    private Vector3 targetPosition;
    private float targetZoom;
    private float targetYaw;

    void Start()
    {
        cameraTransform = GetComponentInChildren<Camera>().transform;

        if (cameraTransform == null)
        {
            Debug.LogError("Ошибка: Внутри пустого объекта нет Главной Камеры! Перетащите камеру внутрь него в Иерархии.");
            return;
        }

        targetPosition = transform.position;
        targetYaw = transform.eulerAngles.y;
        targetZoom = (minZoomDist + maxZoomDist) / 2f; 
    }

    void Update()
    {
        if (cameraTransform == null) return;

        HandleInput();
        ApplySmoothMovement();
    }

    private void HandleInput()
    {
        float hor = Input.GetAxisRaw("Horizontal");
        float ver = Input.GetAxisRaw("Vertical");

        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 right = transform.right;
        right.y = 0f;
        right.Normalize();

        float multiplier = Input.GetKey(KeyCode.LeftShift) ? fastMultiplier : 1f;
        Vector3 moveDirection = (forward * ver + right * hor).normalized;
        
        targetPosition += moveDirection * moveSpeed * multiplier * Time.deltaTime;

        if (Input.GetMouseButton(2)) 
        {
            float mouseX = Input.GetAxis("Mouse X");
            targetYaw += mouseX * rotationSpeed * 1.5f * Time.deltaTime;
        }

        if (Input.GetKey(KeyCode.Q)) targetYaw -= rotationSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.E)) targetYaw += rotationSpeed * Time.deltaTime;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            targetZoom -= scroll * zoomSpeed * 100f * Time.deltaTime;
            targetZoom = Mathf.Clamp(targetZoom, minZoomDist, maxZoomDist);
        }
    }

    private void ApplySmoothMovement()
    {
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 8f);
        
        float currentYaw = Mathf.LerpAngle(transform.eulerAngles.y, targetYaw, Time.deltaTime * 10f);
        transform.rotation = Quaternion.Euler(0f, currentYaw, 0f);

        float zoomProgress = (targetZoom - minZoomDist) / (maxZoomDist - minZoomDist);
        float targetPitch = Mathf.Lerp(minPitch, maxPitch, zoomProgress);
        
        Quaternion targetCameraRotation = Quaternion.Euler(targetPitch, 0f, 0f);
        cameraTransform.localRotation = Quaternion.Slerp(cameraTransform.localRotation, targetCameraRotation, Time.deltaTime * 10f);

        float targetHeight = targetZoom * Mathf.Sin(targetPitch * Mathf.Deg2Rad);
        float targetOffsetZ = -targetZoom * Mathf.Cos(targetPitch * Mathf.Deg2Rad);
        Vector3 targetCameraLocalPos = new Vector3(0f, targetHeight, targetOffsetZ);

        Vector3 potentialWorldPos = transform.TransformPoint(targetCameraLocalPos);

        float terrainHeightAtCamera = 0f;
        if (Terrain.activeTerrain != null)
        {
            terrainHeightAtCamera = Terrain.activeTerrain.SampleHeight(potentialWorldPos);
        }

        if (potentialWorldPos.y < terrainHeightAtCamera + minHeightAboveGround)
        {
            float requiredWorldY = terrainHeightAtCamera + minHeightAboveGround;
            targetCameraLocalPos.y = requiredWorldY - transform.position.y;
        }

        cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, targetCameraLocalPos, Time.deltaTime * 8f);
    }
}
