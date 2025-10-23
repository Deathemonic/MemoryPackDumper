using MemoryPackDumper.Helpers;
using Mono.Cecil;
using ZLinq;

namespace MemoryPackDumper.Assembly;

public static class TypeStringConverter
{
    private static readonly Dictionary<string, string> TypeMap = new()
    {
        ["System.String"] = "string",
        ["System.Int16"] = "short",
        ["System.UInt16"] = "ushort",
        ["System.Int32"] = "int",
        ["System.UInt32"] = "uint",
        ["System.Int64"] = "long",
        ["System.UInt64"] = "ulong",
        ["System.Boolean"] = "bool",
        ["System.Single"] = "float",
        ["System.Double"] = "double",
        ["System.SByte"] = "sbyte",
        ["System.Byte"] = "byte",
        ["System.Decimal"] = "decimal"
    };

    public static string TypeToString(TypeReference typeRef)
    {
        if (typeRef is GenericInstanceType genericInstance) return ConvertGenericType(genericInstance);

        if (typeRef.IsArray)
        {
            var arrayType = typeRef as ArrayType;
            return TypeToString(arrayType!.ElementType) + "[]";
        }

        var typeDef = typeRef.Resolve();
        return typeDef != null ? SystemToStringType(typeDef) : typeRef.Name;
    }

    private static string ConvertGenericType(GenericInstanceType genericInstance)
    {
        var baseType = genericInstance.ElementType.Name;

        if (baseType.Contains('`')) baseType = baseType[..baseType.IndexOf('`')];

        var genericArgs = genericInstance.GenericArguments.AsValueEnumerable().Select(TypeToString).JoinToString(", ");
        return $"{baseType}<{genericArgs}>";
    }

    public static string SystemToStringType(TypeDefinition typeDef)
    {
        var fullName = typeDef.FullName;
        if (TypeMap.TryGetValue(fullName, out var type))
            return type;

        var name = typeDef.Name;
        if (name.StartsWith("System."))
            Log.Global.LogUnknownSystemType(name);

        return name;
    }
}