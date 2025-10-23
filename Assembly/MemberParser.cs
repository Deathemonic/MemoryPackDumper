using FbsDumper.CLI;
using FbsDumper.Helpers;
using Mono.Cecil;

namespace FbsDumper.Assembly;

public static class MemberParser
{
    public static MemoryPackClass TypeToMemoryPackClass(TypeDefinition typeDef, HashSet<string> discoveredTypes)
    {
        var className = typeDef.Name;
        var typeKeyword = TypeHelper.GetTypeKeyword(typeDef);
        var memoryPackClass = new MemoryPackClass(className, typeKeyword);

        memoryPackClass.IsRecord = typeDef.CustomAttributes.Any(a => 
            a.AttributeType.FullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute") ||
            typeDef.BaseType?.Name == "Record";

        ExtractClassAttributes(typeDef, memoryPackClass);

        foreach (var property in typeDef.Properties.Where(p => 
            p.GetMethod != null && 
            !p.GetMethod.IsStatic))
        {
            var member = CreateMemberFromProperty(property);
            if (member != null)
            {
                bool shouldInclude = member.IsInclude || (!member.IsIgnored && property.GetMethod.IsPublic);
                if (shouldInclude)
                {
                    memoryPackClass.Members.Add(member);
                    TrackReferencedType(member.Type, discoveredTypes);
                }
            }
        }

        foreach (var field in typeDef.Fields.Where(f => 
            !f.IsStatic && 
            !f.IsLiteral))
        {
            var member = CreateMemberFromField(field);
            if (member != null)
            {
                bool shouldInclude = member.IsInclude || (!member.IsIgnored && field.IsPublic);
                if (shouldInclude)
                {
                    memoryPackClass.Members.Add(member);
                    TrackReferencedType(member.Type, discoveredTypes);
                }
            }
        }

        if (memoryPackClass.Members.Any(m => m.Order.HasValue))
        {
            memoryPackClass.Members.Sort((a, b) =>
            {
                if (a.Order.HasValue && b.Order.HasValue)
                    return a.Order.Value.CompareTo(b.Order.Value);
                if (a.Order.HasValue)
                    return -1;
                if (b.Order.HasValue)
                    return 1;
                return 0;
            });
        }

        foreach (var method in typeDef.Methods)
        {
            if (method.IsConstructor && method.CustomAttributes.Any(a => a.AttributeType.Name == "MemoryPackConstructorAttribute"))
            {
                var memMethod = CreateMethodFromDefinition(method);
                memMethod.IsConstructor = true;
                memoryPackClass.Methods.Add(memMethod);
            }
            else if (method.CustomAttributes.Any(a => 
                a.AttributeType.Name == "MemoryPackOnSerializingAttribute" ||
                a.AttributeType.Name == "MemoryPackOnSerializedAttribute" ||
                a.AttributeType.Name == "MemoryPackOnDeserializingAttribute" ||
                a.AttributeType.Name == "MemoryPackOnDeserializedAttribute"))
            {
                var memMethod = CreateMethodFromDefinition(method);
                memoryPackClass.Methods.Add(memMethod);
            }
            else if (method.IsStatic && method.Name == "StaticConstructor" && 
                     method.ReturnType.FullName == "System.Void" &&
                     method.Parameters.Count == 0)
            {
                var memMethod = CreateMethodFromDefinition(method);
                memoryPackClass.Methods.Add(memMethod);
            }
        }

        return memoryPackClass;
    }

    private static MemoryPackMethod CreateMethodFromDefinition(MethodDefinition methodDef)
    {
        var returnType = methodDef.ReturnType.Name;
        var method = new MemoryPackMethod(
            methodDef.Name,
            returnType,
            methodDef.IsStatic,
            methodDef.IsPublic
        );

        foreach (var attr in methodDef.CustomAttributes)
        {
            var attrName = attr.AttributeType.Name;
            if (attrName.EndsWith("Attribute"))
                attrName = attrName[..^"Attribute".Length];
            method.Attributes.Add(attrName);
        }

        foreach (var param in methodDef.Parameters)
        {
            method.Parameters.Add((param.ParameterType.Name, param.Name));
        }

        return method;
    }

    private static void ExtractClassAttributes(TypeDefinition typeDef, MemoryPackClass memoryPackClass)
    {
        foreach (var attr in typeDef.CustomAttributes)
        {
            if (attr.AttributeType.Name == "MemoryPackableAttribute")
            {
                if (attr.ConstructorArguments.Count > 0)
                {
                    var generateTypeValue = attr.ConstructorArguments[0].Value;
                    if (generateTypeValue != null)
                    {
                        memoryPackClass.GenerateType = MapGenerateType(generateTypeValue.ToString()!);
                    }
                }

                if (attr.ConstructorArguments.Count > 1)
                {
                    var serializeLayoutValue = attr.ConstructorArguments[1].Value;
                    if (serializeLayoutValue != null)
                    {
                        memoryPackClass.SerializeLayout = MapSerializeLayout(serializeLayoutValue.ToString()!);
                    }
                }
                else if (attr.Properties.Count > 0)
                {
                    var layoutProp = attr.Properties.FirstOrDefault(p => p.Name == "SerializeLayout");
                    if (layoutProp.Argument.Value != null)
                    {
                        memoryPackClass.SerializeLayout = MapSerializeLayout(layoutProp.Argument.Value.ToString()!);
                    }
                }
                continue;
            }

            if (attr.AttributeType.Name == "MemoryPackUnionAttribute")
            {
                if (attr.ConstructorArguments.Count >= 2)
                {
                    var tag = Convert.ToInt32(attr.ConstructorArguments[0].Value);
                    var typeRef = attr.ConstructorArguments[1].Value as TypeReference;
                    if (typeRef != null)
                    {
                        memoryPackClass.Unions.Add(new MemoryPackUnion(tag, typeRef.Name));
                    }
                }
                continue;
            }

            var attrName = attr.AttributeType.Name;
            if (attrName.EndsWith("Attribute"))
                attrName = attrName[..^"Attribute".Length];

            memoryPackClass.Attributes.Add(attrName);
        }
    }

    private static MemoryPackMember? CreateMemberFromProperty(PropertyDefinition property)
    {
        var member = new MemoryPackMember(property.Name, property.PropertyType, false);
        ExtractMemberAttributes(property.CustomAttributes, member);
        return member;
    }

    private static MemoryPackMember? CreateMemberFromField(FieldDefinition field)
    {
        var member = new MemoryPackMember(field.Name, field.FieldType, true);
        ExtractMemberAttributes(field.CustomAttributes, member);
        return member;
    }

    private static void ExtractMemberAttributes(Mono.Collections.Generic.Collection<CustomAttribute> attributes, MemoryPackMember member)
    {
        foreach (var attr in attributes)
        {
            switch (attr.AttributeType.Name)
            {
                case "MemoryPackIgnoreAttribute":
                    member.IsIgnored = true;
                    break;

                case "MemoryPackIncludeAttribute":
                    member.IsInclude = true;
                    break;

                case "MemoryPackOrderAttribute":
                    if (attr.ConstructorArguments.Count > 0)
                    {
                        member.Order = Convert.ToInt32(attr.ConstructorArguments[0].Value);
                    }
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
                        var formatterName = attr.AttributeType.Name;
                        if (formatterName.EndsWith("Attribute"))
                            formatterName = formatterName[..^"Attribute".Length];
                        member.CustomFormatters.Add(formatterName);
                    }
                    break;
            }
        }
    }

