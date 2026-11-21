using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class ValleyEnvironmentSetup
{
    private const string Folder = "Assets/_Game/Materials/Effects/";

    [MenuItem("Tools/RTS/Apply Valley Water and Sky")]
    public static void Apply()
    {
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/URP/URPAsset_Renderer.asset");
        if (renderer != null)
        {
            var settings = new SerializedObject(renderer);
            settings.FindProperty("m_CopyDepthMode").intValue = 0;
            settings.ApplyModifiedProperties();
        }
        var sky = AssetDatabase.LoadAssetAtPath<Material>(Folder + "ValleySky.mat");
        if (sky == null)
        {
            sky = new Material(Shader.Find("Lords of the Valley/Cloud Sky"));
            AssetDatabase.CreateAsset(sky, Folder + "ValleySky.mat");
        }
        var atmosphere = UnityEngine.Object.FindObjectOfType<ValleyAtmosphere>();
        if (atmosphere == null)
        {
            var root = new GameObject("Valley Atmosphere");
            Undo.RegisterCreatedObjectUndo(root, "Add atmosphere");
            atmosphere = root.AddComponent<ValleyAtmosphere>();
        }
        Undo.RecordObject(atmosphere, "Set atmosphere");
        atmosphere.sky = sky;
        atmosphere.Apply();
        foreach (var camera in UnityEngine.Object.FindObjectsOfType<Camera>())
        {
            Undo.RecordObject(camera, "Enable atmospheric sky");
            camera.clearFlags = CameraClearFlags.Skybox;
            var data = camera.GetUniversalAdditionalCameraData();
            Undo.RecordObject(data, "Enable water depth and refraction");
            data.requiresDepthTexture = true;
            data.requiresColorTexture = true;
        }

        // This slope fits the existing rectangular prototype water at the west of LordsGame.
        var water = GameObject.Find("Cube (1)");
        if (water != null && water.GetComponent<Renderer>() != null &&
            water.GetComponent<Renderer>().sharedMaterial != null &&
            water.GetComponent<Renderer>().sharedMaterial.shader.name == "Lords of the Valley/Water" &&
            GameObject.Find("Valley Seabed") == null)
        {
            var sand = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            sand.SetColor("_BaseColor", new Color(0.64f, 0.59f, 0.41f));
            AssetDatabase.CreateAsset(sand, Folder + "ValleySand.mat");
            var mesh = new Mesh { name = "Sloping river bed" };
            const int columns = 32, rows = 32;
            var vertices = new Vector3[(columns + 1) * (rows + 1)];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[columns * rows * 6];
            for (int z = 0; z <= rows; z++)
            for (int x = 0; x <= columns; x++)
            {
                float worldX = Mathf.Lerp(53.18f, 103.8f, x / (float)columns);
                float worldZ = Mathf.Lerp(183.82f, 233.82f, z / (float)rows);
                float depth = 0.08f + Mathf.Pow((103.8f - worldX) / 50.62f, 0.85f) * 7;
                vertices[z * (columns + 1) + x] = new Vector3(worldX, -4 - depth - Mathf.PerlinNoise(worldX * 0.23f, worldZ * 0.23f) * 0.25f, worldZ);
                uv[z * (columns + 1) + x] = new Vector2(worldX, worldZ);
                if (x == columns || z == rows) continue;
                int i = z * (columns + 1) + x, t = (z * columns + x) * 6;
                triangles[t] = i; triangles[t+1] = i + columns + 1; triangles[t+2] = i + 1;
                triangles[t+3] = i + 1; triangles[t+4] = i + columns + 1; triangles[t+5] = i + columns + 2;
            }
            mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangles;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, Folder + "ValleySeabed.asset");
            var bed = new GameObject("Valley Seabed", typeof(MeshFilter), typeof(MeshRenderer));
            Undo.RegisterCreatedObjectUndo(bed, "Add seabed");
            bed.GetComponent<MeshFilter>().sharedMesh = mesh;
            bed.GetComponent<MeshRenderer>().sharedMaterial = sand;
            var material = water.GetComponent<Renderer>().sharedMaterial;
            Undo.RecordObject(material, "Use physical seabed");
            material.SetVector("_EastBank", new Vector4(103.8f, 0, 0.62f, 0));
            EditorUtility.SetDirty(material);
        }
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
    }

    public static void InstallAndVerify()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/LordsGame.unity");
        Apply();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        CapturePreview();
    }

    public static void CapturePreview()
    {
        var preview = new GameObject("Environment Preview Camera");
        var camera = preview.AddComponent<Camera>();
        camera.CopyFrom(Camera.main);
        camera.GetUniversalAdditionalCameraData().requiresDepthTexture = true;
        camera.GetUniversalAdditionalCameraData().requiresColorTexture = true;
        camera.transform.position = new Vector3(77, 35, 162);
        camera.transform.LookAt(new Vector3(108, -4, 197));
        Render(camera, "Library/ValleyEnvironment.png");
        camera.transform.position = new Vector3(100, 0, 195);
        camera.transform.rotation = Quaternion.Euler(-12, 35, 0);
        Render(camera, "Library/ValleySky.png");
        UnityEngine.Object.DestroyImmediate(preview);
    }

    private static void Render(Camera camera, string path)
    {
        var target = new RenderTexture(1280, 800, 24);
        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
        RenderTexture.active = target;
        var image = new Texture2D(1280, 800, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG());
        RenderTexture.active = null;
        UnityEngine.Object.DestroyImmediate(image);
        target.Release(); UnityEngine.Object.DestroyImmediate(target);
        foreach (string name in new[] { "Lords of the Valley/Water", "Lords of the Valley/Cloud Sky" })
        {
            var shader = Shader.Find(name);
            foreach (var message in ShaderUtil.GetShaderMessages(shader)) Debug.Log(message.message);
            if (ShaderUtil.ShaderHasError(shader)) throw new Exception(name + " failed compilation");
        }
        Debug.Log("Environment verified " + path);
    }
}
