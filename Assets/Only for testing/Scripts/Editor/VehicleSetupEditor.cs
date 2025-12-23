using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Editor extension for automatically setting up wheel colliders on a vehicle.
/// Usage: Tag your wheel meshes with "Wheel", select the vehicle root, and click "Setup Vehicle Wheels".
/// </summary>
[CustomEditor(typeof(VehicleController))]
public class VehicleSetupEditor : Editor
{
    public enum SuspensionPreset
    {
        Soft,       // Comfortable, more body roll
        Medium,     // Balanced
        Stiff,      // Sporty, less body roll
        RaceCar,    // Very stiff, minimal travel
        OffRoad     // Long travel, soft
    }

    private SuspensionPreset selectedPreset = SuspensionPreset.Medium;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        VehicleController controller = (VehicleController)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Vehicle Setup Wizard", EditorStyles.boldLabel);

        // Suspension preset selector
        EditorGUILayout.BeginHorizontal();
        selectedPreset = (SuspensionPreset)EditorGUILayout.EnumPopup("Suspension Preset", selectedPreset);
        if (GUILayout.Button("Apply", GUILayout.Width(60)))
        {
            ApplySuspensionPreset(controller, selectedPreset);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        if (GUILayout.Button("Setup Vehicle Wheels"))
        {
            SetupWheels(controller);
        }

        if (GUILayout.Button("Clear Wheel Setup"))
        {
            ClearWheelSetup(controller);
        }
    }

    private void ApplySuspensionPreset(VehicleController controller, SuspensionPreset preset)
    {
        WheelCollider[] wheelColliders = controller.GetComponentsInChildren<WheelCollider>();
        if (wheelColliders.Length == 0)
        {
            Debug.LogWarning("[VehicleSetup] No WheelColliders found. Run 'Setup Vehicle Wheels' first.");
            return;
        }

        // Get preset values
        float spring, damper, suspensionDistance;
        float targetPosition = 0.5f;

        switch (preset)
        {
            case SuspensionPreset.Soft:
                spring = 20000f;
                damper = 2500f;
                suspensionDistance = 0.25f;
                break;
            case SuspensionPreset.Medium:
                spring = 35000f;
                damper = 4000f;
                suspensionDistance = 0.2f;
                break;
            case SuspensionPreset.Stiff:
                spring = 55000f;
                damper = 5500f;
                suspensionDistance = 0.12f;
                break;
            case SuspensionPreset.RaceCar:
                spring = 80000f;
                damper = 7000f;
                suspensionDistance = 0.08f;
                targetPosition = 0.4f;
                break;
            case SuspensionPreset.OffRoad:
                spring = 25000f;
                damper = 3000f;
                suspensionDistance = 0.35f;
                targetPosition = 0.6f;
                break;
            default:
                spring = 35000f;
                damper = 4000f;
                suspensionDistance = 0.2f;
                break;
        }

        foreach (var wc in wheelColliders)
        {
            Undo.RecordObject(wc, "Apply Suspension Preset");
            
            wc.suspensionDistance = suspensionDistance;
            
            JointSpring springSettings = wc.suspensionSpring;
            springSettings.spring = spring;
            springSettings.damper = damper;
            springSettings.targetPosition = targetPosition;
            wc.suspensionSpring = springSettings;

            EditorUtility.SetDirty(wc);
        }

        Debug.Log($"[VehicleSetup] Applied '{preset}' suspension preset to {wheelColliders.Length} wheels.");
    }

