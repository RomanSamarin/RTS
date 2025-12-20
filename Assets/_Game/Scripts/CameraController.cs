using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 10f;
    public float fastMultiplier = 2f;

    [Header("Rotation Settings")]
    public float rotationSpeed = 100f;

    [Header("Zoom Settings")]
    public float zoomSpeed = 500f;
    public float minZoom = 5f;
    public float maxZoom = 100f;

    private float currentZoom = 30f;
    private float yaw = 0f;
    private float pitch = 45f;

    void Start()
    {
        currentZoom = transform.position.y;
    }

    void Update()
    {
        HandleMovement();
        HandleRotation();
        HandleZoom();
    }

    private void HandleMovement()
    {
        float hor = Input.GetAxis("Horizontal");
        float ver = Input.GetAxis("Vertical");

        float multiplier = Input.GetKey(KeyCode.LeftShift) ? fastMultiplier : 1f;
        Vector3 move = new Vector3(hor, 0f, ver) * moveSpeed * Time.deltaTime * multiplier;
        transform.Translate(move, Space.Self);
    }

    private void HandleRotation()
    {
        if (Input.GetMouseButton(2)) // Средняя кнопка мыши
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            yaw += mouseX * rotationSpeed * Time.deltaTime;
            pitch -= mouseY * rotationSpeed * Time.deltaTime;
            pitch = Mathf.Clamp(pitch, 10f, 80f);

            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        if (Input.GetKey(KeyCode.Q))
            transform.Rotate(Vector3.up, -rotationSpeed * Time.deltaTime, Space.World);
        if (Input.GetKey(KeyCode.E))
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
    }

    private void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        currentZoom -= scroll * zoomSpeed * Time.deltaTime;
        currentZoom = Mathf.Clamp(currentZoom, minZoom, maxZoom);

        Vector3 pos = transform.position;
        pos.y = currentZoom;
        transform.position = pos;
    }
}