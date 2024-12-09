using CommandLine;

namespace DomainNamingHelper;

public class CliParameters
{
    [Option("dataObjectTypeName", Required = true, HelpText = "Name of type to generate code for")]
    public string? DataObjectTypeName { get; set; }

    [Option("ns", Required = true, HelpText = "Namespace for generated C# class")]
    public string? Namespace { get; set; }

    [Option("layername", Required = true, HelpText = "Layer name for constant values in generated C# class (ex. Ingestion)")]
    public string? Layername { get; set; }

    [Option("outputdir", Required = true, HelpText = "Output directory")]
    public string? OutputDir { get; set; }
}