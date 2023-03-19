using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.UI;

namespace EasyRobotics
{
    public static class Lib
    {
        public static readonly WaitForFixedUpdate WaitForFixedUpdate = new WaitForFixedUpdate();

        public static readonly WaitForEndOfFrame WaitForEndOfFrame = new WaitForEndOfFrame();

        public static Func<S, T> CreateGetter<S, T>(FieldInfo field)
        {
            string methodName = field.ReflectedType.FullName + ".get_" + field.Name;
            DynamicMethod setterMethod = new DynamicMethod(methodName, typeof(T), new Type[1] { typeof(S) }, true);
            ILGenerator gen = setterMethod.GetILGenerator();
            if (field.IsStatic)
            {
                gen.Emit(OpCodes.Ldsfld, field);
            }
            else
            {
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldfld, field);
            }
            gen.Emit(OpCodes.Ret);
            return (Func<S, T>)setterMethod.CreateDelegate(typeof(Func<S, T>));
        }

        public static Action<S, T> CreateSetter<S, T>(FieldInfo field)
        {
            string methodName = field.ReflectedType.FullName + ".set_" + field.Name;
            DynamicMethod setterMethod = new DynamicMethod(methodName, null, new Type[2] { typeof(S), typeof(T) }, true);
            ILGenerator gen = setterMethod.GetILGenerator();
            if (field.IsStatic)
            {
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Stsfld, field);
            }
            else
            {
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Stfld, field);
            }
            gen.Emit(OpCodes.Ret);
            return (Action<S, T>)setterMethod.CreateDelegate(typeof(Action<S, T>));
        }

        public static void SetGUIActive(this BaseField baseField, bool enabled)
        {
            baseField._guiActive = enabled;
            baseField._guiActiveEditor = enabled;
        }

        public static void SetGUIActive(this BaseEvent baseEvent, bool enabled)
        {
            baseEvent.guiActive = enabled;
            baseEvent.guiActiveEditor = enabled;
        }

        /// <summary>
        /// Force a PartActionWindow layout refresh.
        /// This can be necessary to avoid overlapping items or blank spaces after toggling KSPField/KSPEvent controls visibility
        /// </summary>
        /// <param name="part">The part to be refreshed</param>
        /// <param name="layoutOnly">true to only rebuild the visual layout, false to trigger a full PAW rebuild</param>
        public static void RefreshPAWLayout(this Part part, bool layoutOnly = true)
        {
            if (part.PartActionWindow.IsNullOrDestroyed() || !part.PartActionWindow.isActiveAndEnabled)
                return;

            if (layoutOnly)
                part.StartCoroutine(new RebuildPAWLayoutCoroutine(part.PartActionWindow));
            else
                part.PartActionWindow.displayDirty = true;
        }

        private struct RebuildPAWLayoutCoroutine : IEnumerator
        {
            private UIPartActionWindow _paw;
            private bool _mustWait;

            public RebuildPAWLayoutCoroutine(UIPartActionWindow paw)
            {
                _paw = paw;
                _mustWait = true;
            }

            public bool MoveNext()
            {
                if (_mustWait)
                {
                    _mustWait = false;
                    return true;
                }

                if (!_paw.IsDestroyed() && _paw.isActiveAndEnabled)
                    LayoutRebuilder.MarkLayoutForRebuild(_paw.itemsContentTransform);

                return false;
            }

            public void Reset()
            {
                throw new NotImplementedException();
            }

            public object Current => WaitForEndOfFrame;
        }

        public static float FromToRange(this float value, float fromMin, float fromMax, float toMin, float toMax)
        {
            return (value - fromMin) * (toMax - toMin) / (fromMax - fromMin) + toMin;
        }

        public static double FromToRange(this double value, double fromMin, double fromMax, double toMin, double toMax)
        {
            return (value - fromMin) * (toMax - toMin) / (fromMax - fromMin) + toMin;
        }

        public static bool IsDistanceLower(Vector3 from, Vector3 to, float lowerThan)
        {
            return (from - to).sqrMagnitude < lowerThan * lowerThan;
        }

        public static bool IsDistanceLowerOrEqual(Vector3 from, Vector3 to, float lowerThan)
        {
            return (from - to).sqrMagnitude <= lowerThan * lowerThan;
        }

        public static bool IsDistanceHigher(Vector3 from, Vector3 to, float lowerThan)
        {
            return (from - to).sqrMagnitude > lowerThan * lowerThan;
        }

        public static bool IsDistanceHigherOrEqual(Vector3 from, Vector3 to, float lowerThan)
        {
            return (from - to).sqrMagnitude >= lowerThan * lowerThan;
        }

        public static double SqrDistance(Vector3 v1, Vector3 v2)
        {
            double x = v1.x - v2.x;
            double y = v1.y - v2.y;
            double z = v1.z - v2.z;
            return x * x + y * y + z * z;
        }

        public static double Distance(Vector3 v1, Vector3 v2)
        {
            double x = v1.x - v2.x;
            double y = v1.y - v2.y;
            double z = v1.z - v2.z;
            return Math.Sqrt(x * x + y * y + z * z);
        }

        /// <summary>
        /// For a given set of options, enumerate all possible combinations of these options for the given count
        /// </summary>
        public static IEnumerable<T[]> Combinations<T>(T[] options, int count)
        {
            int[] index = new int[count];
            T[] current = new T[count];

            while (true)
            {
                for (int i = 0; i < count; i++)
                    current[i] = options[index[i]];

                yield return current;

                for (int i = count - 1; ; i--)
                {
                    if (i < 0)
                        yield break;

                    index[i]++;
                    if (index[i] == options.Length)
                        index[i] = 0;
                    else
                        break;
                }
            }
        }
    }
}