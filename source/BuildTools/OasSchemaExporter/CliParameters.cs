using CommandLine;

namespace OasSchemaExporter;

public class CliParameters
{
    [Option("dataobjecttypename", Required = true, HelpText = "DataObjectTypename (for naming)")]
    public string? DataObjectTypeName { get; set; }

    [Option("oastype", Required = true, HelpText = "Type from OAS to export")]
    public string? OasType { get; set; }

    [Option("oaspath", Required = true, HelpText = "Path to the OAS YAML file")]
    public string? OasPath { get; set; }

    [Option("ns", Required = true, HelpText = "Namespace for generated C# classes")]
    public string? Namespace { get; set; }

    [Option("outputdir", Required = true, HelpText = "Output directory")]
    public string? OutputDir { get; set; }

    [Option("serializenull", Required = false, HelpText = "When true (default), nullable properties are maked as 'include' for JSON serialization", Default = true)]
    public bool SerializeNull { get; set; }
}