using System;
using System.Data.SqlClient;
using System.IO;
using Microsoft.SqlServer.Dac;
using Microsoft.SqlServer.Dac.Model;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: ExtractSqlSchema <outputDirectory> [<databaseConnectionString> | <dacpacFilePath>]");
            return;
        }

        string outputDirectory = args[0];
        string source = args[1];
        TSqlModel model;

        if (File.Exists(source))
        {
            Console.WriteLine("Using DACPAC file: " + source);
            model = LoadDacpac(source);
        }
        else
        {
            Console.WriteLine("Extracting database schema from: " + source);
            string dacpacPath = Path.Combine(outputDirectory, "database.dacpac");
            ExportDatabaseSchema(source, dacpacPath);
            model = LoadDacpac(dacpacPath);
        }

        Console.WriteLine("Extracting schema...");
        ExtractSchemaObjects(model, outputDirectory);
        Console.WriteLine("Database schema exported successfully.");
    }

    static void ExportDatabaseSchema(string connectionString, string dacpacPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dacpacPath) ?? "./");
        var dacService = new DacServices(connectionString);
        dacService.Extract(
            dacpacPath, 
            "ExtractedDatabase", 
            "1.0.0.0", 
            new Version(1, 0, 0, 0), 
            null, 
            null, 
            new DacExtractOptions(), 
            CancellationToken.None
        );
    }

    static TSqlModel LoadDacpac(string dacpacPath)
    {
        return new TSqlModel(dacpacPath, DacSchemaModelStorageType.Memory);
    }

    static void ExtractSchemaObjects(TSqlModel model, string outputDirectory)
    {
        var objectTypeMappings = new Dictionary<ModelTypeClass, string>
        {
            { ModelSchema.Table, "Tables" },
            { ModelSchema.View, "Views" },
            { ModelSchema.Procedure, "StoredProcedures" },
            { ModelSchema.User, "Security" },
            { ModelSchema.Schema, "Security" }
        };

        Dictionary<string, List<string>> schemaDefinitions = new Dictionary<string, List<string>>();

        foreach (var obj in model.GetObjects(DacQueryScopes.UserDefined))
        {
            string schema = obj.Name.Parts.Count > 1 ? obj.Name.Parts[0] : "dbo";
            string name = obj.Name.Parts.Count > 0 ? obj.Name.Parts[^1] : "UnknownObject";
            string objectType = objectTypeMappings.ContainsKey(obj.ObjectType) ? objectTypeMappings[obj.ObjectType] : "Misc";
            
            if (objectType == "Tables" || objectType == "Views")
            {
                string parentKey = schema + "." + name;
                if (!schemaDefinitions.ContainsKey(parentKey))
                {
                    schemaDefinitions[parentKey] = new List<string>();
                }

                string script = ExtractTableOrViewDefinition(obj);
                schemaDefinitions[parentKey].Add(script);
            }
            else if (objectType == "StoredProcedures")
            {
                string procKey = schema + "." + name;
                if (!schemaDefinitions.ContainsKey(procKey))
                {
                    schemaDefinitions[procKey] = new List<string>();
                }

                if (TryExtractScript(obj, out string script))
                {
                    schemaDefinitions[procKey].Add(script);
                }
            }
            else
            {
                string schemaKey = schema + "." + objectType;
                if (!schemaDefinitions.ContainsKey(schemaKey))
                {
                    schemaDefinitions[schemaKey] = new List<string>();
                }

                if (TryExtractScript(obj, out string script))
                {
                    schemaDefinitions[schemaKey].Add(script);
                }
            }
        }

        foreach (var schemaEntry in schemaDefinitions)
        {
            string[] parts = schemaEntry.Key.Split('.');
            string schema = parts[0];
            string objectName = parts.Length > 1 ? parts[1] : "Unknown";
            string objectType = objectTypeMappings.Values.Contains(objectName) ? objectName : "StoredProcedures";
            string schemaPath = Path.Combine(outputDirectory, schema, objectType);
            Directory.CreateDirectory(schemaPath);

            string filePath = Path.Combine(schemaPath, objectName + ".sql");
            File.WriteAllText(filePath, string.Join("\n", schemaEntry.Value));
            Console.WriteLine($"Exported: {filePath}");
        }
    }

    static string ExtractTableOrViewDefinition(TSqlObject obj)
    {
        List<string> parts = new List<string>();
        if (TryExtractScript(obj, out string tableScript))
        {
            parts.Add(tableScript);
        }
        
        foreach (var child in obj.GetChildren(DacQueryScopes.UserDefined))
        {
            if (child.ObjectType == ModelSchema.DmlTrigger || child.ObjectType == ModelSchema.Index ||
                child.ObjectType == ModelSchema.ExtendedProperty || child.ObjectType == ModelSchema.UniqueConstraint ||
                child.ObjectType == ModelSchema.PrimaryKeyConstraint || child.ObjectType == ModelSchema.ForeignKeyConstraint)
            {
                if (TryExtractScript(child, out string childScript))
                {
                    parts.Add(childScript);
                }
            }
        }
        return string.Join("\n", parts);
    }

    static bool TryExtractScript(TSqlObject obj, out string script)
    {
        try
        {
            if (obj.TryGetScript(out script))
            {
                return true;
            }
        }
        catch (DacModelException)
        {
            Console.WriteLine($"Skipping {obj.Name} - script retrieval not supported.");
        }
        script = string.Empty;
        return false;
    }
}