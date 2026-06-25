using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;
using UDebug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Essentials.Console
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal static class DebugRuntimeHelper
    {
        private static readonly Dictionary<string, object> LastObservedValues = new();
        private static readonly object LastObservedValuesLock = new();
        private static readonly Dictionary<Type, FieldInfo[]> TypeFieldCache = new();
        private static readonly object TypeFieldCacheLock = new();

        internal static void LogExpression<T>(Expression<Func<T>> valueExpression, Object context)
        {
            if (!TryEvaluate(valueExpression, out var value, out var exception))
            {
                UDebug.LogError(BuildExpressionFailureMessage(valueExpression, exception), context);
                return;
            }

            var name = ExtractExpressionName(valueExpression.Body);
            var message = $"{name}={FormatValue(value)}";
            if (context == null)
            {
                UDebug.Log(message);
            }
            else
            {
                UDebug.Log(message, context);
            }
        }

        internal static void LogChanges<T>(Expression<Func<T>> valueExpression, Object context, string filePath, int lineNumber)
        {
            if (!TryEvaluate(valueExpression, out var currentValue, out var exception))
            {
                UDebug.LogError(BuildExpressionFailureMessage(valueExpression, exception), context);
                return;
            }

            var expressionName = ExtractExpressionName(valueExpression.Body);
            var key = BuildChangeKey(valueExpression, filePath, lineNumber);

            object previousValue = null;
            var hasPrevious = false;

            lock (LastObservedValuesLock)
            {
                if (LastObservedValues.TryGetValue(key, out previousValue))
                {
                    hasPrevious = true;
                }

                LastObservedValues[key] = currentValue;
            }

            if (!hasPrevious || ValuesEqual(previousValue, currentValue))
            {
                return;
            }

            var message = $"{expressionName}={FormatValue(currentValue)}\n(was: {FormatValue(previousValue)})";
            var resolvedContext = context ?? TryResolveUnityObjectContext(valueExpression);
            if (resolvedContext == null)
            {
                UDebug.Log(message);
            }
            else
            {
                UDebug.Log(message, resolvedContext);
            }
        }

        internal static void LogState(object instance, Object context)
        {
            if (instance == null)
            {
                if (context == null)
                {
                    UDebug.Log("null");
                }
                else
                {
                    UDebug.Log("null", context);
                }

                return;
            }

            var message = BuildStateMessage(instance);
            var resolvedContext = context ?? instance as Object;
            if (resolvedContext == null)
            {
                UDebug.Log(message);
            }
            else
            {
                UDebug.Log(message, resolvedContext);
            }
        }

        private static bool TryEvaluate<T>(Expression<Func<T>> valueExpression, out T value, out Exception exception)
        {
            value = default;
            exception = null;

            if (valueExpression == null)
            {
                exception = new ArgumentNullException(nameof(valueExpression));
                return false;
            }

            try
            {
                value = valueExpression.Compile().Invoke();
                return true;
            }
            catch (Exception ex)
            {
                exception = ex;
                return false;
            }
        }

        private static string BuildExpressionFailureMessage(LambdaExpression expression, Exception exception)
        {
            if (expression == null)
            {
                return "Debug expression logging failed because the expression was null.";
            }

            var reason = exception == null ? "Unknown error." : exception.Message;
            return $"Debug expression logging failed for '{expression}': {reason}";
        }

        private static string ExtractExpressionName(Expression expression)
        {
            if (expression == null)
            {
                return "value";
            }

            expression = UnwrapConvert(expression);

            if (expression is MemberExpression memberExpression)
            {
                return memberExpression.Member.Name;
            }

            if (expression is ParameterExpression parameterExpression)
            {
                return parameterExpression.Name ?? "value";
            }

            if (expression is MethodCallExpression methodCallExpression)
            {
                return methodCallExpression.Method.Name + "()";
            }

            if (expression is ConstantExpression)
            {
                return "value";
            }

            return expression.ToString();
        }

        private static Expression UnwrapConvert(Expression expression)
        {
            while (expression is UnaryExpression unaryExpression &&
                   (unaryExpression.NodeType == ExpressionType.Convert ||
                    unaryExpression.NodeType == ExpressionType.ConvertChecked))
            {
                expression = unaryExpression.Operand;
            }

            return expression;
        }

        private static string FormatValue(object value)
        {
            if (value == null)
            {
                return "null";
            }

            if (value is Object unityObject)
            {
                return unityObject == null ? "null" : unityObject.name;
            }

            if (value is string stringValue)
            {
                return stringValue;
            }

            if (value is IFormattable formattable)
            {
                return formattable.ToString(null, CultureInfo.InvariantCulture);
            }

            return value.ToString() ?? "null";
        }

        private static string BuildChangeKey(LambdaExpression valueExpression, string filePath, int lineNumber)
        {
            var expressionBody = valueExpression?.Body?.ToString() ?? "null";
            var owner = TryResolveExpressionOwner(valueExpression?.Body);
            var ownerId = "none";

            if (owner is Object unityObject)
            {
#if UNITY_6000_0_OR_NEWER
                ownerId = unityObject == null ? "unity:null" : $"unity:{unityObject.GetEntityId()}";
#else
                ownerId = unityObject == null ? "unity:null" : $"unity:{unityObject.GetInstanceID().ToString(CultureInfo.InvariantCulture)}";
#endif
            }
            else if (owner != null)
            {
                ownerId = $"obj:{RuntimeHelpers.GetHashCode(owner).ToString(CultureInfo.InvariantCulture)}";
            }

            return $"{filePath}:{lineNumber.ToString(CultureInfo.InvariantCulture)}:{expressionBody}:{ownerId}";
        }

        private static Object TryResolveUnityObjectContext(LambdaExpression valueExpression)
        {
            var owner = TryResolveExpressionOwner(valueExpression?.Body);
            return owner as Object;
        }

        private static object TryResolveExpressionOwner(Expression expression)
        {
            if (expression == null)
            {
                return null;
            }

            expression = UnwrapConvert(expression);

            if (expression is MemberExpression memberExpression)
            {
                if (memberExpression.Expression == null)
                {
                    return null;
                }

                return TryEvaluateSubExpression(memberExpression.Expression);
            }

            return null;
        }

        private static object TryEvaluateSubExpression(Expression expression)
        {
            if (expression == null)
            {
                return null;
            }

            try
            {
                var boxed = Expression.Convert(expression, typeof(object));
                return Expression.Lambda<Func<object>>(boxed).Compile().Invoke();
            }
            catch
            {
                return null;
            }
        }

        private static bool ValuesEqual(object previousValue, object currentValue)
        {
            if (ReferenceEquals(previousValue, currentValue))
            {
                return true;
            }

            if (previousValue is Object || currentValue is Object)
            {
                var previousUnityObject = previousValue as Object;
                var currentUnityObject = currentValue as Object;
                return previousUnityObject == currentUnityObject;
            }

            return Equals(previousValue, currentValue);
        }

        private static string BuildStateMessage(object instance)
        {
            var type = instance.GetType();
            var fields = GetInspectableFields(type);
            var builder = new StringBuilder();
            builder.Append(type.Name).Append(" state:");

            if (fields.Length == 0)
            {
                builder.Append("\nvalue=").Append(FormatValue(instance));
                return builder.ToString();
            }

            for (var i = 0; i < fields.Length; i++)
            {
                var field = fields[i];
                object value;

                try
                {
                    value = field.GetValue(instance);
                }
                catch (Exception ex)
                {
                    value = $"<error: {ex.GetType().Name}>";
                }

                builder.Append('\n')
                    .Append(GetReadableFieldName(field.Name))
                    .Append('=')
                    .Append(FormatValue(value));
            }

            return builder.ToString();
        }

        private static FieldInfo[] GetInspectableFields(Type type)
        {
            lock (TypeFieldCacheLock)
            {
                if (TypeFieldCache.TryGetValue(type, out var cached))
                {
                    return cached;
                }
            }

            var fields = new List<FieldInfo>();
            var currentType = type;

            while (currentType != null && currentType != typeof(object))
            {
                var declaredFields = currentType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                Array.Sort(declaredFields, CompareFieldOrder);

                for (var i = 0; i < declaredFields.Length; i++)
                {
                    var field = declaredFields[i];
                    if (field.IsStatic)
                    {
                        continue;
                    }

                    if (field.IsDefined(typeof(CompilerGeneratedAttribute), true))
                    {
                        continue;
                    }

                    fields.Add(field);
                }

                currentType = currentType.BaseType;
            }

            var result = fields.ToArray();
            lock (TypeFieldCacheLock)
            {
                TypeFieldCache[type] = result;
            }

            return result;
        }

        private static int CompareFieldOrder(FieldInfo left, FieldInfo right)
        {
            try
            {
                return left.MetadataToken.CompareTo(right.MetadataToken);
            }
            catch
            {
                return string.Compare(left.Name, right.Name, StringComparison.Ordinal);
            }
        }

        private static string GetReadableFieldName(string rawName)
        {
            if (string.IsNullOrEmpty(rawName))
            {
                return "field";
            }

            if (rawName.Length > 4 && rawName[0] == '<' && rawName.EndsWith(">k__BackingField", StringComparison.Ordinal))
            {
                var endIndex = rawName.IndexOf('>');
                if (endIndex > 1)
                {
                    return rawName.Substring(1, endIndex - 1);
                }
            }

            if (rawName.StartsWith("m_", StringComparison.Ordinal) && rawName.Length > 2)
            {
                return rawName.Substring(2);
            }

            if (rawName[0] == '_' && rawName.Length > 1)
            {
                return rawName.Substring(1);
            }

            return rawName;
        }
    }
#else
    internal static class DebugRuntimeHelper
    {
        internal static void LogExpression<T>(Expression<Func<T>> valueExpression, Object context)
        {
        }

        internal static void LogChanges<T>(Expression<Func<T>> valueExpression, Object context, string filePath, int lineNumber)
        {
        }

        internal static void LogState(object instance, Object context)
        {
        }
    }
#endif
}
