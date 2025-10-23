using Mono.Cecil;

namespace FbsDumper.Assembly;

public class MemoryPackSchema
{
    public readonly List<MemoryPackClass> Classes = [];
    public readonly List<MemoryPackEnum> Enums = [];
}

public class MemoryPackClass(string className, string typeKeyword = "class")
{
    public readonly string ClassName = className;
    public readonly string TypeKeyword = typeKeyword;
    public readonly List<MemoryPackMember> Members = [];
    public readonly List<MemoryPackMethod> Methods = [];
    public readonly List<string> Attributes = [];
    public string? GenerateType = null;
    public string? SerializeLayout = null;
    public readonly List<MemoryPackUnion> Unions = [];
    public bool IsRecord = false;
}

public class MemoryPackMethod(string name, string returnType, bool isStatic, bool isPublic)
{
    public readonly string Name = name;
    public readonly string ReturnType = returnType;
    public readonly bool IsStatic = isStatic;
    public readonly bool IsPublic = isPublic;
    public readonly List<string> Attributes = [];
    public readonly List<(string Type, string Name)> Parameters = [];
    public bool IsConstructor = false;
}

public class MemoryPackUnion(int tag, string typeName)
{
    public readonly int Tag = tag;
    public readonly string TypeName = typeName;
}

public class MemoryPackMember(string name, TypeReference type, bool isField)
{
    public string Name = name;
    public TypeReference Type = type;
    public bool IsField = isField;
    public int? Order = null;
    public bool IsIgnored = false;
    public bool IsInclude = false;
    public bool SuppressDefaultInitialization = false;
    public bool AllowSerialize = false;
    public List<string> CustomFormatters = [];
    public bool IsReadOnly = false;
    public bool IsInit = false;
    public bool IsRequired = false;
}

public class MemoryPackEnum(TypeDefinition valueType, string enumName)
{
    public readonly string EnumName = enumName;
    public readonly List<MemoryPackEnumField> Fields = [];
    public readonly TypeDefinition Type = valueType;
}

public class MemoryPackEnumField(string name, long value = 0)
{
    public readonly string Name = name;
    public readonly long Value = value;
}

