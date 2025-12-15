using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;

namespace AtlasAuto.Editor
{
    public class CreateVehicleScript
    {
        static string TEMPLATE =
    @"using UnityEngine;
using System.Collections.Generic;

public class #NAME : VehicleBehaviour
{
    // Custom start logic since regular Start cannot be used (seriously you'll get an error!)
    protected override void OnStart()
    {
        
    }

    // Regular Update runs every frame
    void Update()
    {
    
    }
}";

        [MenuItem("Assets/Create/AtlasAuto/Create Vehicle Behaviour")]
        public static void CreateVehicleButton()
        {
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(
                0,
                ScriptableObject.CreateInstance<OnEditEnd>(),
                "VehicleBehaviour.cs",
                EditorGUIUtility.IconContent("cs Script Icon").image as Texture2D,
                null
            );
        }

        class OnEditEnd : EndNameEditAction
        {
            public override void Action(int instanceId, string pathName, string resourceFile)
            {
                pathName = pathName.Replace(" ", "");
                string className = Path.GetFileNameWithoutExtension(pathName);
                string content = TEMPLATE.Replace("#NAME", className);

                File.WriteAllText(pathName, content);

                AssetDatabase.Refresh();
                var obj = AssetDatabase.LoadAssetAtPath<Object>(pathName);
                Selection.activeObject = obj;
            }
        }
    }
}