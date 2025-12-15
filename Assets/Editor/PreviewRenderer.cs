using UnityEngine;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.Linq;
using Unity.VisualScripting;
using TMPro;

namespace AtlasAuto
{
    internal class WireframeParentReference : MonoBehaviour
    {
        public GameObject parent;
    }

    public class PreviewRenderer
    {
        const float sensitivity = 0.3f;
        RenderTexture rt;
        Scene previewScene;

        GameObject rendering;
        Bounds renderingBounds;

        UnityEngine.Camera camera;


        float yaw;
        float pitch;
        float distance;

        struct Materials
        {
            public Material objectOutline;
            public Material wheelOutline;
            public Material hoverOutline;
        }

        Materials materials;

        List<GameObject> cosmetics = new List<GameObject>();

        Dictionary<string, GameObject> carParts;

        float maxDistance;

        public void InitRenderer(int size = 512)
        {
            previewScene = EditorSceneManager.NewPreviewScene();

            camera = new GameObject("RenderCam", typeof(UnityEngine.Camera)).GetComponent<UnityEngine.Camera>();
            camera.fieldOfView = 75f;
            camera.backgroundColor = Color.blue;
            camera.transform.position = new Vector3(0, 1, -3);
            camera.transform.LookAt(Vector3.zero);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.scene = previewScene;

            rt = new RenderTexture(size, size, 26, RenderTextureFormat.ARGB32);
            camera.targetTexture = rt;

            materials.objectOutline = makeMaterial(Color.black);
            materials.wheelOutline = makeMaterial(Color.green);
            materials.hoverOutline = makeMaterial(Color.red);

            SceneManager.MoveGameObjectToScene(camera.gameObject, previewScene);
        }

        Material makeMaterial(Color col)
        {
            var mat = new Material(Shader.Find("Hidden/Internal-Colored"));
            mat.hideFlags = HideFlags.HideAndDontSave;
            mat.SetColor("_Color", col);
            return mat;
        }

        public void AddRenderObject(GameObject obj, Dictionary<string, GameObject> carParts)
        {
            this.carParts = carParts;
            if (rendering != null)
            {
                Object.DestroyImmediate(rendering);
            }

            GameObject clone = Object.Instantiate(obj);
            clone.GetComponentsInChildren<Collider>().ToList().ForEach(Collider.DestroyImmediate);
            rendering = clone;
            SceneManager.MoveGameObjectToScene(clone, previewScene);

            Bounds bounds = CalculateBounds(clone);
            float radius = bounds.extents.magnitude;

            distance = radius / Mathf.Sin(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            maxDistance = distance * 1.2f;
            yaw = 0;
            pitch = 0;

            Vector3 position = bounds.center - Vector3.back * (distance / 1.2f);

            camera.transform.position = position;
            camera.transform.LookAt(bounds.center);
            renderingBounds = bounds;
        }

        public void ApplyCosmetics(GameObject exclude = null)
        {
            RemoveCosmetics(exclude);
            ApplyWireframeTo(rendering, materials.objectOutline);

            foreach (var key in carParts.Keys)
            {
                var obj = carParts[key];
                if (obj == exclude) continue;
                if (key.Contains("Wheel"))
                {
                    ApplyWireframeTo(obj, materials.wheelOutline, true);
                }
            }
        }

        public void RemoveCosmetics(GameObject exclude = null)
        {
            cosmetics.ForEach(o => { if (o != exclude) Object.DestroyImmediate(o); });
        }


        public void PositionCameraBy(Vector2 pos)
        {
            if (rendering == null) return;


            yaw += pos.x * sensitivity;
            pitch -= pos.y * sensitivity;
            pitch = Mathf.Clamp(pitch, -80f, 80f);

            UpdateCameraFromAngles();
        }

        public void Zoom(float level)
        {
            distance += level * sensitivity;
            distance = Mathf.Clamp(distance, 2, maxDistance);
            UpdateCameraFromAngles();
        }

        void UpdateCameraFromAngles()
        {
            Vector3 target = renderingBounds.center;

            float yawRad = yaw * Mathf.Deg2Rad;
            float pitchRad = pitch * Mathf.Deg2Rad;

            float cosPitch = Mathf.Cos(pitchRad);

            Vector3 dir = new Vector3(
                cosPitch * Mathf.Sin(yawRad),
                Mathf.Sin(pitchRad),
                cosPitch * Mathf.Cos(yawRad)
            );

            camera.transform.position = target - dir * distance;
            camera.transform.LookAt(target);
        }


        Bounds CalculateBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();

            Bounds b = new Bounds(go.transform.position, Vector3.zero);
            foreach (var r in renderers) b.Encapsulate(r.bounds);

            return b;
        }

        public void Clean()
        {
            EditorSceneManager.CloseScene(previewScene, true);
        }

        public RenderTexture RenderFrame()
        {
            camera.Render();
            return rt;
        }

        public void CheckForHover(Vector2 pos)
        {
            var ray = camera.ViewportPointToRay(pos);

            RaycastHit hit;
            var raycast = previewScene.GetPhysicsScene().Raycast(ray.origin, ray.direction, out hit);

            if (raycast)
            {
                var hitElement = hit.collider.gameObject;

                ApplyCosmetics(hitElement);
                if (carParts.ContainsValue(hitElement.GetComponent<WireframeParentReference>().parent))
                {
                    var newMat = new Material(materials.hoverOutline);
                    hitElement.GetComponent<MeshRenderer>().sharedMaterial = newMat;
                }
            }
        }

        Mesh CreateBoundsLineMesh(Bounds bounds)
        {
            Mesh m = new Mesh();
            m.name = "Wireframe";

            Vector3 c = bounds.center;
            Vector3 e = bounds.extents;

            Vector3[] v = new Vector3[8]
            {
                c + new Vector3(-e.x, -e.y, -e.z), // 0
                c + new Vector3( e.x, -e.y, -e.z), // 1
                c + new Vector3( e.x, -e.y,  e.z), // 2
                c + new Vector3(-e.x, -e.y,  e.z), // 3
                c + new Vector3(-e.x,  e.y, -e.z), // 4
                c + new Vector3( e.x,  e.y, -e.z), // 5
                c + new Vector3( e.x,  e.y,  e.z), // 6
                c + new Vector3(-e.x,  e.y,  e.z) // 7
            };

            int[] lines = new int[]
            {
                0,1, 1,2, 2,3, 3,0,
                4,5, 5,6, 6,7, 7,4,
                0,4, 1,5, 2,6, 3,7
            };

            m.SetVertices(v);
            m.SetIndices(lines, MeshTopology.Lines, 0);
            m.RecalculateBounds();
            return m;
        }

        void ApplyWireframeTo(GameObject o, Material mat, bool canHover = false)
        {
            Bounds bounds = CalculateBounds(o);
            Mesh wireframeMesh = CreateBoundsLineMesh(bounds);

            GameObject wireframe = new GameObject(o.name + "_AWF");
            wireframe.AddComponent<MeshFilter>().mesh = wireframeMesh;
            wireframe.AddComponent<MeshRenderer>().sharedMaterial = mat;
            wireframe.AddComponent<WireframeParentReference>().parent = o;

            if (canHover)
            {
                BoxCollider collider = wireframe.AddComponent<BoxCollider>();
                collider.size = bounds.size;
                collider.center = bounds.center;
            }

            SceneManager.MoveGameObjectToScene(wireframe, previewScene);
            cosmetics.Add(wireframe);
        }
    }
}
