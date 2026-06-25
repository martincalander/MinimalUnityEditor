using System;
using System.Collections.Generic;
using UnityEngine;

namespace Essentials.MenuItem.Editor
{
    [Serializable]
    internal sealed class BundleState
    {
        [SerializeField] internal string id;
        [SerializeField] internal bool enabled;
    }

    [Serializable]
    internal sealed class MenuItemSettingsData
    {
        [SerializeField] internal bool moduleEnabled = true;
        [SerializeField] internal bool customDefinitionsEnabled;
        [SerializeField] internal string customDefinitionsText = string.Empty;
        [SerializeField] internal List<BundleState> bundles = new List<BundleState>();

        internal bool GetBundleEnabled(string bundleId, bool defaultValue)
        {
            if (string.IsNullOrEmpty(bundleId))
            {
                return defaultValue;
            }

            for (var i = 0; i < bundles.Count; i++)
            {
                if (string.Equals(bundles[i].id, bundleId, StringComparison.OrdinalIgnoreCase))
                {
                    return bundles[i].enabled;
                }
            }

            return defaultValue;
        }

        internal void SetBundleEnabled(string bundleId, bool enabled)
        {
            if (string.IsNullOrEmpty(bundleId))
            {
                return;
            }

            for (var i = 0; i < bundles.Count; i++)
            {
                if (string.Equals(bundles[i].id, bundleId, StringComparison.OrdinalIgnoreCase))
                {
                    bundles[i].enabled = enabled;
                    return;
                }
            }

            bundles.Add(new BundleState { id = bundleId, enabled = enabled });
        }
    }
}
