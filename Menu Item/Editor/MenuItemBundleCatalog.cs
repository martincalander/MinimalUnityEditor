using System;
using System.Collections.Generic;

namespace Essentials.MenuItem.Editor
{
    internal static class MenuItemBundleCatalog
    {
        internal static readonly IReadOnlyList<MenuItemBundleDefinition> Bundles = CreateBundles();

        static IReadOnlyList<MenuItemBundleDefinition> CreateBundles()
        {
            var bundles = new List<MenuItemBundleDefinition>();

#if HAS_TMP
            bundles.Add(new MenuItemBundleDefinition(
                "text_mesh_pro",
                "TextMeshPro",
                true,
                true,
                new[]
                {
                    "GameObject/UI/Text - TextMeshPro",
                    "GameObject/3D Object/Text - TextMeshPro",
                    // Unity 6 with UGUI 2.0 groups the common TMP create items under UI (Canvas).
                    "GameObject/UI (Canvas)/Button - TextMeshPro",
                    "GameObject/UI (Canvas)/Dropdown - TextMeshPro",
                    "GameObject/UI (Canvas)/Input Field - TextMeshPro",
                    "GameObject/UI (Canvas)/Text - TextMeshPro",
                    "Component/TextMeshPro/*",
                    "Component/Mesh/TextMeshPro - Text",
                    "Component/UI (Canvas)/Dropdown - TextMeshPro",
                    "Component/UI (Canvas)/TextMeshPro - Input Field",
                    "Component/UI (Canvas)/TextMeshPro - Text (UI)",
                    "Window/TextMeshPro/*",
                    "Window/TextMeshPro",
                    "Windows/TextMeshPro",
                    "Assets/Create/TextMeshPro"
                }));
#else
            bundles.Add(new MenuItemBundleDefinition(
                "text_mesh_pro",
                "TextMeshPro",
                false,
                false,
                Array.Empty<string>()));
#endif

            bundles.Add(new MenuItemBundleDefinition(
                "ui_canvas_legacy",
                "UI (Canvas) Legacy",
                true,
                true,
                new[]
                {
                    "Component/UI (Canvas)/Legacy*",
                    "Component/UI/Legacy",
                    "Component/Mesh/Legacy",
                    "GameObject/3D Object/Legacy",
                    "GameObject/UI (Canvas)/Legacy*",
                    "GameObject/UI/Legacy",
                    "Window/Text",
                    "Windows/Text",
                    "Assets/Create/Text Core",
                    "Assets/Create/GUI Skin",
                    "Assets/Create/Custom Font"
                }));

            bundles.Add(new MenuItemBundleDefinition(
                "ui_toolkit",
                "UI Toolkit",
                true,
                true,
                new[]
                {
                    "Window/UI Toolkit",
                    "Assets/Create/UI Toolkit",
                    "Component/UI Toolkit/*",
                    "GameObject/UI Toolkit/*"
                }));

            bundles.Add(new MenuItemBundleDefinition(
                "jobs",
                "Jobs",
                true,
                true,
                new[]
                {
                    "Jobs/*"
                }));

            bundles.Add(new MenuItemBundleDefinition(
                "upm_packaging",
                "UPM Packaging",
                true,
                true,
                new[]
                {
                    "Assets/Create UPM Package...",
                    "Assets/Export As UPM Package...",
                    "Assets/Import New Asset...",
                    "Assets/Import Package",
                    "Assets/View in Package Manager",
                    "Assets/Export As Asset Package..."
                }));

            bundles.Add(new MenuItemBundleDefinition(
                "d3d12_vulkan",
                "D3D12/Vulkan",
                false,
                true,
                new[]
                {
                    "Edit/Rendering/D3D12*",
                    "Edit/Rendering/Vulkan*",
                    "Assets/Create/D3D12 Settings",
                    "Assets/Create/Vulkan Settings"
                }));

#if HAS_UNITY_TERRAIN
            bundles.Add(new MenuItemBundleDefinition(
                "terrain",
                "Terrain",
                true,
                true,
                new[]
                {
                    "Assets/Create/Terrain",
                    "GameObject/3D Object/Terrain",
                    "Component/Physics/Terrain Collider",
                    "Component/Rendering/Terrain"
                }));
#else
            bundles.Add(new MenuItemBundleDefinition(
                "terrain",
                "Terrain",
                false,
                false,
                Array.Empty<string>()));
#endif

#if HAS_INPUT_SYSTEM
            bundles.Add(new MenuItemBundleDefinition(
                "xr_options",
                "XR Options",
                true,
                true,
                new[]
                {
                    "Component/XR"
                }));
#else
            bundles.Add(new MenuItemBundleDefinition(
                "xr_options",
                "XR Options",
                false,
                false,
                Array.Empty<string>()));
#endif

#if HAS_GLTFAST
            bundles.Add(new MenuItemBundleDefinition(
                "gltf_importer",
                "glTF Importer",
                true,
                true,
                new[]
                {
                    "GameObject/Export glTF",
                    "Assets/Export glTF",
                    "Component/Scripts/GLTFast/*",
                    "Component/Scripts/GLTFast.Documentation.Examples/*"
                }));
#else
            bundles.Add(new MenuItemBundleDefinition(
                "gltf_importer",
                "glTF Importer",
                false,
                false,
                Array.Empty<string>()));
#endif

            bundles.Add(new MenuItemBundleDefinition(
                "assets_extra",
                "Assets Extra",
                true,
                true,
                new[]
                {
                    "Assets/Open C# Project",
                    "Assets/View in Import Activity Window",
                    "Assets/Update UXML Schema",
                    "Assets/Extract Material SubAsset"
                }));

            bundles.Add(new MenuItemBundleDefinition(
                "assets_dependencies",
                "Assets Dependencies",
                true,
                true,
                new[]
                {
                    "Assets/Find References In Scene",
                    "Assets/Find References In Project",
                    "Assets/Select Dependencies"
                }));

#if HAS_UNITY_AI_ASSISTANT
            bundles.Add(new MenuItemBundleDefinition(
                "ai_assistant",
                "AI Assistant",
                true,
                true,
                new[]
                {
                    "Assets/Add to Assistant",
                    "Assets/Generate",
                    "Assets/Search Generated Assets",
                    "Window/AI"
                }));
#else
            bundles.Add(new MenuItemBundleDefinition(
                "ai_assistant",
                "AI Assistant",
                false,
                false,
                Array.Empty<string>()));
#endif

#if HAS_PLATFORM_TOOLKIT
            bundles.Add(new MenuItemBundleDefinition(
                "platform_toolkit",
                "Platform Toolkit",
                true,
                true,
                new[]
                {
                    "Assets/Create/Platform Toolkit"
                }));
#else
            bundles.Add(new MenuItemBundleDefinition(
                "platform_toolkit",
                "Platform Toolkit",
                false,
                false,
                Array.Empty<string>()));
#endif

            bundles.Add(new MenuItemBundleDefinition(
                "gameobject_extra",
                "GameObject Extra",
                true,
                true,
                new[]
                {
                    "GameObject/Set as first sibling",
                    "GameObject/Set as last sibling",
                    "GameObject/Move To View",
                    "GameObject/Align With View",
                    "GameObject/Align View to Selected",
                    "GameObject/Toggle Active State",
                    "GameObject/Create Empty Child",
                    "GameObject/Create Empty Parent",
                    "GameObject/Make Parent",
                    "GameObject/Clear Parent"
                }));

            bundles.Add(new MenuItemBundleDefinition(
                "version_control",
                "Version Control",
                true,
                true,
                new[]
                {
                    "Window/Version Control"
                }));

            return bundles;
        }

        internal static MenuItemBundleDefinition Find(string bundleId)
        {
            if (string.IsNullOrWhiteSpace(bundleId))
            {
                return null;
            }

            for (var i = 0; i < Bundles.Count; i++)
            {
                var bundle = Bundles[i];
                if (string.Equals(bundle.Id, bundleId, StringComparison.OrdinalIgnoreCase))
                {
                    return bundle;
                }
            }

            return null;
        }
    }
}
