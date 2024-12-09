using CommandLine;
using Microsoft.CodeAnalysis;
using NJsonSchema;
using NJsonSchema.CodeGeneration.CSharp;
using NSwag;
using OasSchemaExporter;

var parser = new Parser(with => with.EnableDashDash = true);
var userProvidedParsed = parser.ParseArguments<CliParameters>(args);

Exception? lastException = null;

userProvidedParsed.WithParsed(cliParameters =>
{
    Console.WriteLine($"DataObjectTypeName  : {cliParameters.DataObjectTypeName}");
    Console.WriteLine($"OAS Type            : {cliParameters.OasType}");
    Console.WriteLine($"OAS Path            : {cliParameters.OasPath}");
    Console.WriteLine($"Namespace           : {cliParameters.Namespace}");
    Console.WriteLine($"Output Directory    : {cliParameters.OutputDir}");
    Console.WriteLine($"Serialize NULL      : {cliParameters.SerializeNull}");
});

userProvidedParsed.WithNotParsed(errors =>
{
    // If not all required arguments are provided, emit help
    if (errors.IsHelp() || errors.IsVersion())
    {
        Console.WriteLine("Usage: OasSchemaExporter.exe [options]");
        Console.WriteLine("Options:");
        Console.WriteLine("--dataobjecttypename <typeName>  - Name of type in DIH (DataObjectTypeName)");
        Console.WriteLine("--oastype <typeName>             - Name of type from OAS document");
        Console.WriteLine("--oaspath <path>                 - Path to OpenAPI spec YAML file");
        Console.WriteLine("--ns <namespace>                 - Namespace for generated code");
        Console.WriteLine("--outputdir <directory>          - Output directory for generated code");
        Console.WriteLine("--serializenull <bool>           - Always serialize properties that are null");
    }
    else
    {
        Console.WriteLine("Missing required arguments. Use --help for usage information.");
    }
});

userProvidedParsed.WithParsed(async userProvided =>
{
    try 
    { 
        Console.WriteLine("Starting POCO & JSON Schema export");
        // validate user provided type input
        if (!File.Exists(userProvided.OasPath))
        {
            throw new ArgumentException($"Path to OAS YAML file is invalid: {userProvided.OasPath}");
        }
        else
        {
            Console.WriteLine($"Check OK: Found OAS YAML at {userProvided.OasPath}.");
        }
        // validate user provided namespace
        if (string.IsNullOrWhiteSpace(userProvided.Namespace))
        {
            throw new ArgumentException($"Namespace cannot be empty");
        }
        else
        {
            Console.WriteLine($"Check OK: Good namespace {userProvided.Namespace}.");
        }
        // validate user provided project directory path
        if (string.IsNullOrWhiteSpace(userProvided.OutputDir) || !Directory.Exists(userProvided.OutputDir))
        {
            throw new ArgumentException($"Project directory path not found: {userProvided.OutputDir}");
        }
        else
        {
            Console.WriteLine($"Check OK: Output dir found at {userProvided.OutputDir}.");
        }
        Console.WriteLine($"Serialize null properties: {userProvided.SerializeNull}");

        Action<string, string> emitFile = (filename, content) => File.WriteAllText(Path.Combine(userProvided.OutputDir, filename), content);

        var typesToGenerate = userProvided.OasType?.Split(',').Select(f => f.Trim()) ?? new List<string>();
        if (typesToGenerate.Count() > 1) throw new InvalidOperationException("Only one type supported by code - call this tool with one type, multiple times if needed.");
        var yamlString = File.ReadAllText(userProvided.OasPath);
        var openApiDocument = await OpenApiYamlDocument.FromFileAsync(userProvided.OasPath);

        // validate user provided type list input
        var unknownUserProvidedTypes = typesToGenerate.Where(f => !openApiDocument.Components.Schemas.Keys.Contains(f));
        if (unknownUserProvidedTypes.Any())
        {
            throw new ArgumentException($"Unknown type(s) provided, not found in oas: {string.Join(", ", unknownUserProvidedTypes)}. Known types are {string.Join(", ", openApiDocument.Components.Schemas.Keys)}");
        }
        else
        {
            Console.WriteLine($"Check OK: Found all types in OAS YAML, {userProvided.OasType}.");
        }

        foreach (var oasTypeName in typesToGenerate)
        {
            Console.WriteLine($"Exporting: OAS type {oasTypeName}.");
            CSharpGeneratorSettings csGenSettings = new CSharpGeneratorSettings()
            {
                ClassStyle = CSharpClassStyle.Poco,
                HandleReferences = true,
                Namespace = $"{userProvided.Namespace}.{userProvided.DataObjectTypeName}",   
            };

            var schema = openApiDocument.Components.Schemas[oasTypeName];

            var cSharpGenerator = new CSharpGenerator(schema, csGenSettings);
            var code = CodeCleaningService.CleanAdditionalProperties(cSharpGenerator.GenerateFile());
            if (userProvided.SerializeNull) code = CodeCleaningService.MakeNullPropertiesSerializable(code);
            code = code.Replace("class Anonymous", $"class {userProvided.OasType}");
            emitFile($"{userProvided.DataObjectTypeName}.cs", code);

            Type? t = null;
            try
            {
                t = CompilerServices.Compile(code, $"{userProvided.Namespace}.{userProvided.DataObjectTypeName}.{userProvided.OasType}");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Compile failed for source code:\n\n{code}", ex);
            }

            var generatedSchema = JsonSchema.FromType(t);
            emitFile($"{userProvided.DataObjectTypeName}-schema.json", generatedSchema.ToJson());
        }
        Console.WriteLine("POCO & JSON Schemas exported successfully");
    }
    catch (Exception ex)
    {
        Console.WriteLine("OasSchemaExporter failed with below exception.");
        Console.WriteLine(ex.ToString());
        lastException = ex;
        throw;
    }
});

if (lastException != null) throw lastException;

Console.WriteLine("Finished");
