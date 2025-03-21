# Dac2Sql
Dac2Sql converts .dacpac files or MSSQL databases into organized .sql files.
Inspired by the [SQL Database Projects](https://marketplace.visualstudio.com/items?itemName=ms-mssql.sql-database-projects-vscode) extension in vscode.

## Output Structure
```
/ExportedSchema
    /SchemaName
        /Tables
            TableName.sql
        /Views
            ViewName.sql
        /StoredProcedures
            ProcedureName.sql
    /Security
        CreateSchemaStatements.sql
        CreateUserStatements.sql
```

### Object Handling
* Tables & Views include Triggers, Extended Properties, Indexes, Constraints, and Keys inside their respective .sql file.
* Stored Procedures are stored individually.
* Security (CREATE SCHEMA, CREATE USER) statements go into a separate Security folder.

## Getting started
```
git clone https://github.com/JesperJ/Dac2Sql.git
cd Dac2Sql
dotnet restore
dotnet build
```

## Usage
```
Dac2Sql.exe <outputDirectory> [<databaseConnectionString> | <dacpacFilePath>]
```

* **`<outputDirectory>`** – Directory where the extracted `.sql` files will be stored.
* **`<databaseConnectionString>`** – (Optional) MSSQL connection string to extract schema from a live database.
* **`<dacpacFilePath>`** – (Optional) Path to a `.dacpac` file to extract schema from.

**Note:** Extracting from a database via `ConnectionString` is **untested**.

## Example
Extract schema from a .dacpac file:
```
Dac2Sql.exe .\ExportedSchema\ .\Database.dacpac
```

## License
MIT License
