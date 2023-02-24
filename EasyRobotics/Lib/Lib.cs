using System;
using System.Collections;
using System.Reflection.Emit;
using System.Reflection;
using UnityEngine;
using System.Collections.Generic;
using static ProceduralSpaceObject;

namespace EasyRobotics
{
    public static class Lib
    {
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

        public static float FromToRange(this float value, float fromMin, float fromMax, float toMin, float toMax)
        {
            return (value - fromMin) * (toMax - toMin) / (fromMax - fromMin) + toMin;
        }

        public static double FromToRange(this double value, double fromMin, double fromMax, double toMin, double toMax)
        {
            return (value - fromMin) * (toMax - toMin) / (fromMax - fromMin) + toMin;
        }

        /// <summary>
        /// For a given set of options, return all possible combinations of these options for the given count
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