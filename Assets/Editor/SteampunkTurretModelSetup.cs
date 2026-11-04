using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Survive.Items;
using Survive.Turrets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace Survive.Editor
{
    public static class SteampunkTurretModelSetup
    {
        public const string ModelPath = "Assets/Models/Turrets/Steampunk/SteampunkTurret.fbx";
        public const string MaterialPath = "Assets/Materials/Vendor/SteampunkTurret.mat";
        public const string Folder = "Logs/SteampunkTurret";
        const string RigFolder = "Assets/Models/Turrets/Steampunk/Rig";
        const float YawHeight = .43f, PitchCut = .82f;
        // El FBX está centrado incluyendo los cañones; el eje real de su rodamiento queda 24 cm atrás.
        const float BaseCenterOffset = .2396196f;
        static readonly Vector3 PitchPivot = new Vector3(0, 1.115f, -.30f + BaseCenterOffset);

        [MenuItem("Survive/Vendedor/Integrar torreta Steampunk")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Salir de Play Mode.");
            Directory.CreateDirectory(Folder); PrepareAssets(); BuildMeshes();
            TurretDefinition definition = AssetDatabase.LoadAssetAtPath<TurretDefinition>(VendorTurretSetup.DefinitionPath);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(definition.DeployedPrefab, out string guid, out long id);
            CreateTurret(definition);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(definition.DeployedPrefab, out string afterGuid, out long afterId);
            if (guid != afterGuid || id != afterId) throw new InvalidOperationException("Se alteró la referencia original de la torreta.");
            CreateItem(definition); AssetDatabase.SaveAssets(); RefreshPresentation();
            File.WriteAllText(Folder + "/integration.txt", "PASS: FBX y textura importados con material URP.\nPASS: base fija, giro horizontal, elevación de cañones y dos bocas alternadas.\nPASS: modelo reemplazado en torreta, preview de colocación y pickup; icono del mercader regenerado.\nPASS: referencia original conservada: " + guid + " / " + id + ".\n");
            Debug.Log("STEAMPUNK_TURRET_SETUP_PASS");
        }

        public static void RefreshPresentation()
        {
            Directory.CreateDirectory(Folder); VendorTurretSetup.CreateTurretIcon();
            Scene scene = EditorSceneManager.NewPreviewScene(); var root = new GameObject("SteampunkTurretPreview"); SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                var turret = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(VendorTurretSetup.TurretPath), root.transform);
                Render(root, scene, new Vector3(2.5f, 1.8f, 3), "05-integrated-turret.png");
                turret.transform.Find("RotatingHead").localRotation = Quaternion.Euler(0, 65, 0);
                turret.transform.Find("RotatingHead/BarrelPitch").localRotation = Quaternion.Euler(-15, 0, 0);
                Render(root, scene, new Vector3(2.5f, 1.8f, 3), "06-aiming-rig.png");
            }
            finally { Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
        }
        public static void RefreshAndValidate() { RefreshPresentation(); SteampunkTurretValidation.Run(); }

        public static void CreateTurret(TurretDefinition definition)
        {
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(VendorTurretSetup.TurretPath) != null;
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(VendorTurretSetup.TurretPath) : new GameObject("AutoTurret");
            try
            {
                AutoTurret turret = root.GetComponent<AutoTurret>(); if (turret == null) turret = root.AddComponent<AutoTurret>();
                LineRenderer line = root.GetComponentInChildren<LineRenderer>();
                foreach (Transform child in root.transform.Cast<Transform>().ToArray()) if (line == null || child.gameObject != line.gameObject) Object.DestroyImmediate(child.gameObject);
                if (line == null) { line = new GameObject("Tracer").AddComponent<LineRenderer>(); line.transform.SetParent(root.transform, false); }
                line.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Vendor/TurretTracer.mat");
                line.useWorldSpace = true; line.positionCount = 2; line.widthMultiplier = .025f; line.enabled = false;
                Mesh baseMesh = MeshAsset("Base"), yawMesh = MeshAsset("Yaw"), gunMesh = MeshAsset("Guns");
                RenderPart(root.transform, "SteampunkBase", baseMesh, false);
                var head = new GameObject("RotatingHead").transform; head.SetParent(root.transform, false); head.localPosition = Vector3.up * YawHeight;
                RenderPart(head, "YawMechanism", yawMesh, true);
                var pitch = new GameObject("BarrelPitch").transform; pitch.SetParent(head, false); pitch.localPosition = PitchPivot - Vector3.up * YawHeight;
                RenderPart(pitch, "TwinGatlingVisual", gunMesh, true);
                var left = new GameObject("MuzzleLeft").transform; left.SetParent(pitch, false); left.localPosition = new Vector3(-.383f, 1.115f, .775f + BaseCenterOffset) - PitchPivot;
                var right = new GameObject("MuzzleRight").transform; right.SetParent(pitch, false); right.localPosition = new Vector3(.383f, 1.115f, .775f + BaseCenterOffset) - PitchPivot;
                BoxCollider box = root.GetComponent<BoxCollider>(); if (box == null) box = root.AddComponent<BoxCollider>(); box.center = baseMesh.bounds.center; box.size = baseMesh.bounds.size;
                NavMeshObstacle obstacle = root.GetComponent<NavMeshObstacle>(); if (obstacle == null) obstacle = root.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Capsule; obstacle.center = new Vector3(0, .7f, 0); obstacle.height = 1.4f;
                obstacle.radius = Mathf.Max(.65f, new Vector2(baseMesh.bounds.extents.x, baseMesh.bounds.extents.z).magnitude); obstacle.carving = true;
                AudioSource audio = root.GetComponent<AudioSource>(); if (audio == null) audio = root.AddComponent<AudioSource>();
                audio.playOnAwake = false; audio.spatialBlend = 1; audio.minDistance = 2; audio.maxDistance = 35;
                VendorTurretSetup.Set(turret, "definition", definition, "head", head, "barrelPitch", pitch, "muzzle", left, "alternateMuzzle", right,
                    "tracer", line, "shotAudio", audio, "presentation", AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Data/Weapons/WD_MachineGun.asset"));
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, VendorTurretSetup.TurretPath);
                // Barrido completo de los cañones: la colocación debe reservar más espacio que la base sola.
                VendorTurretSetup.Set(definition, "deployedPrefab", prefab.GetComponent<AutoTurret>(), "footprintRadius", 1.2f, "height", 1.4f);
            }
            finally { if (exists) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root); }
        }

        public static void CreateItem(TurretDefinition definition)
        {
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(VendorTurretSetup.ItemPath) != null;
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(VendorTurretSetup.ItemPath) : new GameObject("TurretItem");
            try
            {
                if (root.GetComponent<TurretItem>() == null) root.AddComponent<TurretItem>();
                foreach (Transform child in root.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                AddModel(root.transform, .5f); VendorTurretSetup.Set(root.GetComponent<TurretItem>(), "definition", definition);
                PrefabUtility.SaveAsPrefabAsset(root, VendorTurretSetup.ItemPath);
            }
            finally { if (exists) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root); }
        }

        static void RenderPart(Transform parent, string name, Mesh mesh, bool collider)
        {
            var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); part.transform.SetParent(parent, false);
            part.GetComponent<MeshFilter>().sharedMesh = mesh; part.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (collider) { var box = part.AddComponent<BoxCollider>(); box.center = mesh.bounds.center; box.size = mesh.bounds.size; }
        }
        static Mesh MeshAsset(string name) => AssetDatabase.LoadAssetAtPath<Mesh>(RigFolder + "/Steampunk" + name + ".asset");

        struct Vertex
        {
            public Vector3 position, normal; public Vector2 uv;
            public static Vertex Lerp(Vertex a, Vertex b, float t) => new Vertex { position = Vector3.Lerp(a.position, b.position, t), normal = Vector3.Lerp(a.normal, b.normal, t).normalized, uv = Vector2.Lerp(a.uv, b.uv, t) };
        }
        sealed class PartMesh
        {
            public readonly List<Vector3> positions = new List<Vector3>(), normals = new List<Vector3>();
            public readonly List<Vector2> uvs = new List<Vector2>(); public readonly List<int> indices = new List<int>();
            public void Add(List<Vertex> polygon, Vector3 pivot)
            {
                for (int i = 1; i + 1 < polygon.Count; i++)
                {
                    Vertex[] tri = { polygon[0], polygon[i], polygon[i + 1] };
                    if (Vector3.Cross(tri[1].position - tri[0].position, tri[2].position - tri[0].position).sqrMagnitude < 1e-14f) continue;
                    foreach (Vertex v in tri) { indices.Add(positions.Count); positions.Add(v.position - pivot); normals.Add(v.normal); uvs.Add(v.uv); }
                }
            }
            public void Save(string name)
            {
                var mesh = new Mesh { name = "Steampunk" + name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.SetVertices(positions); mesh.SetNormals(normals); mesh.SetUVs(0, uvs); mesh.SetTriangles(indices, 0); mesh.RecalculateBounds(); mesh.RecalculateTangents();
                string path = RigFolder + "/Steampunk" + name + ".asset"; Mesh previous = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (previous == null) AssetDatabase.CreateAsset(mesh, path); else { EditorUtility.CopySerialized(mesh, previous); previous.UploadMeshData(false); EditorUtility.SetDirty(previous); Object.DestroyImmediate(mesh); }
            }
        }
        static List<Vertex> Clip(List<Vertex> input, float height, bool above)
        {
            var result = new List<Vertex>(); if (input.Count == 0) return result;
            Vertex previous = input[input.Count - 1]; float prior = (previous.position.y - height) * (above ? 1 : -1);
            foreach (Vertex current in input)
            {
                float distance = (current.position.y - height) * (above ? 1 : -1);
                if ((distance >= 0) != (prior >= 0)) result.Add(Vertex.Lerp(previous, current, prior / (prior - distance)));
                if (distance >= 0) result.Add(current); previous = current; prior = distance;
            }
            return result;
        }
        public static void BuildMeshes()
        {
            if (!AssetDatabase.IsValidFolder(RigFolder)) AssetDatabase.CreateFolder("Assets/Models/Turrets/Steampunk", "Rig");
            Scene scene = EditorSceneManager.NewPreviewScene(); var root = new GameObject("SteampunkMeshBake"); SceneManager.MoveGameObjectToScene(root, scene);
            var basis = new PartMesh(); var yaw = new PartMesh(); var guns = new PartMesh();
            try
            {
                GameObject model = AddModel(root.transform, 1.4f);
                foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>())
                {
                    Mesh mesh = filter.sharedMesh; Vector3[] vertices = mesh.vertices, normals = mesh.normals; Vector2[] uvs = mesh.uv; int[] triangles = mesh.triangles;
                    Matrix4x4 matrix = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    Matrix4x4 normalMatrix = matrix.inverse.transpose;
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        var triangle = new List<Vertex>(3);
                        for (int k = 0; k < 3; k++) { int index = triangles[i + k]; triangle.Add(new Vertex { position = matrix.MultiplyPoint3x4(vertices[index]) + Vector3.forward * BaseCenterOffset, normal = normalMatrix.MultiplyVector(normals[index]).normalized, uv = uvs[index] }); }
                        basis.Add(Clip(triangle, YawHeight, false), Vector3.zero);
                        yaw.Add(Clip(Clip(triangle, YawHeight, true), PitchCut, false), Vector3.up * YawHeight);
                        guns.Add(Clip(triangle, PitchCut, true), PitchPivot);
                    }
                }
                basis.Save("Base"); yaw.Save("Yaw"); guns.Save("Guns"); AssetDatabase.SaveAssets();
                File.WriteAllText(Folder + "/rig-meshes.txt", $"Base triangles={basis.indices.Count / 3}\nYaw triangles={yaw.indices.Count / 3}\nGuns triangles={guns.indices.Count / 3}\n");
            }
            finally { Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
        }
        public static void PrepareAssets()
        {
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            importer.importAnimation = false; importer.importCameras = false; importer.importLights = false;
            importer.importTangents = ModelImporterTangents.CalculateMikk; importer.isReadable = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard; importer.SaveAndReimport();
            const string texturePath = "Assets/Models/Turrets/Steampunk/Textures/Albedo.jpg";
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
            var texture = (TextureImporter)AssetImporter.GetAtPath(texturePath); texture.sRGBTexture = true; texture.maxTextureSize = 2048; texture.SaveAndReimport();
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, MaterialPath); }
            material.SetColor("_BaseColor", Color.white); material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetFloat("_Metallic", .35f); material.SetFloat("_Smoothness", .32f); EditorUtility.SetDirty(material); AssetDatabase.SaveAssets();
            foreach (Material source in AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Material>())
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), source.name), material);
            importer.SaveAndReimport();
        }
        public static GameObject AddModel(Transform parent, float height)
        {
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath), parent);
            model.name = "SteampunkTurretModel"; model.transform.localPosition = Vector3.zero;
            Bounds bounds = BoundsOf(model); model.transform.localScale *= height / bounds.size.y;
            bounds = BoundsOf(model);
            model.transform.localPosition -= parent.InverseTransformVector(new Vector3(bounds.center.x - parent.position.x, bounds.min.y - parent.position.y, bounds.center.z - parent.position.z));
            return model;
        }
        public static Bounds BoundsOf(GameObject model)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(); if (renderers.Length == 0) throw new InvalidOperationException("Modelo sin renderers");
            Bounds bounds = renderers[0].bounds; foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds); return bounds;
        }
        public static void Inspect()
        {
            Directory.CreateDirectory(Folder); PrepareAssets(); Scene scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("TurretInspection"); SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                GameObject model = AddModel(root.transform, 1.4f); var report = new StringBuilder();
                report.AppendLine("Root bounds: " + BoundsOf(model));
                foreach (Transform t in model.GetComponentsInChildren<Transform>()) report.AppendLine("Transform " + t.name + " pos=" + t.position + " rot=" + t.rotation.eulerAngles + " scale=" + t.lossyScale);
                foreach (MeshFilter f in model.GetComponentsInChildren<MeshFilter>()) report.AppendLine("Mesh " + f.name + " vertices=" + f.sharedMesh.vertexCount + " triangles=" + f.sharedMesh.triangles.Length / 3 + " bounds=" + f.sharedMesh.bounds);
                File.WriteAllText(Folder + "/model-inspection.txt", report.ToString());
                Render(root, scene, new Vector3(2.5f, 1.8f, 3), "01-model-oblique.png");
                Render(root, scene, new Vector3(0, .8f, 4), "02-model-front-positive-z.png");
                Render(root, scene, new Vector3(4, .8f, 0), "03-model-side-positive-x.png");
                Render(root, scene, new Vector3(0, .8f, -4), "04-model-back-negative-z.png");
            }
            finally { Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
            Debug.Log("STEAMPUNK_INSPECTION_PASS");
        }
        static void Render(GameObject root, Scene scene, Vector3 position, string file)
        {
            Camera camera = new GameObject("Camera").AddComponent<Camera>(); camera.transform.SetParent(root.transform);
            camera.scene = scene; camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.14f, .17f, .21f);
            camera.orthographic = true; camera.orthographicSize = 1.05f; camera.nearClipPlane = .01f; camera.farClipPlane = 30;
            camera.transform.position = position; camera.transform.LookAt(new Vector3(0, .7f, 0));
            Light light = new GameObject("Light").AddComponent<Light>(); light.transform.SetParent(root.transform); light.type = LightType.Directional; light.intensity = 2; light.transform.rotation = Quaternion.Euler(35, -25, 0);
            var rt = new RenderTexture(768, 768, 24); rt.Create(); camera.targetTexture = rt;
            RenderTexture previous = RenderTexture.active; var photo = new Texture2D(768, 768, TextureFormat.RGB24, false);
            try { camera.Render(); RenderTexture.active = rt; photo.ReadPixels(new Rect(0, 0, 768, 768), 0, 0); photo.Apply(); File.WriteAllBytes(Folder + "/" + file, photo.EncodeToPNG()); }
            finally { RenderTexture.active = previous; rt.Release(); Object.DestroyImmediate(photo); Object.DestroyImmediate(rt); Object.DestroyImmediate(camera.gameObject); Object.DestroyImmediate(light.gameObject); }
        }
    }
}
