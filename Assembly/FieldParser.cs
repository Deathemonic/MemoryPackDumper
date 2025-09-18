using FbsDumper.CLI;
using Mono.Cecil;

namespace FbsDumper.Assembly;

public static class FieldParser
{
    public static TypeDefinition ProcessOffsets(TypeDefinition targetType, TypeDefinition fieldType, FlatField field,
        string fieldName, ref TypeReference fieldTypeRef)
    {
        switch (fieldType.FullName)
        {
            case "FlatBuffers.StringOffset":
                field.Type = targetType.Module.TypeSystem.String.Resolve();
                field.Name = fieldName.EndsWith("Offset")
                    ? new string([.. fieldName.SkipLast("Offset".Length)])
                    : fieldName;
                field.Name = TypeHelper.CleanFieldName(field.Name); // Needed for BA
                break;

            case "FlatBuffers.VectorOffset":
            case "FlatBuffers.Offset":
                var newFieldName = fieldName.EndsWith("Offset")
                    ? new string([.. fieldName.SkipLast("Offset".Length)])
                    : fieldName;
                newFieldName = TypeHelper.CleanFieldName(newFieldName); // Needed for BA

                var method = targetType.Methods.First(m =>
                    m.Name.Equals(newFieldName, StringComparison.CurrentCultureIgnoreCase)
                );
                
                var typeDefinition = method.ReturnType.Resolve();
                
                field.IsArray = fieldType.FullName == "FlatBuffers.VectorOffset";
                
                fieldType = typeDefinition;
                fieldTypeRef = method.ReturnType;

                field.Type = typeDefinition;
                field.Name = method.Name;
                break;
        }

        return fieldType;
    }
    
    public static TypeReference ForceProcessOffsets(TypeDefinition targetType, TypeDefinition fieldType, FlatField field,
        string fieldName, TypeReference fieldTypeRef, MethodDefinition method)
    {
        switch (fieldType.FullName)
        {
            case "FlatBuffers.StringOffset":
                field.Type = targetType.Module.TypeSystem.String.Resolve();
                field.Name = fieldName.EndsWith("Offset")
                    ? new string([.. fieldName.SkipLast("Offset".Length)])
                    : fieldName;
                break;
            case "FlatBuffers.VectorOffset":
            case "FlatBuffers.Offset":
                var newFieldName = fieldName.EndsWith("Offset")
                    ? new string([.. fieldName.SkipLast("Offset".Length)])
                    : fieldName;
                newFieldName = TypeHelper.CleanFieldName(newFieldName); // needed for BA

                if (fieldType.FullName == "FlatBuffers.VectorOffset")
                {
                    var startMethod = targetType.Methods.First(m => m.Name == $"Start{newFieldName}Vector");
                    fieldType = startMethod.Parameters[1].ParameterType.Resolve();
                    field.IsArray = true;
                }

                fieldTypeRef = fieldType;
                field.Type = fieldType;
                field.Name = method.Name;

                break;
        }

        return fieldTypeRef;
    }

    public static TypeReference ExtractGeneric(TypeReference fieldTypeRef, ref TypeDefinition fieldType)
    {
        if (fieldTypeRef is not GenericInstanceType genericInstance) return fieldTypeRef;
        fieldType = genericInstance.GenericArguments.First().Resolve();
        fieldTypeRef = genericInstance.GenericArguments.First();

        return fieldTypeRef;
    }

    public static TypeDefinition SetGeneric(TypeReference fieldTypeRef, TypeDefinition fieldType, FlatField field)
    {
        if (!fieldTypeRef.IsGenericInstance) return fieldType;

        var newGenericInstance = (GenericInstanceType)fieldTypeRef;
        fieldType = newGenericInstance.GenericArguments.First().Resolve();
        fieldTypeRef = newGenericInstance.GenericArguments.First();
        field.Type = fieldType;

        return fieldType;
    }

    public static void SaveEnum(FlatField field, TypeDefinition fieldType)
    {
        if (field.Type.IsEnum && !Parser.FlatEnumsToAdd.Contains(fieldType))
            Parser.FlatEnumsToAdd.Add(fieldType);
    }
}