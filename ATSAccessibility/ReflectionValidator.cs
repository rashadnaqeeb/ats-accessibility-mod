using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Validates reflection caches by inspecting static fields for null values.
    /// Uses reflection-on-reflection to automatically find and check all cached metadata.
    /// </summary>
    public static class ReflectionValidator
    {
        private static readonly HashSet<Type> ReflectionTypes = new HashSet<Type>
        {
            typeof(PropertyInfo),
            typeof(MethodInfo),
            typeof(FieldInfo),
            typeof(Type),
            typeof(ConstructorInfo),
            typeof(Assembly)
        };

        /// <summary>
        /// Validates all private static reflection fields in the given class.
        /// Returns the count of null fields.
        /// </summary>
        public static int ValidateFields(Type reflectionClass, string className)
        {
            var fields = reflectionClass.GetFields(BindingFlags.NonPublic | BindingFlags.Static);
            int total = 0;
            int nullCount = 0;
            var nullFields = new List<string>();

            foreach (var field in fields)
            {
                if (!ReflectionTypes.Contains(field.FieldType))
                    continue;

                total++;
                var value = field.GetValue(null);
                if (value == null)
                {
                    nullCount++;
                    nullFields.Add(field.Name);
                }
            }

            if (nullCount == 0)
            {
                Debug.Log($"[ATSAccessibility] {className}: {total}/{total} fields cached OK");
            }
            else
            {
                Debug.Log($"[ATSAccessibility] {className}: {total - nullCount}/{total} fields cached, {nullCount} MISSING");
                foreach (var name in nullFields)
                {
                    Debug.Log($"[ATSAccessibility]   {className}.{name} is null");
                }
            }

            return nullCount;
        }

        /// <summary>
        /// Discovers and invokes all parameterless Ensure* methods on the class,
        /// then validates all reflection fields. Returns count of null fields.
        /// </summary>
        public static int TriggerAndValidate(Type reflectionClass, string className)
        {
            var methods = reflectionClass.GetMethods(BindingFlags.NonPublic | BindingFlags.Static);

            foreach (var method in methods)
            {
                if (!method.Name.StartsWith("Ensure"))
                    continue;
                if (method.GetParameters().Length != 0)
                    continue;

                try
                {
                    method.Invoke(null, null);
                }
                catch (Exception ex)
                {
                    Debug.Log($"[ATSAccessibility] {className}.{method.Name}() threw: {ex.InnerException?.Message ?? ex.Message}");
                }
            }

            return ValidateFields(reflectionClass, className);
        }
    }
}
