using System;
using System.Collections.Generic;
using UnityEditor;

namespace MartinCalander.Essentials.Console.Editor
{
    [InitializeOnLoad]
    public static class ConsoleHyperlinks
    {
        static readonly Dictionary<string, Action<Dictionary<string, string>>> s_handlers =
            new Dictionary<string, Action<Dictionary<string, string>>>(StringComparer.Ordinal);

        static ConsoleHyperlinks()
        {
            EditorGUI.hyperLinkClicked -= OnHyperlinkClicked;
            EditorGUI.hyperLinkClicked += OnHyperlinkClicked;
        }

        public static void Register(string href, Action<Dictionary<string, string>> handler)
        {
            s_handlers[href] = handler;
        }

        public static void Unregister(string href)
        {
            s_handlers.Remove(href);
        }

        static void OnHyperlinkClicked(EditorWindow window, HyperLinkClickedEventArgs args)
        {
            if (!args.hyperLinkData.TryGetValue("href", out var href))
                return;

            if (s_handlers.TryGetValue(href, out var handler))
            {
                handler.Invoke(args.hyperLinkData);
            }
        }
    }
}
