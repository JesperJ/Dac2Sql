using System;
using Microsoft.Data.SqlClient;
using System.IO;
using Microsoft.SqlServer.Dac;
using Microsoft.SqlServer.Dac.Model;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using Dac2Sql.Models;
using Dac2Sql.Extractors;

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
        string databaseName = "Database";
        TSqlModel model;

        if (File.Exists(source))
        {
            Console.WriteLine("Using DACPAC file: " + source);
            databaseName = Path.GetFileNameWithoutExtension(source);
            model = LoadDacpac(source);
        }
        else
        {
            var builder = new SqlConnectionStringBuilder(source);
            databaseName = builder.InitialCatalog;
            Console.WriteLine("Extracting database schema from: " + databaseName);
            string dacpacPath = Path.Combine(outputDirectory, databaseName + ".dacpac");
            ExportDatabaseSchema(source, dacpacPath);
            model = LoadDacpac(dacpacPath);
        }

        Console.WriteLine("Extracting schema...");
        outputDirectory = Path.Combine(outputDirectory, databaseName);
        var schemaExtractor = new SchemaExtractor(model, outputDirectory);
        schemaExtractor.ExtractSchemaObjects();
        schemaExtractor.WriteSchemaFiles();
        Console.WriteLine("Database schema exported successfully.");
    }

    static void ExportDatabaseSchema(string connectionString, string dacpacPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dacpacPath) ?? "./");
        var builder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = builder.InitialCatalog;

        var dacService = new DacServices(connectionString);
        dacService.Extract(
            dacpacPath,
            databaseName,
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
}
