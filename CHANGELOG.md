## v1.0.0

Created two branches to support different versions of the Addressable Package:
- Addressables-3 for version 3.1.0 => Support for catalogs v2
- Addressables-4 for version 4.0.1 => Support for catalogs v3

Main branch now only contains a stub build script.

Reworked the build script to comply with the `SchemaBuilder` design:
- Added `ReferenceSchemaBuilder` - Provides the references of a base game to the build pipeline
- Added `SwapInternalIdSchemaBuilder` - Replaces the internal id of base game bundles with the ones detected by analysis
- Added `CatalogExportSchemaBuilder` - Generates and copy additional catalogs based on Build Path variable

## v0.1.0

First milestone release of the tool. Read documentation for details.

The next developement efforts will be focused on the new Addressable v4 version. v4 is not backward compatible with earlier versions of Catalogs so the features developed for v4 will be backported to v3 afterward. With both branches being maintained at the same time.

Main branch will be authoritative on changes common to the two build script branches (GUI & Analyzer).