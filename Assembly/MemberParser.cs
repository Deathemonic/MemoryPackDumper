using MemoryPackDumper.Helpers;
using Mono.Cecil;

namespace MemoryPackDumper.Assembly;

public static class MemberParser
{
    public static MemoryPackClass TypeToMemoryPackClass(TypeDefinition typeDef, HashSet<string> discoveredTypes)
    {
        var className = typeDef.Name;
        var typeKeyword = TypeHelper.GetTypeKeyword(typeDef);
        var memoryPackClass = new MemoryPackClass(className, typeKeyword)
        {
            IsRecord = IsRecordType(typeDef)
        };

        AttributeExtractor.ExtractClassAttributes(typeDef, memoryPackClass);

        ProcessProperties(typeDef, memoryPackClass, discoveredTypes);
        ProcessFields(typeDef, memoryPackClass, discoveredTypes);
        SortMembersByOrder(memoryPackClass);
        ProcessMethods(typeDef, memoryPackClass);

        return memoryPackClass;
    }

    private static bool IsRecordType(TypeDefinition typeDef)
    {
        return typeDef.CustomAttributes.Any(a =>
                   a.AttributeType.FullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute") ||
               typeDef.BaseType?.Name == "Record";
    }

    private static void ProcessProperties(TypeDefinition typeDef, MemoryPackClass memoryPackClass,
        HashSet<string> discoveredTypes)
    {
        foreach (var property in typeDef.Properties.Where(p => p.GetMethod is { IsStatic: false }))
        {
            var member = CreateMemberFromProperty(property);
            if (!ShouldIncludeMember(member, property.GetMethod.IsPublic)) continue;

            memoryPackClass.Members.Add(member);
            TypeReferenceTracker.TrackReferencedType(member.Type, discoveredTypes);
        }
    }

    private static void ProcessFields(TypeDefinition typeDef, MemoryPackClass memoryPackClass,
        HashSet<string> discoveredTypes)
    {
        foreach (var field in typeDef.Fields.Where(f => !f.IsStatic && !f.IsLiteral))
        {
            var member = CreateMemberFromField(field);
            if (!ShouldIncludeMember(member, field.IsPublic)) continue;

            memoryPackClass.Members.Add(member);
            TypeReferenceTracker.TrackReferencedType(member.Type, discoveredTypes);
        }
    }

    private static bool ShouldIncludeMember(MemoryPackMember member, bool isPublic)
    {
        return member.IsInclude || (!member.IsIgnored && isPublic);
    }

    private static void SortMembersByOrder(MemoryPackClass memoryPackClass)
    {
        if (!memoryPackClass.Members.Any(m => m.Order.HasValue)) return;

        memoryPackClass.Members.Sort((a, b) =>
        {
            return (a.Order, b.Order) switch
            {
                (not null, not null) => a.Order.Value.CompareTo(b.Order.Value),
                (not null, null) => -1,
                (null, not null) => 1,
                _ => 0
            };
        });
    }

    private static void ProcessMethods(TypeDefinition typeDef, MemoryPackClass memoryPackClass)
    {
        foreach (var method in typeDef.Methods)
        {
            if (!IsMemoryPackMethod(method)) continue;

            var memMethod = CreateMethodFromDefinition(method);
            memMethod.IsConstructor = IsMemoryPackConstructor(method);
            memoryPackClass.Methods.Add(memMethod);
        }
    }

    private static bool IsMemoryPackMethod(MethodDefinition method)
    {
        return IsMemoryPackConstructor(method) || IsCallbackMethod(method) || IsStaticConstructor(method);
    }

    private static bool IsMemoryPackConstructor(MethodDefinition method)
    {
        return method.IsConstructor &&
               method.CustomAttributes.Any(a => a.AttributeType.Name == "MemoryPackConstructorAttribute");
    }

    private static bool IsCallbackMethod(MethodDefinition method)
    {
        return method.CustomAttributes.Any(a =>
            a.AttributeType.Name == "MemoryPackOnSerializingAttribute" ||
            a.AttributeType.Name == "MemoryPackOnSerializedAttribute" ||
            a.AttributeType.Name == "MemoryPackOnDeserializingAttribute" ||
            a.AttributeType.Name == "MemoryPackOnDeserializedAttribute");
    }

    private static bool IsStaticConstructor(MethodDefinition method)
    {
        return method is { IsStatic: true, Name: "StaticConstructor" } &&
               method.ReturnType.FullName == "System.Void" &&
               method.Parameters.Count == 0;
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

        AttributeExtractor.ExtractMethodAttributes(methodDef, method);

        foreach (var param in methodDef.Parameters) method.Parameters.Add((param.ParameterType.Name, param.Name));

        return method;
    }

    private static MemoryPackMember CreateMemberFromProperty(PropertyDefinition property)
    {
        var member = new MemoryPackMember(property.Name, property.PropertyType, false);
        AttributeExtractor.ExtractMemberAttributes(property.CustomAttributes, member);
        return member;
    }

    private static MemoryPackMember CreateMemberFromField(FieldDefinition field)
    {
        var member = new MemoryPackMember(field.Name, field.FieldType, true);
        AttributeExtractor.ExtractMemberAttributes(field.CustomAttributes, member);
        return member;
    }

    public static MemoryPackEnum TypeToEnum(TypeDefinition typeDef)
    {
        var underlyingType = GetEnumUnderlyingType(typeDef);
        var memoryPackEnum = new MemoryPackEnum(underlyingType, typeDef.Name);

        foreach (var fieldDef in typeDef.Fields.Where(f => f.HasConstant))
        {
            var enumField = new MemoryPackEnumField(fieldDef.Name, Convert.ToInt64(fieldDef.Constant));
            memoryPackEnum.Fields.Add(enumField);
        }

        return memoryPackEnum;
    }

    private static TypeDefinition GetEnumUnderlyingType(TypeDefinition typeDef)
    {
        var underlyingType = typeDef.Fields.FirstOrDefault(f => f.Name == "value__")?.FieldType.Resolve();
        if (underlyingType != null) return underlyingType;

        Log.Warning($"Could not determine underlying type for enum {typeDef.FullName}");
        return typeDef.Module.TypeSystem.Int32.Resolve();
    }
}