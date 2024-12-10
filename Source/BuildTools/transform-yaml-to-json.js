const fs = require('fs');
const yaml = require('js-yaml');

// Paths
const yamlFilePath = process.argv[2]; // First CLI argument: Path to YAML file
const jsonFilePath = process.argv[3]; // Second CLI argument: Path to JSON output

try {
    // Load and parse the YAML file
    const yamlContent = fs.readFileSync(yamlFilePath, 'utf8');
    const dataObjects = yaml.load(yamlContent);

    // Initialize the structure for Ingestion and Service
    const jsonStructure = {
        Ingestion: [],
        Service: []
    };

    // Process Ingestion items
    if (dataObjects.Ingestion) {
        for (const [dataObjectTypeName, item] of Object.entries(dataObjects.Ingestion)) {
            jsonStructure.Ingestion.push({
                DataObjectTypeName: dataObjectTypeName,
                IdSubstitute: item.IdSubstitute || "id",
                PartitionKey: item.PartitionKey || "id",
                StoreInRaw: item.StoreInRaw !== undefined ? item.StoreInRaw : true
            });
        }
    }

    // Process Service items
    if (dataObjects.Service) {
        for (const [dataObjectTypeName, item] of Object.entries(dataObjects.Service)) {
            jsonStructure.Service.push({
                DataObjectTypeName: dataObjectTypeName,
                PartitionKey: item.PartitionKey || "id",
                CopyFromRaw: item.CopyFromRaw || null
            });
        }
    }

    // Convert to JSON and write to file
    fs.writeFileSync(jsonFilePath, JSON.stringify(jsonStructure, null, 2));
    console.log(`JSON saved to ${jsonFilePath}`);
} catch (error) {
    console.error("Error processing YAML file:", error);
    process.exit(1);
}
