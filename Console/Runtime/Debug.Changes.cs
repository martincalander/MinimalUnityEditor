using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using UnityEngine;
using UDebug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace MartinCalander.Essentials.Console
{
public static partial class Debug
{
    private static readonly Dictionary<string, object> LastObservedValues = new();
    private static readonly object LastObservedValuesLock = new();

    [HideInCallstack]
    public static void LogChanges<T>(
        Expression<Func<T>> valueExpression,
        Object context = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        if (!TryEvaluate(valueExpression, out var currentValue, out var exception))
        {
            UDebug.LogError(BuildExpressionFailureMessage(valueExpression, exception), context);
            return;
        }

        var expressionName = ExtractExpressionName(valueExpression.Body);
        var key = BuildChangeKey(valueExpression, filePath, lineNumber);

        object previousValue;
        var hasPrevious = false;

        lock (LastObservedValuesLock)
        {
            if (LastObservedValues.TryGetValue(key, out previousValue))
                hasPrevious = true;

            LastObservedValues[key] = currentValue;
        }

        if (!hasPrevious || ValuesEqual(previousValue, currentValue))
            return;

        var message = $"{expressionName}={FormatValue(currentValue)}\n(was: {FormatValue(previousValue)})";
        var resolvedContext = context ?? TryResolveUnityObjectContext(valueExpression);
        if (resolvedContext == null)
            UDebug.Log(message);
        else
            UDebug.Log(message, resolvedContext);
    }

    private static string BuildChangeKey(LambdaExpression valueExpression, string filePath, int lineNumber)
    {
        var expressionBody = valueExpression?.Body?.ToString() ?? "null";
        var owner = TryResolveExpressionOwner(valueExpression?.Body);
        var ownerId = "none";

        if (owner is Object unityObject)
            ownerId = unityObject == null ? "unity:null" : $"unity:{unityObject.GetInstanceID().ToString(CultureInfo.InvariantCulture)}";
        else if (owner != null)
            ownerId = $"obj:{RuntimeHelpers.GetHashCode(owner).ToString(CultureInfo.InvariantCulture)}";

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
            return null;

        expression = UnwrapConvert(expression);

        if (expression is MemberExpression memberExpression)
        {
            if (memberExpression.Expression == null)
                return null;

            return TryEvaluateSubExpression(memberExpression.Expression);
        }

        return null;
    }

    private static object TryEvaluateSubExpression(Expression expression)
    {
        if (expression == null)
            return null;

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
            return true;

        if (previousValue is Object || currentValue is Object)
        {
            var previousUnityObject = previousValue as Object;
            var currentUnityObject = currentValue as Object;
            return previousUnityObject == currentUnityObject;
        }

        return Equals(previousValue, currentValue);
    }
}
}
