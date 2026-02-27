# ReflectionHelper.cs
Null-safe reflection utilities. All methods return sensible defaults on failure and never throw. The central helper used by every other *Reflection.cs file.

## class ReflectionHelper (line 7)

### Methods
**Initialization**
- public static void `InitCache(string name, Action<Assembly> action)` (line 20)
  Finds the game assembly by name and runs the caching action. Logs warnings on failure.

**Field accessors** (all null-safe, return default on failure)
- public static object `GetField(FieldInfo field, object instance)` (line 42)
- public static bool `GetBool(FieldInfo field, object instance)` (line 47)
- public static int `GetInt(FieldInfo field, object instance)` (line 52)
- public static float `GetFloat(FieldInfo field, object instance)` (line 57)
- public static string `GetString(FieldInfo field, object instance)` (line 62)
- public static T `GetEnum<T>(FieldInfo field, object instance)` (line 67)
- public static void `SetField(FieldInfo field, object instance, object value)` (line 78)

**Property accessors** (all null-safe, return default on failure)
- public static object `GetProp(PropertyInfo prop, object instance)` (line 84)
- public static bool `GetPropBool(PropertyInfo prop, object instance)` (line 89)
- public static int `GetPropInt(PropertyInfo prop, object instance)` (line 94)
- public static float `GetPropFloat(PropertyInfo prop, object instance)` (line 99)
- public static string `GetPropString(PropertyInfo prop, object instance)` (line 104)

**Method invocation** (all null-safe, return default on failure)
- public static object `Invoke(MethodInfo method, object instance)` (line 115)
- public static object `Invoke(MethodInfo method, object instance, object arg1)` (line 120)
- public static object `Invoke(MethodInfo method, object instance, object arg1, object arg2)` (line 125)
- public static object `Invoke(MethodInfo method, object instance, object arg1, object arg2, object arg3)` (line 130)
- public static bool `InvokeBool(MethodInfo method, object instance)` (line 137)
- public static bool `InvokeBool(MethodInfo method, object instance, object arg1)` (line 143)
- public static bool `InvokeBool(MethodInfo method, object instance, object arg1, object arg2)` (line 149)
- public static int `InvokeInt(MethodInfo method, object instance)` (line 154)
- public static int `InvokeInt(MethodInfo method, object instance, object arg1)` (line 159)
- public static float `InvokeFloat(MethodInfo method, object instance)` (line 166)
- public static float `InvokeFloat(MethodInfo method, object instance, object arg1)` (line 172)
- public static string `InvokeString(MethodInfo method, object instance)` (line 178)
- public static string `InvokeString(MethodInfo method, object instance, object arg1)` (line 183)
- public static bool `InvokeVoid(MethodInfo method, object instance)` (line 190)
  Returns false if method is null, true on success.
- public static bool `InvokeVoid(MethodInfo method, object instance, object arg1)` (line 196)
- public static bool `InvokeVoid(MethodInfo method, object instance, object arg1, object arg2)` (line 202)
- public static bool `InvokeVoid(MethodInfo method, object instance, object arg1, object arg2, object arg3)` (line 208)

**Collection utilities**
- public static IList `GetList(FieldInfo field, object instance)` (line 217)
- public static IList `GetList(PropertyInfo prop, object instance)` (line 222)
- public static IEnumerable `IterateKeys(object dict)` (line 231)
  Iterates the keys of any dictionary via reflection. Use instead of casting to Dictionary<K,V>.
- public static int `DictGetInt(object dict, object key)` (line 242)
- public static object `DictGet(object dict, object key)` (line 249)

**Localization**
- public static string `GetLocaString(FieldInfo field, object instance)` (line 265)
  Combines `GetField` + `GameReflection.GetLocaText` to read a LocaText field as a string.
