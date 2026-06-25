using System;
using System.Diagnostics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Essentials.Console
{
public static partial class Debug
{
    [HideInCallstack]
    [Conditional("UNITY_EDITOR")]
    [Conditional("DEVELOPMENT_BUILD")]
    public static void LogState(object instance)
    {
        LogState(instance, instance as Object);
    }

    [HideInCallstack]
    [Conditional("UNITY_EDITOR")]
    [Conditional("DEVELOPMENT_BUILD")]
    public static void LogState(object instance, Object context)
    {
        DebugRuntimeHelper.LogState(instance, context);
    }
}
}
