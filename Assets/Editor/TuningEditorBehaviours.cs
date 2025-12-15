using System;
using System.Reflection;
using UnityEngine;


namespace AtlasAuto.Editor
{

    public class TuningEditorBehaviours
    {
        public class EngineSettings
        {
            [EditorRange(0, 50)] public float testValue;
        }
        public class SpringAndDampingSettings
        {
            [EditorRange(25, 94)] public float testValue2;
        }

        [ExportToSidebar("Engine")] public EngineSettings engineSettings;
        [ExportToSidebar("Springs and Dampings")] public SpringAndDampingSettings springsAndShit;

        public TuningEditorBehaviours()
        {
            foreach (var f in GetType().GetFields())
            {
                if (f.GetCustomAttribute<ExportToSidebarAttribute>() != null)
                {
                    f.SetValue(this, Activator.CreateInstance(f.FieldType));
                }
            }
        }
    }


    [System.AttributeUsage(System.AttributeTargets.Field, Inherited = false, AllowMultiple = true)]
    internal class ExportToSidebarAttribute : System.Attribute
    {
        readonly string name;

        public ExportToSidebarAttribute(string name)
        {
            this.name = name;

        }

        public string Name
        {
            get { return name; }
        }
    }

    [System.AttributeUsage(System.AttributeTargets.Field, Inherited = false, AllowMultiple = true)]
    internal class EditorRangeAttribute : System.Attribute
    {
        readonly int min, max;

        public EditorRangeAttribute(int min, int max)
        {
            this.min = min;
            this.max = max;
        }

        public int Min => min;
        public int Max => max;
    }
}
