#nullable enable
/*namespace EditorHelper {
    public static partial class ReflectionExtensions {
        public static partial T get<T>(this object obj, string fieldName);
        public static partial void set<T>(this object obj, string fieldName, T value);
        public static partial T invoke<T>(this object obj, string methodName, params object[] args);
        public static partial void invoke(this object obj, string methodName, params object[] args);
        public static partial T invoke<T>(this object obj, string methodName, object[] argTypes, params object[] args);
        public static partial void invoke(this object obj, string methodName, object[] argTypes, params object[] args);
    }
}*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace EditorPlus {
    /// <summary>
    /// Class for getting/setting/invoking using <see cref="System.Reflection"/>.
    /// </summary>
    public static class ReflectionExtensions {
        /// <summary>
        /// A <see langword="delegate"/> that has <see langword="params"/> <see langword="object"/>[] parameter.
        /// </summary>
        /// <typeparam name="T">Return type of the delegate.</typeparam>
        public delegate T InvokableMethod<out T>(params object[] args);

        private static Dictionary<Type, Dictionary<string, FieldInfo>> fields =
            new Dictionary<Type, Dictionary<string, FieldInfo>>();

        private static Dictionary<Type, Dictionary<string, PropertyInfo>> properties =
            new Dictionary<Type, Dictionary<string, PropertyInfo>>();

        private static Dictionary<Type, Dictionary<string, IEnumerable<(Type[], MethodInfo)>>> methods =
            new Dictionary<Type, Dictionary<string, IEnumerable<(Type[], MethodInfo)>>>();

        [Flags]
        private enum PropertyType {
            None,
            Get,
            Set,
            GetSet = Get | Set
        }

        private static bool CheckField(Type type, string member) {
            var field = type.GetField(member, AccessTools.all);
            if (field != null) {
                fields[type][member] = field;
                return true;
            }

            return false;
        }

        private static PropertyType CheckProperty(Type type, string member) {
            var property = type.GetProperty(member, AccessTools.all);
            if (property == null) return PropertyType.None;

            if (property.CanRead) {
                if (property.CanWrite) {
                    properties[type][member] = property;
                    return PropertyType.GetSet;
                }

                properties[type][member] = property;
                return PropertyType.Get;
            }

            properties[type][member] = property;
            return PropertyType.Set;
        }

        private static bool CheckMethod(Type type, string member) {
            var methodInfos = type.GetMethods(AccessTools.all).Where(info => info.Name == member);
            var enumerable = methodInfos as MethodInfo[] ?? methodInfos.ToArray();
            if (!enumerable.Any()) return false;
            methods[type][member] = enumerable.Select(info =>
                (info.GetParameters().Select(param => param.ParameterType).ToArray(), info));
            return true;
        }

        private static bool CheckType(this IEnumerable<Type> target, IEnumerable<object> toCompare) {
            using var enumOrig = target.GetEnumerator();
            using var enumToCompare = toCompare.GetEnumerator();
            var result = true;

            while (enumOrig.MoveNext()) {
                if (!enumToCompare.MoveNext()) return false;
                var curr = enumToCompare.Current?.GetType() ?? typeof(object);
                var comp = enumOrig.Current;
                result = result && (curr == comp || curr.IsSubclassOf(comp));
            }

            if (enumOrig.MoveNext()) result = false;
            return result;
        }

        private static bool isNullable(Type type) {
            if (!type.IsValueType) return true;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>)) {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets a field or property from a specific instance.
        /// </summary>
        /// <typeparam name="T">Type of field/property to get.</typeparam>
        /// <param name="instance">Instance to get field/property from.</param>
        /// <param name="varName">Name of field/property to get.</param>
        /// <returns>Value of field/property in <paramref name="instance"></paramref>.</returns>
        public static T? get<T>(this object instance, string varName) {
            var type = instance.GetType();
            if (!fields.ContainsKey(type)) {
                fields[type] = new Dictionary<string, FieldInfo>();
            }

            if (!properties.ContainsKey(type)) {
                properties[type] = new Dictionary<string, PropertyInfo>();
            }

            if (!fields[type].ContainsKey(varName) && !properties[type].ContainsKey(varName)) {
                if (!CheckField(type, varName)) {
                    if (!CheckProperty(type, varName).HasFlag(PropertyType.Get))
                        throw new MissingMemberException(varName);
                }
            }

            if (fields[type].ContainsKey(varName)) {
                var result = fields[type][varName].GetValue(instance);
                if (result == null && isNullable(typeof(T))) return default;
                if (result is T res) return res;
                throw new InvalidCastException(varName);
            }

            if (properties[type][varName].CanRead) {
                var result = properties[type][varName].GetValue(instance);
                if (result == null && isNullable(typeof(T))) return default;
                if (result is T res) return res;
                throw new InvalidCastException(varName);
            }

            throw new InvalidOperationException(varName);
        }

        /// <summary>
        /// Instantly sets a field or property from a specific instance.
        /// </summary>
        /// <typeparam name="T">Type of the field/property to set.</typeparam>
        /// <param name="instance">Instance to get the field/property from.</param>
        /// <param name="varName">Name of the field/property to set.</param>
        public static void set<T>(this object instance, string varName, T value) => instance.set<T>(varName)(value);

        /// <summary>
        /// Sets a field or property from a specific instance.
        /// </summary>
        /// <typeparam name="T">Type of the field/property to set.</typeparam>
        /// <param name="instance">Instance to get the field/property from.</param>
        /// <param name="varName">Name of the field/property to set.</param>
        /// <returns>An Action to set value to <paramref name="instance"></paramref>.</returns>
        public static Action<T> set<T>(this object instance, string varName) {
            var type = instance.GetType();
            if (!fields.ContainsKey(type)) {
                fields[type] = new Dictionary<string, FieldInfo>();
            }

            if (!properties.ContainsKey(type)) {
                properties[type] = new Dictionary<string, PropertyInfo>();
            }

            if (!fields[type].ContainsKey(varName) && !properties[type].ContainsKey(varName)) {
                if (!CheckField(type, varName)) {
                    if (!CheckProperty(type, varName).HasFlag(PropertyType.Set))
                        throw new MissingMemberException(varName);
                }
            }

            if (fields[type].ContainsKey(varName)) {
                var info = fields[type][varName];
                if (info.FieldType == typeof(T))
                    return value => info.SetValue(instance, value);
                throw new InvalidCastException(varName);
            } else {
                var info = properties[type][varName];
                if (!info.CanWrite) throw new InvalidOperationException();
                if (info.PropertyType == typeof(T))
                    return value => info.SetValue(instance, value);
                throw new InvalidCastException(varName);

            }
        }

        public static InvokableMethod<object> invoke(this object instance, string methodName) =>
            instance.invoke<object>(methodName);

        /// <summary>
        /// Invokes a non-static method.
        /// </summary>
        /// <typeparam name="T">Return type of the method to invoke.</typeparam>
        /// <param name="instance">Instance to invoke the method from.</param>
        /// <param name="methodName">Name of the method to invoke.</param>
        /// <returns>A <see cref="InvokableMethod{T}">delegate</see> that invokes the method.</returns>
        public static InvokableMethod<T> invoke<T>(this object instance, string methodName) {
            var type = instance.GetType();
            if (!methods.ContainsKey(type)) {
                methods[type] = new Dictionary<string, IEnumerable<(Type[], MethodInfo)>>();
            }

            if (!methods[type].ContainsKey(methodName)) {
                if (!CheckMethod(type, methodName)) {
                    throw new MissingMemberException(methodName);
                }
            }

            var methodInfos = methods[type][methodName];
            return args => {
                foreach (var methodInfo in methodInfos) {
                    if (methodInfo.Item1.CheckType(args)) {
                        if (typeof(T) != typeof(object) && methodInfo.Item2.ReturnType != typeof(T))
                            throw new InvalidCastException(methodName);
                        return (T) methodInfo.Item2.Invoke(instance, args);
                    }
                }

                throw new MissingMemberException(methodName);
            };
        }

        /// <summary>
        /// Clears infos in <see cref="fields"/>, <see cref="properties"/>, and <see cref="methods"/>.
        /// </summary>
        public static void ClearMemberInfoCache() {
            fields = new Dictionary<Type, Dictionary<string, FieldInfo>>();
            properties = new Dictionary<Type, Dictionary<string, PropertyInfo>>();
            methods = new Dictionary<Type, Dictionary<string, IEnumerable<(Type[], MethodInfo)>>>();
        }
    }
}