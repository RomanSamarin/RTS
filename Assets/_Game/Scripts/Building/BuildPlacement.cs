using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(AudioSource))]
public class BuildPlacement : MonoBehaviour
{
    [Header("Raycast")]
    [SerializeField] private LayerMask placementLayer;
    [SerializeField] private float rayDistance = 1000f;

    [Header("Movement")]
    [SerializeField] private float followSpeed = 15f;
    [SerializeField] private float heightOffset = 0.05f;

    [Header("Rotation")]
    [SerializeField] private float rotationStep = 90f;

    [Header("Placement Validation")]
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private Material validMaterial;
    [SerializeField] private Material invalidMaterial;

    [Header("Sound")]
    [SerializeField] private AudioClip placeSound;
    [SerializeField] private AudioClip errorSound;

    private Camera mainCamera;
    private Collider col;
    private MeshRenderer meshRenderer;
    private AudioSource audioSource;
    private BuildSpawner spawner;

    private bool isPlaced;
    private bool canPlace;
    private Material originalMaterial;

    private void Awake()
    {
        mainCamera = Camera.main;
        col = GetComponent<Collider>();
        meshRenderer = GetComponentInChildren<MeshRenderer>();
        audioSource = GetComponent<AudioSource>();
        spawner = FindObjectOfType<BuildSpawner>();

        originalMaterial = meshRenderer.material;
        col.isTrigger = true;
    }

    private void Update()
    {
        if (isPlaced) return;

        FollowMouse();
        HandleRotation();
        ValidatePlacement();
        HandleInput();
    }

    private void FollowMouse()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, rayDistance, placementLayer))
        {
            Vector3 targetPos = hit.point + Vector3.up * heightOffset;
            transform.position = Vector3.Lerp(
                transform.position,
                targetPos,
                followSpeed * Time.deltaTime
            );
        }
    }

    private void HandleRotation()
    {
        if (Input.GetKeyDown(KeyCode.R))
            transform.Rotate(Vector3.up, rotationStep);
    }

    private void ValidatePlacement()
    {
        Bounds bounds = col.bounds;

        Collider[] hits = Physics.OverlapBox(
            bounds.center,
            bounds.extents,
            transform.rotation,
            obstacleLayer
        );

        canPlace = hits.Length == 0;
        meshRenderer.material = canPlace ? validMaterial : invalidMaterial;
    }

    private void HandleInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (canPlace)
                PlaceObject();
            else
                PlayErrorSound();
        }

        if (Input.GetMouseButtonDown(1))
            CancelPlacement();
    }

    private void PlaceObject()
    {
        isPlaced = true;
        col.isTrigger = false;
        meshRenderer.material = originalMaterial;

        if (placeSound)
            audioSource.PlayOneShot(placeSound);

        spawner?.OnBuildPlaced();
        Destroy(this);
    }

    private void CancelPlacement()
    {
        spawner?.OnBuildCanceled();
        Destroy(gameObject);
    }

    private void PlayErrorSound()
    {
        if (errorSound)
            audioSource.PlayOneShot(errorSound);
    }
}