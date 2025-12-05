using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class VehicleBehavourValidator
{
    static VehicleBehavourValidator()
    {
        EditorApplication.delayCall += ValidateVehicleBehaviours;
    }

    static void ValidateVehicleBehaviours()
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var type in asm.GetTypes())
            {
                if (!typeof(VehicleBehaviour).IsAssignableFrom(type)) continue;
                if (type == typeof(VehicleBehaviour)) continue;

                var hasStart =
                    type.GetMethod("Start", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    != null;

                var hasAwake =
                    type.GetMethod("Awake", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    != null;

                if (hasStart || hasAwake)
                {
                    var v = AssetDatabase.LoadAssetAtPath<MonoScript>(ScriptPath(type));
                    Debug.LogError(
                        $"'{type.FullName}' is lowk cooked! Don't implement your own Start or Awake and use the built in overrides!",
                        v
                    );
                    GameObject.DestroyImmediate(v, true);
                }
            }
        }
    }

    static string ScriptPath(Type type)
    {
        var scripts = Resources.FindObjectsOfTypeAll<MonoScript>();
        foreach (var script in scripts)
        {
            if (script != null && script.GetClass() == type)
                return AssetDatabase.GetAssetPath(script);
        }
        return null;
    }
}
