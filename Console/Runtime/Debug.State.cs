using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;
using UDebug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace MartinCalander.Essentials.Console
{
public static partial class Debug
{
    private static readonly Dictionary<Type, FieldInfo[]> TypeFieldCache = new();
    private static readonly object TypeFieldCacheLock = new();

    [HideInCallstack]
    public static void LogState(object instance)
    {
        LogState(instance, instance as Object);
    }

    [HideInCallstack]
    public static void LogState(object instance, Object context)
    {
        if (instance == null)
        {
            if (context == null)
                UDebug.Log("null");
            else
                UDebug.Log("null", context);

            return;
        }

        var message = BuildStateMessage(instance);
        var resolvedContext = context ?? instance as Object;
        if (resolvedContext == null)
            UDebug.Log(message);
        else
            UDebug.Log(message, resolvedContext);
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
                return cached;
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
                    continue;

                if (field.IsDefined(typeof(CompilerGeneratedAttribute), true))
                    continue;

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
            return "field";

        if (rawName.Length > 4 && rawName[0] == '<' && rawName.EndsWith(">k__BackingField", StringComparison.Ordinal))
        {
            var endIndex = rawName.IndexOf('>');
            if (endIndex > 1)
                return rawName.Substring(1, endIndex - 1);
        }

        if (rawName.StartsWith("m_", StringComparison.Ordinal) && rawName.Length > 2)
            return rawName.Substring(2);

        if (rawName[0] == '_' && rawName.Length > 1)
            return rawName.Substring(1);

        return rawName;
    }
}
}
