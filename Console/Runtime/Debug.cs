using System;
using System.Globalization;
using System.Linq.Expressions;
using UnityEngine;
using UDebug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace MartinCalander.Essentials.Console
{
public static partial class Debug
{
    public static ILogger logger => UDebug.unityLogger;
    public static ILogger unityLogger => UDebug.unityLogger;
    public static bool isDebugBuild => UDebug.isDebugBuild;

    [HideInCallstack]
    public static void Log(object message) => UDebug.Log(message);

    [HideInCallstack]
    public static void Log(object message, Object context) => UDebug.Log(message, context);

    [HideInCallstack]
    public static void LogWarning(object message) => UDebug.LogWarning(message);

    [HideInCallstack]
    public static void LogWarning(object message, Object context) => UDebug.LogWarning(message, context);

    [HideInCallstack]
    public static void LogError(object message) => UDebug.LogError(message);

    [HideInCallstack]
    public static void LogError(object message, Object context) => UDebug.LogError(message, context);

    [HideInCallstack]
    public static void LogException(Exception exception) => UDebug.LogException(exception);

    [HideInCallstack]
    public static void LogException(Exception exception, Object context) => UDebug.LogException(exception, context);

    [HideInCallstack]
    public static void Assert(bool condition) => UDebug.Assert(condition);

    [HideInCallstack]
    public static void Assert(bool condition, object message) => UDebug.Assert(condition, message);

    [HideInCallstack]
    public static void Assert(bool condition, Object context) => UDebug.Assert(condition, context);

    [HideInCallstack]
    public static void Assert(bool condition, object message, Object context) => UDebug.Assert(condition, message, context);

    [HideInCallstack]
    public static void AssertFormat(bool condition, string format, params object[] args) => UDebug.AssertFormat(condition, format, args);

    [HideInCallstack]
    public static void AssertFormat(bool condition, Object context, string format, params object[] args) =>
        UDebug.AssertFormat(condition, context, format, args);

    [HideInCallstack]
    public static void LogFormat(string format, params object[] args) => UDebug.LogFormat(format, args);

    [HideInCallstack]
    public static void LogFormat(LogType logType, LogOption logOptions, Object context, string format, params object[] args) =>
        UDebug.LogFormat(logType, logOptions, context, format, args);

    [HideInCallstack]
    public static void Log<T>(Expression<Func<T>> valueExpression)
    {
        Log(valueExpression, null);
    }

    [HideInCallstack]
    public static void Log<T>(Expression<Func<T>> valueExpression, Object context)
    {
        if (!TryEvaluate(valueExpression, out var value, out var exception))
        {
            UDebug.LogError(BuildExpressionFailureMessage(valueExpression, exception), context);
            return;
        }

        var name = ExtractExpressionName(valueExpression.Body);
        var message = $"{name}={FormatValue(value)}";
        if (context == null)
            UDebug.Log(message);
        else
            UDebug.Log(message, context);
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
            return "Debug expression logging failed because the expression was null.";

        var reason = exception == null ? "Unknown error." : exception.Message;
        return $"Debug expression logging failed for '{expression}': {reason}";
    }

    private static string ExtractExpressionName(Expression expression)
    {
        if (expression == null)
            return "value";

        expression = UnwrapConvert(expression);

        if (expression is MemberExpression memberExpression)
            return memberExpression.Member.Name;

        if (expression is ParameterExpression parameterExpression)
            return parameterExpression.Name ?? "value";

        if (expression is MethodCallExpression methodCallExpression)
            return methodCallExpression.Method.Name + "()";

        if (expression is ConstantExpression)
            return "value";

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
            return "null";

        if (value is Object unityObject)
            return unityObject == null ? "null" : unityObject.name;

        if (value is string stringValue)
            return stringValue;

        if (value is IFormattable formattable)
            return formattable.ToString(null, CultureInfo.InvariantCulture);

        return value.ToString() ?? "null";
    }
}
}
