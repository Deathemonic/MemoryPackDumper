using Mono.Cecil;
using Newtonsoft.Json;

namespace FbsDumper.Assembly;

public class FlatSchema
{
    public readonly List<FlatEnum> FlatEnums = [];
    public readonly List<FlatTable> FlatTables = [];
}

public class FlatTable(string tableName)
{
    public readonly List<FlatField> Fields = [];
    public readonly string TableName = tableName;
    public bool NoCreate = false;
}

public class FlatField(TypeDefinition type, string name, bool isArray = false)
{
    public bool IsArray = isArray;
    public string Name = name;
    public int Offset;

    [JsonIgnore] public TypeDefinition Type = type;
}

public class FlatEnum(TypeDefinition valueType, string enumName)
{
    public readonly string EnumName = enumName;
    public readonly List<FlatEnumField> Fields = [];

    [JsonIgnore] public readonly TypeDefinition Type = valueType;
}

public class FlatEnumField(string name, long value = 0)
{
    public readonly string Name = name;
    public readonly long Value = value;
}