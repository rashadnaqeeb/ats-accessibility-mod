using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility.Reflection {
	/// <summary>
	/// Null-safe reflection utilities. Never throws, returns sensible defaults on failure.
	/// Replaces repetitive try/catch/log patterns across *Reflection.cs files.
	/// </summary>
	public static class ReflectionHelper {
		// ========================================
		// INIT CACHE
		// ========================================

		/// <summary>
		/// Wrapper for the EnsureCached body. Gets the game assembly, runs cacheAction,
		/// logs success or failure. Returns false if assembly was null.
		/// </summary>
		public static bool InitCache(string className, Action<Assembly> cacheAction) {
			try {
				var assembly = GameReflection.GameAssembly;
				if (assembly == null) {
					Debug.LogWarning($"[ATSAccessibility] {className}: Game assembly not available");
					return false;
				}

				cacheAction(assembly);

				Debug.Log($"[ATSAccessibility] {className}: Types cached successfully");
				return true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] {className}: Failed to cache types: {ex.Message}");
				return false;
			}
		}

		// ========================================
		// FIELD ACCESSORS
		// ========================================

		public static object GetField(FieldInfo field, object instance) {
			if (field == null || instance == null) return null;
			try { return field.GetValue(instance); } catch { return null; }
		}

		public static bool GetBool(FieldInfo field, object instance) {
			if (field == null || instance == null) return false;
			try { return field.GetValue(instance) is bool b && b; } catch { return false; }
		}

		public static int GetInt(FieldInfo field, object instance) {
			if (field == null || instance == null) return 0;
			try { return field.GetValue(instance) is int i ? i : 0; } catch { return 0; }
		}

		public static float GetFloat(FieldInfo field, object instance) {
			if (field == null || instance == null) return 0f;
			try { return field.GetValue(instance) is float f ? f : 0f; } catch { return 0f; }
		}

		public static string GetString(FieldInfo field, object instance) {
			if (field == null || instance == null) return null;
			try { return field.GetValue(instance) as string; } catch { return null; }
		}

		public static int GetEnum(FieldInfo field, object instance) {
			if (field == null || instance == null) return 0;
			try {
				var val = field.GetValue(instance);
				return val != null ? Convert.ToInt32(val) : 0;
			} catch { return 0; }
		}

		public static bool SetField(FieldInfo field, object instance, object value) {
			if (field == null || instance == null) return false;
			try { field.SetValue(instance, value); return true; } catch { return false; }
		}

		// ========================================
		// PROPERTY ACCESSORS
		// ========================================

		public static object GetProp(PropertyInfo prop, object instance) {
			if (prop == null || instance == null) return null;
			try { return prop.GetValue(instance); } catch { return null; }
		}

		public static bool GetPropBool(PropertyInfo prop, object instance) {
			if (prop == null || instance == null) return false;
			try { return prop.GetValue(instance) is bool b && b; } catch { return false; }
		}

		public static int GetPropInt(PropertyInfo prop, object instance) {
			if (prop == null || instance == null) return 0;
			try { return prop.GetValue(instance) is int i ? i : 0; } catch { return 0; }
		}

		public static float GetPropFloat(PropertyInfo prop, object instance) {
			if (prop == null || instance == null) return 0f;
			try { return prop.GetValue(instance) is float f ? f : 0f; } catch { return 0f; }
		}

		public static string GetPropString(PropertyInfo prop, object instance) {
			if (prop == null || instance == null) return null;
			try { return prop.GetValue(instance) as string; } catch { return null; }
		}

		// ========================================
		// METHOD INVOCATION (explicit overloads, no params[])
		// ========================================

		// --- Invoke (returns object) ---

		public static object Invoke(MethodInfo method, object instance) {
			if (method == null || instance == null) return null;
			try { return method.Invoke(instance, null); } catch { return null; }
		}

		public static object Invoke(MethodInfo method, object instance, object arg0) {
			if (method == null || instance == null) return null;
			try { return method.Invoke(instance, new[] { arg0 }); } catch { return null; }
		}

		public static object Invoke(MethodInfo method, object instance, object arg0, object arg1) {
			if (method == null || instance == null) return null;
			try { return method.Invoke(instance, new[] { arg0, arg1 }); } catch { return null; }
		}

		public static object Invoke(MethodInfo method, object instance, object arg0, object arg1, object arg2) {
			if (method == null || instance == null) return null;
			try { return method.Invoke(instance, new[] { arg0, arg1, arg2 }); } catch { return null; }
		}

		// --- InvokeBool ---

		public static bool InvokeBool(MethodInfo method, object instance) {
			if (method == null || instance == null) return false;
			try { return method.Invoke(instance, null) is bool b && b; } catch { return false; }
		}

		public static bool InvokeBool(MethodInfo method, object instance, object arg0) {
			if (method == null || instance == null) return false;
			try { return method.Invoke(instance, new[] { arg0 }) is bool b && b; } catch { return false; }
		}

		public static bool InvokeBool(MethodInfo method, object instance, object arg0, object arg1) {
			if (method == null || instance == null) return false;
			try { return method.Invoke(instance, new[] { arg0, arg1 }) is bool b && b; } catch { return false; }
		}

		// --- InvokeInt ---

		public static int InvokeInt(MethodInfo method, object instance) {
			if (method == null || instance == null) return 0;
			try { return method.Invoke(instance, null) is int i ? i : 0; } catch { return 0; }
		}

		public static int InvokeInt(MethodInfo method, object instance, object arg0) {
			if (method == null || instance == null) return 0;
			try { return method.Invoke(instance, new[] { arg0 }) is int i ? i : 0; } catch { return 0; }
		}

		// --- InvokeFloat ---

		public static float InvokeFloat(MethodInfo method, object instance) {
			if (method == null || instance == null) return 0f;
			try { return method.Invoke(instance, null) is float f ? f : 0f; } catch { return 0f; }
		}

		public static float InvokeFloat(MethodInfo method, object instance, object arg0) {
			if (method == null || instance == null) return 0f;
			try { return method.Invoke(instance, new[] { arg0 }) is float f ? f : 0f; } catch { return 0f; }
		}

		// --- InvokeString ---

		public static string InvokeString(MethodInfo method, object instance) {
			if (method == null || instance == null) return null;
			try { return method.Invoke(instance, null) as string; } catch { return null; }
		}

		public static string InvokeString(MethodInfo method, object instance, object arg0) {
			if (method == null || instance == null) return null;
			try { return method.Invoke(instance, new[] { arg0 }) as string; } catch { return null; }
		}

		// --- InvokeVoid (returns bool success) ---

		public static bool InvokeVoid(MethodInfo method, object instance) {
			if (method == null || instance == null) return false;
			try { method.Invoke(instance, null); return true; } catch (Exception ex) { LogInvokeError(method, ex); return false; }
		}

		public static bool InvokeVoid(MethodInfo method, object instance, object arg0) {
			if (method == null || instance == null) return false;
			try { method.Invoke(instance, new[] { arg0 }); return true; } catch (Exception ex) { LogInvokeError(method, ex); return false; }
		}

		public static bool InvokeVoid(MethodInfo method, object instance, object arg0, object arg1) {
			if (method == null || instance == null) return false;
			try { method.Invoke(instance, new[] { arg0, arg1 }); return true; } catch (Exception ex) { LogInvokeError(method, ex); return false; }
		}

		public static bool InvokeVoid(MethodInfo method, object instance, object arg0, object arg1, object arg2) {
			if (method == null || instance == null) return false;
			try { method.Invoke(instance, new[] { arg0, arg1, arg2 }); return true; } catch (Exception ex) { LogInvokeError(method, ex); return false; }
		}

		private static void LogInvokeError(MethodInfo method, Exception ex) {
			var inner = ex is TargetInvocationException tie ? tie.InnerException : ex;
			Debug.LogError($"[ATSAccessibility] InvokeVoid {method.DeclaringType?.Name}.{method.Name} failed: {inner?.Message ?? ex.Message}\n{inner?.StackTrace ?? ex.StackTrace}");
		}

		// ========================================
		// COLLECTION UTILITIES
		// ========================================

		/// <summary>
		/// Get a field or property value as IList.
		/// </summary>
		public static IList GetList(FieldInfo field, object instance) {
			if (field == null || instance == null) return null;
			try { return field.GetValue(instance) as IList; } catch { return null; }
		}

		public static IList GetList(PropertyInfo prop, object instance) {
			if (prop == null || instance == null) return null;
			try { return prop.GetValue(instance) as IList; } catch { return null; }
		}

		/// <summary>
		/// Iterate keys of a reflected dictionary. Uses reflection to avoid
		/// direct cast to Dictionary&lt;K,V&gt; which fails at runtime.
		/// </summary>
		public static IEnumerable IterateKeys(object dict) {
			if (dict == null) return null;
			try {
				var keysProp = dict.GetType().GetProperty("Keys");
				return keysProp?.GetValue(dict) as IEnumerable;
			} catch { return null; }
		}

		/// <summary>
		/// Get an int value from a reflected dictionary by key. Returns 0 if missing or wrong type.
		/// </summary>
		public static int DictGetInt(object dict, object key) {
			return DictGet(dict, key) is int i ? i : 0;
		}

		/// <summary>
		/// Get a value from a reflected dictionary by key.
		/// </summary>
		public static object DictGet(object dict, object key) {
			if (dict == null || key == null) return null;
			try {
				var indexer = dict.GetType().GetMethod("get_Item");
				return indexer?.Invoke(dict, new[] { key });
			} catch { return null; }
		}

		/// <summary>
		/// Set a value in a reflected dictionary by key, adding the key if absent.
		/// Returns false if the dictionary or its indexer was not found.
		/// </summary>
		public static bool DictSet(object dict, object key, object value) {
			if (dict == null || key == null) return false;
			try {
				var indexer = dict.GetType().GetMethod("set_Item");
				if (indexer == null) return false;
				indexer.Invoke(dict, new[] { key, value });
				return true;
			} catch { return false; }
		}

		// ========================================
		// LOCATEXT SHORTCUT
		// ========================================

		/// <summary>
		/// Get a LocaText field's value and extract the localized string.
		/// Combines GetField + GameReflection.GetLocaText into one call.
		/// </summary>
		public static string GetLocaString(FieldInfo locaTextField, object instance) {
			var locaText = GetField(locaTextField, instance);
			return GameReflection.GetLocaText(locaText);
		}
	}
}
