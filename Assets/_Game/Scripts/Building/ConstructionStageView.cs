using System;
using UnityEngine;

/// <summary>Отображает прогресс площадки без запуска логики готового здания.</summary>
public sealed class ConstructionStageView : MonoBehaviour
{
    [NonSerialized] private ConstructionSite site;
    [NonSerialized] private GameObject[] stages;
    [NonSerialized] private Material material;
    [NonSerialized] private int shown = -1;

    internal void Initialize(ConstructionSite target, GameObject building, Vector3 center, Vector2 size, float height, GameObject legacyFrame)
    {
        site = target;
        var settings = building.GetComponent<ConstructionAppearance>();
        var prefabs = new[]
        {
            settings != null ? settings.foundationPrefab : null,
            settings != null && settings.framePrefab != null ? settings.framePrefab : legacyFrame,
            settings != null ? settings.wallsPrefab : null,
            settings != null ? settings.roofPrefab : null
        };
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader != null) material = new Material(shader);
        stages = new GameObject[4];
        height = Mathf.Clamp(height, 1f, 20f);
        for (int i = 0; i < stages.Length; i++)
        {
            stages[i] = new GameObject("ConstructionStage_" + i);
            stages[i].transform.SetParent(transform, false);
            stages[i].SetActive(false);
            if (prefabs[i] != null) CopyMeshes(prefabs[i], stages[i].transform, false);
            else BuildStage(stages[i].transform, i, building, center, size, height);
        }
        var scaffolding = new GameObject("Scaffolding");
        scaffolding.transform.SetParent(transform, false);
        if (settings != null && settings.scaffoldingPrefab != null)
            CopyMeshes(settings.scaffoldingPrefab, scaffolding.transform, false);
        else
            BuildScaffolding(scaffolding.transform, center, size, height);
        LateUpdate();
    }

    private void BuildStage(Transform parent, int stage, GameObject building, Vector3 center, Vector2 size, float height)
    {
        var stone = new Color(.48f, .46f, .42f);
        var timber = new Color(.36f, .23f, .12f);
        float width = Mathf.Max(.5f, size.x), depth = Mathf.Max(.5f, size.y);
        if (stage == 3)
        {
            CopyMeshes(building, parent, true);
            return;
        }
        Box(parent, center + Vector3.up * .15f, new Vector3(width, .3f, depth), stone);
        if (stage == 0) return;
        float wallHeight = height * .65f;
        foreach (float x in new[] { -width * .46f, width * .46f })
            foreach (float z in new[] { -depth * .46f, depth * .46f })
                Box(parent, center + new Vector3(x, wallHeight * .5f, z), new Vector3(.15f, wallHeight, .15f), timber);
        foreach (float z in new[] { -depth * .46f, depth * .46f })
        {
            Box(parent, center + new Vector3(0, wallHeight, z), new Vector3(width, .15f, .15f), timber);
            if (stage == 2)
                Box(parent, center + new Vector3(0, wallHeight * .5f, z), new Vector3(width, wallHeight, .15f), stone);
        }
        foreach (float x in new[] { -width * .46f, width * .46f })
        {
            Box(parent, center + new Vector3(x, wallHeight, 0), new Vector3(.15f, .15f, depth), timber);
            if (stage == 2)
                Box(parent, center + new Vector3(x, wallHeight * .5f, 0), new Vector3(.15f, wallHeight, depth), stone);
        }
    }

    private void BuildScaffolding(Transform parent, Vector3 center, Vector2 size, float height)
    {
        var wood = new Color(.47f, .32f, .17f);
        float halfWidth = size.x * .5f + .45f;
        float halfDepth = size.y * .5f + .45f;
        float top = Mathf.Max(1f, height * .8f);
        foreach (float x in new[] { -halfWidth, halfWidth })
            foreach (float z in new[] { -halfDepth, halfDepth })
                Box(parent, center + new Vector3(x, top * .5f, z), new Vector3(.12f, top, .12f), wood);
        int levels = Mathf.Clamp(Mathf.CeilToInt(top / 1.8f), 1, 6);
        for (int level = 1; level <= levels; level++)
        {
            float y = top * level / levels;
            foreach (float z in new[] { -halfDepth, halfDepth })
                Box(parent, center + new Vector3(0, y, z), new Vector3(halfWidth * 2, .1f, .45f), wood);
            foreach (float x in new[] { -halfWidth, halfWidth })
                Box(parent, center + new Vector3(x, y, 0), new Vector3(.45f, .1f, halfDepth * 2), wood);
        }
    }

    private void Box(Transform parent, Vector3 position, Vector3 scale, Color color)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = position;
        obj.transform.localScale = scale;
        obj.layer = LayerMask.NameToLayer("Ignore Raycast");
        var collider = obj.GetComponent<Collider>();
        collider.enabled = false;
        Destroy(collider);
        var renderer = obj.GetComponent<MeshRenderer>();
        if (material != null) renderer.sharedMaterial = material;
        var properties = new MaterialPropertyBlock();
        properties.SetColor("_BaseColor", color);
        properties.SetColor("_Color", color);
        renderer.SetPropertyBlock(properties);
    }

    private void CopyMeshes(GameObject source, Transform parent, bool honorVisibility)
    {
        CopyNode(source.transform, parent, true, honorVisibility);
    }

    private void CopyNode(Transform source, Transform parent, bool root, bool honorVisibility)
    {
        if (!root && !source.gameObject.activeSelf) return;
        if (source.GetComponent<Canvas>() != null) return;
        var copy = new GameObject(source.name);
        copy.layer = LayerMask.NameToLayer("Ignore Raycast");
        copy.transform.SetParent(parent, false);
        copy.transform.localPosition = root ? Vector3.zero : source.localPosition;
        copy.transform.localRotation = root ? Quaternion.identity : source.localRotation;
        copy.transform.localScale = source.localScale;
        var original = source.GetComponent<MeshRenderer>();
        var mesh = source.GetComponent<MeshFilter>();
        if (original != null && mesh != null && original.enabled)
        {
            copy.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
            var renderer = copy.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = original.sharedMaterials;
            renderer.shadowCastingMode = original.shadowCastingMode;
            var properties = new MaterialPropertyBlock();
            original.GetPropertyBlock(properties);
            renderer.SetPropertyBlock(properties);
        }
        foreach (Transform child in source) CopyNode(child, copy.transform, false, honorVisibility);
    }

    private void LateUpdate()
    {
        if (site == null || stages == null || SaveGameManager.IsLoading) return;
        int index = Mathf.Min(3, Mathf.FloorToInt(Mathf.Clamp01(site.Progress01) * 4f));
        if (shown == index) return;
        for (int i = 0; i < stages.Length; i++) stages[i].SetActive(i == index);
        shown = index;
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