    private static void TrackReferencedType(TypeReference typeRef, HashSet<string> discoveredTypes)
    {
        var typeDef = typeRef.Resolve();
        if (typeDef == null)
            return;

        if (typeDef.IsEnum && !Parser.MemoryPackEnumsToAdd.Contains(typeDef))
        {
            Parser.MemoryPackEnumsToAdd.Add(typeDef);
            return;
        }

        if (IsMemoryPackable(typeDef))
        {
            discoveredTypes.Add(typeDef.FullName);
        }

        if (typeRef is GenericInstanceType genericInstance)
        {
            foreach (var genericArg in genericInstance.GenericArguments)
            {
                TrackReferencedType(genericArg, discoveredTypes);
            }
        }
    }

    private static bool IsMemoryPackable(TypeDefinition typeDef)
    {
        return typeDef.CustomAttributes.Any(a => a.AttributeType.Name == "MemoryPackableAttribute");
    }

    public static MemoryPackEnum TypeToEnum(TypeDefinition typeDef)
    {
        var underlyingType = typeDef.Fields.FirstOrDefault(f => f.Name == "value__")?.FieldType.Resolve();
        if (underlyingType == null)
        {
            Log.Warning($"Could not determine underlying type for enum {typeDef.FullName}");
            underlyingType = typeDef.Module.TypeSystem.Int32.Resolve();
        }

        var memoryPackEnum = new MemoryPackEnum(underlyingType, typeDef.Name);

        foreach (var fieldDef in typeDef.Fields.Where(f => f.HasConstant))
        {
            var enumField = new MemoryPackEnumField(fieldDef.Name, Convert.ToInt64(fieldDef.Constant));
            memoryPackEnum.Fields.Add(enumField);
        }

        return memoryPackEnum;
    }

    private static string MapGenerateType(string value)
    {
        return value switch
        {
            "0" => "Object",
            "1" => "VersionTolerant",
            "2" => "CircularReference",
            "3" => "Collection",
            "4" => "NoGenerate",
            _ => value
        };
    }

    private static string MapSerializeLayout(string value)
    {
        return value switch
        {
            "0" => "Sequential",
            "1" => "Explicit",
            _ => value
        };
    }
}

