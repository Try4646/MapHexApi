
# **Map Hex API**

This mod enhances the game by adding custom hexes to the map generation system. It includes features such as adding custom Hexes (Map parts). Adding Components doesnt work rn

## **Installation**

## Requirements
- BepInEx 5.4.21
- ModSync
## Hierachy
```
MageArena/
└── BepInEx/
    └── plugins/
        └── MapHexAPI
            └── MapHexAPI.dll
```


## **Usage**

To integrate the custom hexes into your game, follow the setup provided below:

```csharp
[BepInDependency("com.user.MapHexAPI")]
[BepInPlugin("com.othermodder.smt", "Custom Hex Mod", "1.0.0")]
public class CustomHexMod : BaseUnityPlugin
{
    private void Awake()
    {
        // 1. Load assets
        string modPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        // Load prefab
        var prefab1 = AssetLoader.LoadPrefabFromBundle(
            Path.Combine(modPath, "customhex"),
            "HexPrefab"
        );

        var iconTex1 = AssetLoader.LoadTexture(
            Path.Combine(modPath, "Icons", "CustomHexIcon.png")
        );

        var iconMat1 = AssetLoader.CreateDecalMaterial(iconTex1);

        // 2. Add components to the prefab
        List<MonoBehaviour> components = new List<MonoBehaviour>();
        List<NetworkBehaviour> netcomponents = new List<NetworkBehaviour>();

        // Add custom components
        FlagControllerCustom flagController = prefab1.AddComponent<FlagControllerCustom>();
        components.Add(flagController);
        netcomponents.Add(flagController);

        MoutainWindCustom mountainWind = prefab1.AddComponent<MoutainWindCustom>();
        components.Add(mountainWind);  
        netcomponents.Add(mountainWind);

        // 3. Register the custom hex type
        int hexType = HexRegistry.RegisterHex(
            prefab: prefab1,
            mapIcon: iconMat1,
            components: components,
            netcomponents: netcomponents,
            displayName: "CustomHex"
        );

        Logger.LogInfo($"Successfully registered custom hex with type: {hexType}");
    }
}
```
## **Your Mod Hierarchy**
``` 
MageArena/
└── BepInEx/
    └── plugins/
        └── YourModFolder
            └── Icons
            |   └── icon.png
            └── customhex (Assetbundle)
```

## Prefab Creation
- N/A



## **Change Log**

## v1.0.0
- Release
