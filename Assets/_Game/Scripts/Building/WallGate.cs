using UnityEngine;
using UnityEngine.AI;

public class WallGate : WallSegment
{
    [Header("Ворота")]
    public bool startsOpen;
    [Min(0.1f)] public float animationDuration = 1.2f;
    public Transform leftLeaf, rightLeaf;
    public Animator gateAnimator;
    public string openParameter = "isOpen";
    public Material frameMaterial;
    [Range(10, 120)] public float openAngle = 90;
    private GateMotion motion;
    private Quaternion leftClosed, rightClosed;
    public bool IsOpen => motion != null && motion.Passable;
    public string Status => motion == null ? "Закрыты" : IsOpen ? "Открыты" : motion.WantsOpen ? "Открываются" : motion.Progress > 0 ? "Закрываются" : "Закрыты";
    public string LastError { get; private set; } = "";

    protected override void Awake()
    {
        base.Awake();
        if (leftLeaf == null && rightLeaf == null) CreateDefaultVisual();
        if (leftLeaf != null) leftClosed = leftLeaf.localRotation;
        if (rightLeaf != null) rightClosed = rightLeaf.localRotation;
        motion = new GateMotion(startsOpen); SetAnimator(); Apply();
    }
    protected override void OnEnable() { base.OnEnable(); if (motion != null) Apply(); }
    protected override void Configure() { base.Configure(); if (motion != null) Apply(); }
    public void Open() { SetOpen(true); }
    public void Close() { SetOpen(false); }
    public void Toggle() { SetOpen(motion == null || !motion.WantsOpen); }
    public bool SetOpen(bool open)
    {
        if (motion == null || !isActiveAndEnabled) return false;
        if (!open && HasUnitsInPassage())
        { LastError = "Нельзя закрыть ворота: в проезде находится юнит."; Debug.LogWarning("[Ворота] " + LastError, this); return false; }
        LastError = ""; motion.Request(open); SetAnimator(); Apply(); return true;
    }
    private void SetAnimator()
    {
        if (gateAnimator == null || gateAnimator.runtimeAnimatorController == null) return;
        foreach (var parameter in gateAnimator.parameters)
            if (parameter.type == AnimatorControllerParameterType.Bool && parameter.name == openParameter)
                gateAnimator.SetBool(openParameter, motion.WantsOpen);
    }
    private void Update()
    {
        if(SaveGameManager.IsLoading)return;
        if (motion == null) return;
        // Если юнит успел войти до обновления carving, прерываем закрытие.
        if (!motion.WantsOpen && motion.Progress > 0 && HasUnitsInPassage())
        { motion.Request(true); SetAnimator(); }
        motion.Tick(Time.deltaTime, animationDuration); Apply();
    }
    private void Apply()
    {
        bool blocked = !motion.Passable;
        var box = GetComponent<BoxCollider>(); if (box != null) box.enabled = blocked;
        var obstacle = GetComponent<NavMeshObstacle>(); if (obstacle != null) obstacle.enabled = blocked;
        if (gateAnimator != null && gateAnimator.runtimeAnimatorController != null) return;
        float t = Mathf.SmoothStep(0, 1, motion.Progress);
        if (leftLeaf != null) leftLeaf.localRotation = leftClosed * Quaternion.Euler(0, -openAngle * t, 0);
        if (rightLeaf != null) rightLeaf.localRotation = rightClosed * Quaternion.Euler(0, openAngle * t, 0);
    }
    private bool HasUnitsInPassage()
    {
        foreach (var agent in FindObjectsOfType<NavMeshAgent>())
        {
            var unit = agent.GetComponent<Unit>();
            if (unit == null || unit.unitHealth <= 0) continue;
            Vector3 local = transform.InverseTransformPoint(agent.transform.position);
            if (Mathf.Abs(local.x) <= length * 0.4f + agent.radius && Mathf.Abs(local.z) <= thickness * 0.6f + agent.radius
                && local.y < height - 0.7f && local.y + agent.height > 0) return true;
        }
        return false;
    }
    public static bool TryToggleAt(Ray ray)
    {
        if (!Physics.Raycast(ray, out var hit, Mathf.Infinity, ~0, QueryTriggerInteraction.Ignore)) return false;
        var gate = hit.collider.GetComponentInParent<WallGate>();
        if (gate == null || !gate.isActiveAndEnabled||!FactionMember.IsPlayer(gate.gameObject)) return false;
        gate.Toggle(); return true;
    }
    private void CreateDefaultVisual()
    {
        float post = Mathf.Min(0.7f, length * 0.15f), beam = 0.7f;
        Box("LeftPost", new Vector3(-length * 0.5f + post * 0.5f, height * 0.5f, 0), new Vector3(post, height, thickness), transform, true);
        Box("RightPost", new Vector3(length * 0.5f - post * 0.5f, height * 0.5f, 0), new Vector3(post, height, thickness), transform, true);
        Box("TopBeam", new Vector3(0, height - beam * 0.5f, 0), new Vector3(length, beam, thickness), transform, false);
        float half = (length - post * 2) * 0.5f;
        leftLeaf = new GameObject("LeftHinge").transform; leftLeaf.SetParent(transform, false);
        leftLeaf.localPosition = new Vector3(-half, 0, 0);
        rightLeaf = new GameObject("RightHinge").transform; rightLeaf.SetParent(transform, false);
        rightLeaf.localPosition = new Vector3(half, 0, 0);
        Box("LeftLeaf", new Vector3(half * 0.5f, (height - beam) * 0.5f, 0), new Vector3(half, height - beam, 0.25f), leftLeaf, false, false);
        Box("RightLeaf", new Vector3(-half * 0.5f, (height - beam) * 0.5f, 0), new Vector3(half, height - beam, 0.25f), rightLeaf, false, false);
    }
    private void Box(string label, Vector3 position, Vector3 size, Transform parent, bool carve, bool solid = true)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name = label; obj.layer = gameObject.layer;
        obj.transform.SetParent(parent, false); obj.transform.localPosition = position; obj.transform.localScale = size;
        if (frameMaterial != null) obj.GetComponent<Renderer>().sharedMaterial = frameMaterial;
        if (!solid) { obj.GetComponent<Collider>().enabled = false; Destroy(obj.GetComponent<Collider>()); }
        if (!carve) return;
        var obstacle = obj.AddComponent<NavMeshObstacle>(); obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.size = new Vector3(1, Mathf.Max(0.1f, 1 - 0.5f / height), 1);
        obstacle.center = Vector3.down * (0.25f / height); obstacle.carving = true;
    }
}
