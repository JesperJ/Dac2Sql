using System;
using System.Data.SqlClient;
using System.IO;
using Microsoft.SqlServer.Dac;
using Microsoft.SqlServer.Dac.Model;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using Dac2Sql.Models;

namespace Dac2Sql.Extractors;
class SchemaExtractor
{
    private readonly TSqlModel _model;
    private readonly string _outputDirectory;
    private readonly List<SchemaDefinition> _schemaDefinitions = new();

    private static readonly Dictionary<ModelTypeClass, string> ObjectTypeMappings = new()
    {
        { ModelSchema.Table, "Tables" },
        { ModelSchema.View, "Views" },
        { ModelSchema.Procedure, "StoredProcedures" },
        { ModelSchema.User, "Security" },
        { ModelSchema.Schema, "Security" }
    };

    public SchemaExtractor(TSqlModel model, string outputDirectory)
    {
        _model = model;
        _outputDirectory = outputDirectory;
    }

    public void ExtractSchemaObjects()
    {
        foreach (var obj in _model.GetObjects(DacQueryScopes.UserDefined))
        {
            string schema = obj.Name.Parts.Count > 1 ? obj.Name.Parts[0] : "dbo";
            string name = obj.Name.Parts.Count > 0 ? obj.Name.Parts[^1] : "UnknownObject";
            string objectType = ObjectTypeMappings.ContainsKey(obj.ObjectType) ? ObjectTypeMappings[obj.ObjectType] : "Misc";

            if (objectType == "Security")
            {
                schema = "Security"; // Force all security-related objects into the Security folder
            }

            if (objectType == "Tables" || objectType == "Views")
            {
                var schemaDef = _schemaDefinitions.FirstOrDefault(x => x.ObjectName == name && x.ObjectType == objectType && x.Schema == schema);
                if (schemaDef == null)
                {
                    schemaDef = new SchemaDefinition { Schema = schema, ObjectName = name, ObjectType = objectType };
                    _schemaDefinitions.Add(schemaDef);
                }
                string script = ExtractTableOrViewDefinition(obj);
                script += script.EndsWith("\nGO\n") ? "" : "\nGO\n";
                schemaDef.Definitions.Add(script);
            }
            else if (!IsChildObject(obj) && TryExtractScript(obj, out string script))
            {
                var schemaDef = new SchemaDefinition { Schema = schema, ObjectName = name, ObjectType = objectType };
                schemaDef.Definitions.Add(script);
                _schemaDefinitions.Add(schemaDef);
            }
        }
    }

    public void WriteSchemaFiles()
    {
        foreach (var schemaDef in _schemaDefinitions)
        {
            string schemaPath = schemaDef.Schema == "Security"
                ? Path.Combine(_outputDirectory, "Security")
                : Path.Combine(_outputDirectory, schemaDef.Schema, schemaDef.ObjectType);

            Directory.CreateDirectory(schemaPath);

            string filePath = Path.Combine(schemaPath, schemaDef.ObjectName + ".sql");
            File.WriteAllText(filePath, string.Join("\nGO\n", schemaDef.Definitions));
            Console.WriteLine($"Exported: {filePath}");
        }
    }

    private string ExtractTableOrViewDefinition(TSqlObject obj)
    {
        List<string> parts = new List<string>();
        if (TryExtractScript(obj, out string tableScript))
        {
            parts.Add(tableScript);
        }

        foreach (var child in obj.GetChildren(DacQueryScopes.UserDefined))
        {
            if (IsChildObject(child) && TryExtractScript(child, out string childScript))
            {
                parts.Add(childScript);
            }
        }
        return string.Join("\nGO\n", parts);
    }

    private static bool TryExtractScript(TSqlObject obj, out string script)
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

    private static bool IsChildObject(TSqlObject obj)
    {
        return obj.ObjectType == ModelSchema.DmlTrigger || obj.ObjectType == ModelSchema.Index ||
               obj.ObjectType == ModelSchema.ExtendedProperty || obj.ObjectType == ModelSchema.UniqueConstraint ||
               obj.ObjectType == ModelSchema.PrimaryKeyConstraint || obj.ObjectType == ModelSchema.ForeignKeyConstraint;
    }
}