using Microsoft.VisualBasic.FileIO;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace FbsDumper;

internal class TypeHelper
{
    public static List<TypeDefinition> GetAllFlatBufferTypes(ModuleDefinition module, string baseTypeName)
    {
        List<TypeDefinition> ret = module.GetTypes().Where(t =>
            t.HasInterfaces &&
            t.Interfaces.Any(i => i.InterfaceType.FullName == baseTypeName)
		).ToList();

        if (!String.IsNullOrEmpty(MainApp.NameSpace2LookFor))
        {
            ret = ret.Where(t => t.Namespace == MainApp.NameSpace2LookFor).ToList();
        }

        // Dedupe
		ret = ret
	        .GroupBy(t => t.Name)
	        .Select(g => g.First())
	        .ToList();

		// todo: check nested types

		return ret;
    }

    public static FlatTable? Type2Table(TypeDefinition targetType)
    {
        string typeName = targetType.Name;
        FlatTable ret = new FlatTable(typeName);

        MethodDefinition? createMethod = targetType.Methods.FirstOrDefault(m =>
            m.Name == $"Create{typeName}" &&
            m.Parameters.Count > 1 &&
            m.Parameters.First().Name == "builder" &&
            m.IsStatic &&
            m.IsPublic
        );

        if (createMethod == null)
        {
            if (MainApp.ForceDump == true)
            {
                ForceProcessFields(ref ret, targetType);
                return ret;
                // return null;
            }
            Console.WriteLine($"[ERR] {targetType.FullName} does NOT contain a Create{typeName} function, skipping...");
            return null;
        }
        
        ProcessFields(ref ret, createMethod, targetType);

        return ret;
    }

    private static void ForceProcessFields(ref FlatTable ret, TypeDefinition targetType)
    {
        foreach (MethodDefinition method in targetType.GetMethods().Where(m =>
            m.IsPublic &&
            m.IsStatic &&
            m.Name.StartsWith("Add") &&
            m.HasParameters &&
            m.Parameters.Count == 2 &&
            m.Parameters.First().Name == "builder"
        ))
        {
            // Console.WriteLine(method.Name);

            ParameterDefinition param = method.Parameters[1];

            TypeDefinition fieldType = param.ParameterType.Resolve();
            TypeReference fieldTypeRef = param.ParameterType;
            string fieldName = param.Name;

            if (fieldTypeRef is GenericInstanceType genericInstance)
            {
                // GenericInstanceType genericInstance = (GenericInstanceType)fieldTypeRef;
                fieldType = genericInstance.GenericArguments.First().Resolve();
                fieldTypeRef = genericInstance.GenericArguments.First();
            }

            FlatField field = new FlatField(fieldType, fieldName);

            switch (fieldType.FullName)
            {
                case "FlatBuffers.StringOffset":
                    field.type = targetType.Module.TypeSystem.String.Resolve();
                    field.name = fieldName.EndsWith("Offset") ?
                                    new string(fieldName.SkipLast("Offset".Length).ToArray()) :
                                    fieldName;
                    break;
                case "FlatBuffers.VectorOffset":
                case "FlatBuffers.Offset":
                    string newFieldName = fieldName.EndsWith("Offset") ?
                                    new string(fieldName.SkipLast("Offset".Length).ToArray()) :
                                    fieldName;
                    newFieldName = newFieldName.Replace("_", ""); // needed for BA

                    if (fieldType.FullName == "FlatBuffers.VectorOffset")
                    {
                        MethodDefinition startMethod = targetType.Methods.First(m => m.Name == $"Start{newFieldName}Vector");
                        fieldType = startMethod.Parameters[1].ParameterType.Resolve();
                        field.isArray = true;
                    }

                    fieldTypeRef = fieldType;
                    field.type = fieldType;
                    field.name = method.Name;

                    break;
                default:
                    break;

            }

            if (fieldTypeRef.IsGenericInstance)
            {
                GenericInstanceType newGenericInstance = (GenericInstanceType)fieldTypeRef;
                fieldType = newGenericInstance.GenericArguments.First().Resolve();
                fieldTypeRef = newGenericInstance.GenericArguments.First();
                field.type = fieldType;
            }

            ret.fields.Add(field);
        }
    }

    private static void ProcessFields(ref FlatTable ret, MethodDefinition createMethod, TypeDefinition targetType)
    {
        foreach (ParameterDefinition param in createMethod.Parameters.Skip(1))
        {
            TypeDefinition fieldType = param.ParameterType.Resolve();
            TypeReference fieldTypeRef = param.ParameterType;
            string fieldName = param.Name;

            if (fieldTypeRef is GenericInstanceType genericInstance)
            {
                // GenericInstanceType genericInstance = (GenericInstanceType)fieldTypeRef;
                fieldType = genericInstance.GenericArguments.First().Resolve();
                fieldTypeRef = genericInstance.GenericArguments.First();
            }

            FlatField field = new FlatField(fieldType, fieldName.Replace("_", "")); // needed for BA

            switch (fieldType.FullName)
            {
                case "FlatBuffers.StringOffset":
                    field.type = targetType.Module.TypeSystem.String.Resolve();
                    field.name = fieldName.EndsWith("Offset") ?
                                    new string(fieldName.SkipLast("Offset".Length).ToArray()) :
                                    fieldName;
                    field.name = field.name.Replace("_", ""); // needed for BA
                    break;
                case "FlatBuffers.VectorOffset":
                case "FlatBuffers.Offset":
                    string newFieldName = fieldName.EndsWith("Offset") ?
                                    new string(fieldName.SkipLast("Offset".Length).ToArray()) :
                                    fieldName;
                    newFieldName = newFieldName.Replace("_", ""); // needed for BA

                    MethodDefinition method = targetType.Methods.First(m =>
                        m.Name.ToLower() == newFieldName.ToLower()
                    );
                    TypeDefinition typeDefinition = method.ReturnType.Resolve();
                    field.isArray = fieldType.FullName == "FlatBuffers.VectorOffset";
                    fieldType = typeDefinition;
                    fieldTypeRef = method.ReturnType;

                    field.type = typeDefinition;
                    field.name = method.Name;
                    break;
                default:
                    break;

            }

            if (fieldTypeRef.IsGenericInstance)
            {
                GenericInstanceType newGenericInstance = (GenericInstanceType)fieldTypeRef;
                fieldType = newGenericInstance.GenericArguments.First().Resolve();
                fieldTypeRef = newGenericInstance.GenericArguments.First();
                field.type = fieldType;
            }

            if (field.type.IsEnum && !MainApp.flatEnumsToAdd.Contains(fieldType))
            {
                MainApp.flatEnumsToAdd.Add(fieldType);
            }

            ret.fields.Add(field);
        }
    }

    public static FlatEnum Type2Enum(TypeDefinition typeDef)
    {
        TypeDefinition retType = typeDef.GetEnumUnderlyingType().Resolve();
        FlatEnum ret = new FlatEnum(retType, typeDef.Name);

        foreach (FieldDefinition fieldDef in typeDef.Fields.Where(f => f.HasConstant))
        {
            FlatEnumField enumField = new FlatEnumField(fieldDef.Name, Convert.ToInt64(fieldDef.Constant));
            ret.fields.Add(enumField);
        }

        return ret;
    }
}
