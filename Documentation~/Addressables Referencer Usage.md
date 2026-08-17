# Addressables Referencer Usage

This documentation assumes you already know how to set up addressable groups for your own assets and scenes.

Refer to the [Addressables Package Documentation](https://docs.unity3d.com/Packages/com.unity.addressables@3.1/manual/index.html) for instructions and details.

Table of contents
- [Requirements](#requirements)
- [Setup](#setup)
    - [Initial setup](#initial-setup)
    - [Reset Addressables Referencer Settings](#reset-addressables-referencer-settings)
- [Bundle Analysis](#bundle-analysis)
    - [Setup Streaming Assets Folder](#setup-streaming-assets-folder)
    - [Run Bundle Analysis](#run-bundles-analysis)
    - [Path Id Overrides](#path-id-overrides)
- [Addressables Build](#addressables-build)
    - [Build Options](#build-options)
        - [Use Base Game Builtins Asset Bundle](#use-base-game-builtins-asset-bundle)
        - [Export Catalog To Build Location Schema](#export-catalog-to-build-location-schema)
        - [Multi Target Setup](#multi-target-setup)
    - [Build Addressables with References](#build-the-addressables-bundles-with-references)
- [Limitations and Caveats](#limitations-and-caveats)
    - [Game Version not Matching](#game-version-not-matching)
    - [Modifying Base Game Assets](#modifying-base-game-assets)
    - [Shaders](#shaders)
    - [AssetRipper Quirks](#assetripper-export-quirks)

## Requirements

- Addressables >3.0

## Setup

The Addressables Referencer window is accessible at `Window > Asset Management > Addressables Referencer`

![alt text](Images/AR-window.png)

### Initial setup

Before Addressables Referencer can be used, the project requires an existing Addressables Asset Settings,

![alt text](Images/Create-Addressables-Settings.png)

Should unity ask whether the "Legacy Bundles" should get converted to addressables, click on `Ignore` as this process will conflict with Addressables Referencer.

![alt text](Images/Ignore-Conversion.png)

Afterward, Addressables Referencer settings will be automatically created and the Addressables Referencer window fully operational.

![alt text](Images/AR-Empty-Window.png)

### Reset Addressables Referencer Settings

In case something went horribly wrong during your work, you can reset the Addressables Referencer settings to go back to a clean slate.

`Tools & Setup > Reset Addressables Referencer Settings`

![alt text](Images/Reset-Settings.png)

## Bundle Analysis

### Setup Streaming Assets Folder

The first step in setting up the bundles references is to point toward the StreamingAssets folder. It will open a browsing dialog window, use it to select the `StreamingAssets` or `StreamingAssets/aa` folder that contains the bundles you want to reference.

This can be either the actual game installation, or the `Assets/StreamingAssets[/aa]` folder within the Unity project if it contains it.

![alt text](Images/Set-StreamingAssets-Folder.png)

Once selected, the package will generate Addressable Groups that match the game's AssetBundle structure. These groups are locked as "Read-Only" so that no asset can be manually assigned to them. (Since they **will not** be shipped).

> [!TIP]
> Avoid editing the Reference Groups schemas to avoid complications.

![alt text](Images/StreamingAssets-Path.png)![alt text](Images/Newly-Made-Groups.png)

### Run Bundle Analysis

The groups are currently empty. To fill them with the base game addressables assets, use the `Run > Run Addressable Assets Analysis`

![alt text](Images/Run-Analysis.png)

The process will go through every non-scene bundle referenced by the Addressable Catalog and may take a long time to finish. However this is a one-time action (hopefully).

![alt text](Images/Analysis-Running.png)

### Item Tree and Path Id Overrides

Once finished, the Addressables Referencer window will display the Reference group structure with all the bundles/assets that will be referenced during the build.

![alt text](Images/PathId-Overrides.png)

In case it is necessary, it is possible to apply overrides to the pathId that will be provided to the Build Pipeline. There shouldn't be much use cases for this in normal circumstances.

## Addressables Build

This package provides a customized build script to bundle assets with references. It brings several options to the build for ease of use.

### Build options

Addressables referencer build options are available on the right side of the Referencer toolbar.

![alt text](Images/Build-Options.png)

#### Use Base Game Builtins Asset Bundle

This option is used to tell the build pipeline whether it should create references to the base game's `<md4hash>_unitybuiltinassets.bundle` or not, usually you will want this to be `true` as it will make the referenced default shader work for every BuildTarget.
![alt text](Images/Build-Base-Builtins.png)
This option should be set to `false` if the scenes and assets you build into bundles need more builtin assets than what the base game provides.

#### Export Catalog To Build Location Schema

Use the `Export Catalog To Build Location` schema to generate and copy a new catalog to the Addressable Group's `BuildPath` defined in the `BundledAssetGroupSchema`.

![alt text](Images/Export-Catalog-Schema.png)


![alt text](Images/Example-Output-1.png)

The following Addressable Group categories will be included in the catalog:
1. The Addressable Group that the Export Schema belongs to.
2. All the Addressable Groups that use the exact same `Build & Load Variable` (in the `BundledAssetGroupSchema`) whether these groups have an Export Catalog schema.
3. The Addressable Group selected as the `Shared Group` in the Addressables Top Settings (Usually the default)
4. All Addressable groups that do not have an Export Schema (Consider them "Common groups").

> [!CAUTION]
> The hierarchy presented above means that "Common" addressable groups (4th level) **must not** depend on assets found in a group with a catalog export schema (1st & 2nd levels). Likewise, it is unadivsable for an asset in the Shared Group (3rd level) to depend on an asset above in the hierarchy.

> [!IMPORTANT] 
> Due to the existence of the `monoscripts.bundle` and `unitybuiltinassets.bundle` that get built into the Shared group location, it will be necessary to **always** ship the Shared Group output to the game you want to mod either as a "Core Library" or as part of a main mod so that these bundles are available to all mods you build. This also means that all your mods must be updated at once when you do a release build to keep the data of these bundles consistent.

> [!IMPORTANT]
> Addressable Groups with `Local`, `Remote` or `Custom` Build & Load Path variables will not get a catalog export even if they have a schema. Use a specific variable for each group you want to export.

#### Multi Target Setup

If your setup allows it you can export the catalog for multiple `BuildTarget` at once. 
The currently active BuildProfile's `BuildTarget` is always included in the build process and cannot be removed.
![alt text](Images/Multiple-Targets.png)
> [!CAUTION]
> Using this option doesn't specifically rebuilds assets for the selected build targets. Only the Active Build Target selected for the project. If you need assets to be rebuilt from one platform to another (possibly shaders), you **will** need to have separate build process for each target.

![alt text](Images/Multiple-Catalogs.png)

Once you have all your catalogs generated, its up to you to load the right one for the platform using [Addressables' interface](https://docs.unity3d.com/Packages/com.unity.addressables@3.1/api/UnityEngine.AddressableAssets.Addressables.LoadContentCatalogAsync.html)

> [!NOTE]
> The `BuildTarget` is usually hardcoded in the Addressables paths and isn't easily reversed from the platform. A manual Switch/Case for it on `Application.Platform` is probably the easiest method.

### Build the Addressables Bundles with References

Once you find your options satisfying, there are two ways to build the addressables bundles with the referencer script:
- In the Addressables Groups Window
    - `ToolBar > Build > New Build > Reference Build Script`
- In the Addressables Referencer Window
    - `Toolbar > Build > Build Addressables Bundles With Referencer Script`

These two method produce the same output.

![alt text](Images/Build-Method1.png)![alt text](Images/Build-Method2.png)

## Limitations and caveats

### Game version not matching

Addressables Referencer requires an exact match between the editor project and the game bundles assets for them to be referenced. If you try to reference assets from a newer version of the game you are modding compared to the version that is used in your editor project, you can run into trouble with certain assets that were edited by the developer between those versions. These assets (mainly GameObject prefabs) won't referenced and will be logged to avoid trouble at runtime.

Ensure the version you are working on is up to date with the current state of the game you are trying to mod.

### Modifying Base Game Assets

> [!CAUTION]
> Using Addressables Referencer, you are required not to edit referenced assets and their dependencies, any modifications will either get ignored at runtime (because the asset you modified isn't shipped with your bundles) or will create issues for the objects you referenced the assets for because they won't be able to find the objects/components it needs. 

### Shaders

As mentionned in the [Catalog Export](#export-catalog-to-build-location-schema) section, it might necessary to rebuild Shaders or different objects with different `BuildTarget`. In that case, disable the multi-catalog setup and do an Addressable build while changing the Active Build Target each time.

### AssetRipper export quirks

Sometimes with AssetRipper, you might encounter a case with Sprites where it creates both a .asset `Sprite` and a .png `Texture2D` for a single sprite. However, the `Texture2D` asset can hold the same `Sprite` as a sub-asset. In that case, the Sprite might appear incorrectly at runtime if the Addressable Asset is the .png `Texture2D` and its subasset `Sprite` when the actual asset being referenced in a `SpriteAtlas` is the .asset.

![alt text](Images/AssetRipper-Special-Cases.png)

There are a few solutions to this:
- Make the .asset `Sprite` addressable as well in one of your own bundles.
- Replace the .asset `Sprite` references in the project by references to the `Texture2D` sub-asset version

If there are no subasset, make the .asset `Sprite` Addressable.