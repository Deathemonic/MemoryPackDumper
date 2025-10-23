using Mono.Cecil;
using Mono.Collections.Generic;
using ZLinq;

namespace MemoryPackDumper.Assembly;

public static class AttributeExtractor
{
    public static void ExtractClassAttributes(TypeDefinition typeDef, MemoryPackClass memoryPackClass)
    {
        foreach (var attr in typeDef.CustomAttributes)
            switch (attr.AttributeType.Name)
            {
                case "MemoryPackableAttribute":
                    ExtractMemoryPackableAttribute(attr, memoryPackClass);
                    break;

                case "MemoryPackUnionAttribute":
                    ExtractMemoryPackUnionAttribute(attr, memoryPackClass);
                    break;

                default:
                    var attrName = GetAttributeShortName(attr.AttributeType.Name);
                    memoryPackClass.Attributes.Add(attrName);
                    break;
            }
    }

    private static void ExtractMemoryPackableAttribute(CustomAttribute attr, MemoryPackClass memoryPackClass)
    {
        if (attr.ConstructorArguments.Count > 0 && attr.ConstructorArguments[0].Value != null)
            memoryPackClass.GenerateType = EnumMapper.MapGenerateType(attr.ConstructorArguments[0].Value.ToString()!);

        if (attr.ConstructorArguments.Count > 1 && attr.ConstructorArguments[1].Value != null)
        {
            memoryPackClass.SerializeLayout =
                EnumMapper.MapSerializeLayout(attr.ConstructorArguments[1].Value.ToString()!);
        }
        else
        {
            var layoutProp = attr.Properties.FirstOrDefault(p => p.Name == "SerializeLayout");
            if (layoutProp.Argument.Value != null)
                memoryPackClass.SerializeLayout = EnumMapper.MapSerializeLayout(layoutProp.Argument.Value.ToString()!);
        }
    }

    private static void ExtractMemoryPackUnionAttribute(CustomAttribute attr, MemoryPackClass memoryPackClass)
    {
        if (attr.ConstructorArguments.Count < 2) return;

        var tag = Convert.ToInt32(attr.ConstructorArguments[0].Value);
        if (attr.ConstructorArguments[1].Value is TypeReference typeRef)
            memoryPackClass.Unions.Add(new MemoryPackUnion(tag, typeRef.Name));
    }

    public static void ExtractMemberAttributes(Collection<CustomAttribute> attributes, MemoryPackMember member)
    {
        foreach (var attr in attributes)
            switch (attr.AttributeType.Name)
            {
                case "MemoryPackIgnoreAttribute":
                    member.IsIgnored = true;
                    break;

                case "MemoryPackIncludeAttribute":
                    member.IsInclude = true;
                    break;

                case "MemoryPackOrderAttribute" when attr.ConstructorArguments.Count > 0:
                    member.Order = Convert.ToInt32(attr.ConstructorArguments[0].Value);
                    break;

                case "SuppressDefaultInitializationAttribute":
                    member.SuppressDefaultInitialization = true;
                    break;

                case "MemoryPackAllowSerializeAttribute":
                    member.AllowSerialize = true;
                    break;

                case "IsReadOnlyAttribute":
                case "System.Runtime.CompilerServices.IsReadOnlyAttribute":
                    member.IsReadOnly = true;
                    break;

                default:
                    if (attr.AttributeType.Name.Contains("Formatter"))
                    {
                        var formatterName = GetAttributeShortName(attr.AttributeType.Name);
                        member.CustomFormatters.Add(formatterName);
                    }

                    break;
            }
    }

    public static void ExtractMethodAttributes(MethodDefinition methodDef, MemoryPackMethod method)
    {
        foreach (var attrName in methodDef.CustomAttributes.AsValueEnumerable()
                     .Select(attr => GetAttributeShortName(attr.AttributeType.Name)))
            method.Attributes.Add(attrName);
    }

    private static string GetAttributeShortName(string attributeName)
    {
        return attributeName.EndsWith("Attribute")
            ? attributeName[..^"Attribute".Length]
            : attributeName;
    }
}