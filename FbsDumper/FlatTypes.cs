using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Newtonsoft.Json;

namespace FbsDumper;

public class FlatSchema
{
    public List<FlatEnum> flatEnums = new List<FlatEnum>();
    public List<FlatTable> flatTables = new List<FlatTable>();
}

public class FlatTable
{
    public string tableName;
    public List<FlatField> fields = new List<FlatField>();

    public FlatTable(string tableName)
    {
        this.tableName = tableName;
    }
}

public class FlatField
{
    [JsonIgnore]
    public TypeDefinition type;
    public string Type => type.FullName;

    public bool isArray;
    public string name;

    public FlatField(TypeDefinition type, string name, bool isArray = false)
    {
        this.type = type;
        this.name = name;
        this.isArray = isArray;
    }
}

public class FlatEnum
{
    [JsonIgnore]
    public TypeDefinition type;
    public string Type => type.FullName;

    public string enumName;
    public List<FlatEnumField> fields = new List<FlatEnumField>();

    public FlatEnum(TypeDefinition valueType, string enumName)
    {
        this.type = valueType;
        this.enumName = enumName;
    }
}

public class FlatEnumField
{
    public string name;
    public long value;

    public FlatEnumField(string name, long value = 0)
    { 
        this.name = name;
        this.value = value;
    }
}