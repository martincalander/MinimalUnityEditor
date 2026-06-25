using System;
using System.Linq.Expressions;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Essentials.Console
{
public static partial class Debug
{
    [HideInCallstack]
    [Conditional("UNITY_EDITOR")]
    [Conditional("DEVELOPMENT_BUILD")]
    public static void LogChanges<T>(
        Expression<Func<T>> valueExpression,
        Object context = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        DebugRuntimeHelper.LogChanges(valueExpression, context, filePath, lineNumber);
    }
}
}
