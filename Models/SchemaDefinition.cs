namespace Dac2Sql.Models;

class SchemaDefinition
{
    public required string Schema { get; set; }
    public required string ObjectName { get; set; }
    public required string ObjectType { get; set; }
    public List<string> Definitions { get; set; } = new();
}
