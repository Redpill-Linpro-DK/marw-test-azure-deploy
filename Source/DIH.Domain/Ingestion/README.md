# Ingestion POCO & JSON Schemas

## Nuget package use
When installing the nuget package DIH.DihBaseline.Domain you get the following:
 - POCO classes for deserializing raw JSON (JSON send to the Ingestion layer).
 - JSON Schemas for validating raw JSON.
 - Constant classes in the namespace DIH.Common.Domain.Ingestion

### Using constant classes
You can access domain specific constants via the below classes.

- `DIH.Domain.Ingestion.QueueNames`
- `DIH.Domain.Ingestion.SchemaPaths`
- `DIH.Domain.Ingestion.DataObjectTypeNames`

### Using POCO Classes
You can access POCO classes via the below namespace, where `*` is the name of the object type you wish to work with.

`using DIH.Domain.Ingestion.*`

### Using JSON Schemas
Folders with JSON Schemas are automatically copied to the project's bin folder at build time.

Your code can reference a schema using the below relative path, where `*` is the name of the object type you wish to work with.

`Ingestion/*-schema.json`

## How the POCP & JSON schemas are generated
This "Ingestion" folder is populated during the build phase of the DIH.DihBaseline.Common pipeline.

The file generation is done by OasSchemaExporter.exe (located in the DIH.DihBaseline.Common repo).

The generation is done by parsing `/Source/Schemas/ingestion-oas.yml` in the DIH.DihBaseline.Common repo.

Updating schemas in `ingestion-oas.yml` will update POCO & JSON Schemas and the next build. 
When this happens, you should get the latest version of this nuget package.



