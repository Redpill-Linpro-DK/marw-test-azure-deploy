using CommandLine;
using DomainNamingHelper;

var parser = new Parser(with => with.EnableDashDash = true);
var userProvidedParsed = parser.ParseArguments<CliParameters>(args);

Exception? lastException = null;

const string generatorComment = "This code was generated via the DomainNamingHelper.exe project located in the Common repo and made available via the DIH.{project}.Common.Domain NuGet Package.";

userProvidedParsed.WithParsed(cliParameters =>
{
    Console.WriteLine($"DataObjectTypeName  : {cliParameters.DataObjectTypeName}");
    Console.WriteLine($"Namespace           : {cliParameters.Namespace}");
    Console.WriteLine($"Layername           : {cliParameters.Layername}");
    Console.WriteLine($"Output Directory    : {cliParameters.OutputDir}");
});

userProvidedParsed.WithNotParsed(errors =>
{
    // If not all required arguments are provided, emit help
    if (errors.IsHelp() || errors.IsVersion())
    {
        Console.WriteLine("Usage: DomainNamingHelper.exe [options]");
        Console.WriteLine("Options:");
        Console.WriteLine("--dataObjectTypeName <typeList>  - DataObjectTypeName to generate code for (e.g., Companies,Contractors)");
        Console.WriteLine("--ns <namespace>                 - Namespace for generated code");
        Console.WriteLine("--layername <layer>              - Layer name for generated code (Ingestion / Service)");
        Console.WriteLine("--outputdir <directory>          - Output directory for generated code");
    }
    else
    {
        Console.WriteLine("Missing required arguments. Use --help for usage information.");
    }
});

userProvidedParsed.WithParsed(userProvided =>
{
    try 
    { 
        Console.WriteLine("Starting Constant class generator ");

        // Valudate user provided namespace
        if (string.IsNullOrWhiteSpace(userProvided.Namespace))
        {
            throw new ArgumentException($"Namespace cannot be empty");
        }
        else
        {
            Console.WriteLine($"Check OK: Good namespace {userProvided.Namespace}.");
        }
        // Valudate user provided dataObjectTypeName
        if (string.IsNullOrWhiteSpace(userProvided.DataObjectTypeName))
        {
            throw new ArgumentException($"dataObjectTypeName cannot be empty");
        }
        else
        {
            Console.WriteLine($"Check OK: Good DataObjectTypeName {userProvided.DataObjectTypeName}.");
        }
        // Valudate user provided type names
        if (string.IsNullOrWhiteSpace(userProvided.Layername))
        {
            throw new ArgumentException($"Layer name name cannot be empty");
        }
        else
        {
            Console.WriteLine($"Check OK: Good layer name {userProvided.Layername}.");
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
        Action<string, string> emitFile = (filename, content) => File.WriteAllText(Path.Combine(userProvided.OutputDir, filename), content);

        var generator = new ConstantsClassGenerator();
        string code = generator.GenerateConstantsClass(userProvided.DataObjectTypeName, userProvided.Namespace, userProvided.Layername, generatorComment);
        emitFile($"ResourceNames-{userProvided.DataObjectTypeName.GetHashCode()}.cs", code);

        Console.WriteLine("Constant classes exported successfully");
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
