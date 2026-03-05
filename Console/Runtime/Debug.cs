using System;
using System.Linq.Expressions;
using System.Diagnostics;
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
    [Conditional("UNITY_EDITOR")]
    [Conditional("DEVELOPMENT_BUILD")]
    public static void Log<T>(Expression<Func<T>> valueExpression)
    {
        Log(valueExpression, null);
    }

    [HideInCallstack]
    [Conditional("UNITY_EDITOR")]
    [Conditional("DEVELOPMENT_BUILD")]
    public static void Log<T>(Expression<Func<T>> valueExpression, Object context)
    {
        DebugRuntimeHelper.LogExpression(valueExpression, context);
    }
}
}
