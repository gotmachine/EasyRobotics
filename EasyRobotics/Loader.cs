using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace EasyRobotics
{
    public static class Assets
    {
        public static GameObject RotationGizmoPrefab { get; private set; }
        public static GameObject TargetGizmoPrefab { get; private set; }
        public static int BurnColorID { get; private set; }

        public static void ModuleManagerPostLoad()
        {
            string dllPath = Assembly.GetExecutingAssembly().Location;
            string rightPath = Path.Combine("GameData", "EasyRobotics", "Plugins", "EasyRobotics.dll");

            if (!dllPath.EndsWith(rightPath))
            {
                Debug.LogError($"[EasyRobotics] Incorrect install path : {dllPath}." +
                               $"\nInstall path should end with {rightPath}");
                return;
            }

            BurnColorID = Shader.PropertyToID("_BurnColor");
            RotationGizmoPrefab = GetModelPrefab("RotationServoGizmo");
            TargetGizmoPrefab = GetModelPrefab("TargetGizmo");
        }

        private static GameObject GetModelPrefab(string name)
        {
            return GameDatabase.Instance.GetModelPrefab($"EasyRobotics/Models/{name}");
        }
    }
}
