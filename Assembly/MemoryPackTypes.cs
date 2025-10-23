using Mono.Cecil;

namespace MemoryPackDumper.Assembly;

public class MemoryPackSchema
{
    public readonly List<MemoryPackClass> Classes = [];
    public readonly List<MemoryPackEnum> Enums = [];
}

public class MemoryPackClass(string className, string typeKeyword = "class")
{
    public readonly List<string> Attributes = [];
    public readonly string ClassName = className;
    public readonly List<MemoryPackMember> Members = [];
    public readonly List<MemoryPackMethod> Methods = [];
    public readonly string TypeKeyword = typeKeyword;
    public readonly List<MemoryPackUnion> Unions = [];
    public string? GenerateType = null;
    public bool IsRecord = false;
    public string? SerializeLayout = null;
}

public class MemoryPackMethod(string name, string returnType, bool isStatic, bool isPublic)
{
    public readonly List<string> Attributes = [];
    public readonly bool IsPublic = isPublic;
    public readonly bool IsStatic = isStatic;
    public readonly string Name = name;
    public readonly List<(string Type, string Name)> Parameters = [];
    public readonly string ReturnType = returnType;
    public bool IsConstructor = false;
}

public class MemoryPackUnion(int tag, string typeName)
{
    public readonly int Tag = tag;
    public readonly string TypeName = typeName;
}

public class MemoryPackMember(string name, TypeReference type, bool isField)
{
    public bool AllowSerialize = false;
    public List<string> CustomFormatters = [];
    public bool IsField = isField;
    public bool IsIgnored = false;
    public bool IsInclude = false;
    public bool IsInit = false;
    public bool IsReadOnly = false;
    public bool IsRequired = false;
    public string Name = name;
    public int? Order = null;
    public bool SuppressDefaultInitialization = false;
    public TypeReference Type = type;
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