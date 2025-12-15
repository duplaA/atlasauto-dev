using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AtlasAuto.Compiler
{
    public static class CarCompilerManager
    {
        static string[] NECESSARY_PARTS = { "Wheel_fl", "Wheel_fr", "Wheel_rl", "Wheel_rr" };
        public static bool CheckIntegrity(GameObject obj, out int reason, out Dictionary<string, GameObject> parts)
        {

            Dictionary<string, GameObject> carParts = new();
            var transform = obj.transform;
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                var childObj = child.gameObject;

                if (NECESSARY_PARTS.Contains(child.name)) carParts.Add(child.name, childObj);
            }

            parts = carParts;
            var hasProperties = HasPropertParts(carParts);

            if (!hasProperties)
            {
                reason = 0;
                return false;
            }

            reason = 0;
            return true;
        }

        static bool HasPropertParts(Dictionary<string, GameObject> parts)
        {
            foreach (var part in NECESSARY_PARTS)
            {
                if (!parts.ContainsKey(part))
                {
                    return false;
                }
            }

            return true;
        }
        public static GameObject BakeModel(GameObject baseModel, Dictionary<string, GameObject> parts)
        {
            return null;
        }
    }

}