    private void SetupWheels(VehicleController controller)
    {
        // Find all children tagged "Wheel"
        Transform[] allChildren = controller.GetComponentsInChildren<Transform>();
        List<Transform> wheelMeshes = new List<Transform>();

        foreach (var child in allChildren)
        {
            if (child.CompareTag("Wheel"))
            {
                wheelMeshes.Add(child);
            }
        }

        if (wheelMeshes.Count == 0)
        {
            Debug.LogWarning("[VehicleSetup] No objects with tag 'Wheel' found. Please tag your wheel meshes.");
            return;
        }

        // Create or find the WheelColliders parent
        Transform collidersParent = controller.transform.Find("WheelColliders");
        if (collidersParent == null)
        {
            GameObject parentGO = new GameObject("WheelColliders");
            parentGO.transform.SetParent(controller.transform);
            parentGO.transform.localPosition = Vector3.zero;
            parentGO.transform.localRotation = Quaternion.identity;
            collidersParent = parentGO.transform;
            Undo.RegisterCreatedObjectUndo(parentGO, "Create WheelColliders Parent");
        }

        List<VehicleWheel> createdWheels = new List<VehicleWheel>();

        foreach (var wheelMesh in wheelMeshes)
        {
            // Add temporary SphereCollider to get radius
            SphereCollider tempSphere = wheelMesh.gameObject.AddComponent<SphereCollider>();
            float wheelRadius = tempSphere.radius * GetMaxScale(wheelMesh);
            Vector3 wheelCenter = wheelMesh.TransformPoint(tempSphere.center);
            DestroyImmediate(tempSphere);

            // Create the WheelCollider GameObject
            string wcName = wheelMesh.name + "_Collider";
            Transform existingWC = collidersParent.Find(wcName);
            GameObject wcGO;

            if (existingWC != null)
            {
                wcGO = existingWC.gameObject;
            }
            else
            {
                wcGO = new GameObject(wcName);
                wcGO.transform.SetParent(collidersParent);
                Undo.RegisterCreatedObjectUndo(wcGO, "Create WheelCollider");
            }

            wcGO.transform.position = wheelCenter;
            wcGO.transform.rotation = controller.transform.rotation;

            WheelCollider wc = wcGO.GetComponent<WheelCollider>();
            if (wc == null)
            {
                wc = wcGO.AddComponent<WheelCollider>();
            }

            // Configure WheelCollider with Medium preset defaults
            wc.radius = wheelRadius;
            wc.suspensionDistance = 0.2f;
            wc.mass = 20f;

            JointSpring spring = wc.suspensionSpring;
            spring.spring = 35000f;
            spring.damper = 4000f;
            spring.targetPosition = 0.5f;
            wc.suspensionSpring = spring;

            WheelFrictionCurve fwdFriction = wc.forwardFriction;
            fwdFriction.stiffness = 1.5f;
            wc.forwardFriction = fwdFriction;

            WheelFrictionCurve sideFriction = wc.sidewaysFriction;
            sideFriction.stiffness = 1.5f;
            wc.sidewaysFriction = sideFriction;

            VehicleWheel vw = wcGO.GetComponent<VehicleWheel>();
            if (vw == null)
            {
                vw = wcGO.AddComponent<VehicleWheel>();
            }
            vw.wheelCollider = wc;
            vw.wheelVisual = wheelMesh;

            float localZ = controller.transform.InverseTransformPoint(wheelCenter).z;
            vw.isMotor = localZ < 0;
            vw.isSteer = localZ >= 0;

            createdWheels.Add(vw);
            EditorUtility.SetDirty(wcGO);
        }

        SetupAntiRollBars(controller, createdWheels);

        Debug.Log($"[VehicleSetup] Successfully configured {createdWheels.Count} wheels with 'Medium' suspension.");
        EditorUtility.SetDirty(controller.gameObject);
    }

    private void SetupAntiRollBars(VehicleController controller, List<VehicleWheel> wheels)
    {
        List<VehicleWheel> frontWheels = new List<VehicleWheel>();
        List<VehicleWheel> rearWheels = new List<VehicleWheel>();

        foreach (var w in wheels)
        {
            if (w.isSteer) frontWheels.Add(w);
            else rearWheels.Add(w);
        }

        if (frontWheels.Count >= 2)
        {
            AntiRollBar frontARB = controller.GetComponent<AntiRollBar>();
            if (frontARB == null)
            {
                frontARB = controller.gameObject.AddComponent<AntiRollBar>();
            }
            frontWheels.Sort((a, b) => 
                controller.transform.InverseTransformPoint(a.transform.position).x.CompareTo(
                controller.transform.InverseTransformPoint(b.transform.position).x));
            
            frontARB.wheelL = frontWheels[0].wheelCollider;
            frontARB.wheelR = frontWheels[frontWheels.Count - 1].wheelCollider;
            frontARB.antiRollForce = 8000f;
            EditorUtility.SetDirty(frontARB);
        }

        if (rearWheels.Count >= 2)
        {
            AntiRollBar[] existingARBs = controller.GetComponents<AntiRollBar>();
            AntiRollBar rearARB = existingARBs.Length > 1 ? existingARBs[1] : null;
            if (rearARB == null)
            {
                rearARB = controller.gameObject.AddComponent<AntiRollBar>();
            }
            rearWheels.Sort((a, b) =>
                controller.transform.InverseTransformPoint(a.transform.position).x.CompareTo(
                controller.transform.InverseTransformPoint(b.transform.position).x));
            
            rearARB.wheelL = rearWheels[0].wheelCollider;
            rearARB.wheelR = rearWheels[rearWheels.Count - 1].wheelCollider;
            rearARB.antiRollForce = 6000f;
            EditorUtility.SetDirty(rearARB);
        }

        Debug.Log("[VehicleSetup] Anti-roll bars configured.");
    }

    private void ClearWheelSetup(VehicleController controller)
    {
        Transform collidersParent = controller.transform.Find("WheelColliders");
        if (collidersParent != null)
        {
            Undo.DestroyObjectImmediate(collidersParent.gameObject);
            Debug.Log("[VehicleSetup] Wheel setup cleared.");
        }
        else
        {
            Debug.Log("[VehicleSetup] No WheelColliders parent found to clear.");
        }
    }

    private float GetMaxScale(Transform t)
    {
        Vector3 scale = t.lossyScale;
        return Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
    }
}
