using System.Globalization;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using static FbsDumper.Parser;

namespace FbsDumper;

internal class TypeHelper
{
    private readonly InstructionsParser _instructionsResolver = new(GameAssemblyPath);

    public static List<TypeDefinition> GetAllFlatBufferTypes(ModuleDefinition module, string baseTypeName)
    {
        List<TypeDefinition> ret =
        [
            .. module.GetTypes().Where(t =>
                    t.HasInterfaces &&
                    t.Interfaces.Any(i => i.InterfaceType.FullName == baseTypeName)
                //  && t.FullName == "MX.Data.Excel.MinigameRoadPuzzleMapExcel"
            )
        ];

        if (!string.IsNullOrEmpty(NameSpace2LookFor)) ret = ret.Where(t => t.Namespace == NameSpace2LookFor).ToList();

        // Dedupe
        ret =
        [
            .. ret
                .GroupBy(t => t.Name)
                .Select(g => g.First())
        ];

        // todo: check nested types

        return ret;
    }

    public FlatTable Type2Table(TypeDefinition targetType)
    {
        var typeName = targetType.Name;
        var ret = new FlatTable(typeName);

        var createMethod = targetType.Methods.FirstOrDefault(m =>
            m.Name == $"Create{typeName}" &&
            m.Parameters.Count > 1 &&
            m.Parameters.First().Name == "builder" &&
            m is { IsStatic: true, IsPublic: true }
        );

        if (createMethod == null)
        {
            Log.Warning($"{targetType.FullName} does NOT contain a Create{typeName} function. Fields will be empty");

            ret.NoCreate = true;
            return ret;
        }

        ProcessFields(ref ret, createMethod, targetType);

        return ret;
    }

    private void ProcessFields(ref FlatTable ret, MethodDefinition createMethod, TypeDefinition targetType)
    {
        var dict = ParseCalls4CreateMethod(createMethod, targetType);
        dict = dict.OrderBy(t => t.Key).ToDictionary();

        foreach (var kvp in dict)
        {
            var methodDef = kvp.Value;
            var param = methodDef.Parameters[1];
            var fieldType = param.ParameterType.Resolve();
            var fieldTypeRef = param.ParameterType;
            var fieldName = param.Name;

            if (fieldTypeRef is GenericInstanceType genericInstance)
            {
                // GenericInstanceType genericInstance = (GenericInstanceType)fieldTypeRef;
                fieldType = genericInstance.GenericArguments.First().Resolve();
                fieldTypeRef = genericInstance.GenericArguments.First();
            }

            var field = new FlatField(fieldType, fieldName.Replace("_", ""))
            {
                Offset = kvp.Key
            }; // needed for BA


            switch (fieldType.FullName)
            {
                case "FlatBuffers.StringOffset":
                    field.Type = targetType.Module.TypeSystem.String.Resolve();
                    field.Name = fieldName.EndsWith("Offset")
                        ? new string([.. fieldName.SkipLast("Offset".Length)])
                        : fieldName;
                    field.Name = field.Name.Replace("_", ""); // needed for BA
                    break;
                case "FlatBuffers.VectorOffset":
                case "FlatBuffers.Offset":
                    var newFieldName = fieldName.EndsWith("Offset")
                        ? new string([.. fieldName.SkipLast("Offset".Length)])
                        : fieldName;
                    newFieldName = newFieldName.Replace("_", ""); // needed for BA

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

            if (fieldTypeRef.IsGenericInstance)
            {
                var newGenericInstance = (GenericInstanceType)fieldTypeRef;
                fieldType = newGenericInstance.GenericArguments.First().Resolve();
                fieldTypeRef = newGenericInstance.GenericArguments.First();
                field.Type = fieldType;
            }

            if (field.Type.IsEnum && !FlatEnumsToAdd.Contains(fieldType)) FlatEnumsToAdd.Add(fieldType);

            ret.Fields.Add(field);
        }
    }

    private Dictionary<int, MethodDefinition> ParseCalls4CreateMethod(MethodDefinition createMethod,
        TypeDefinition targetType)
    {
        var ret = new Dictionary<int, MethodDefinition>();
        var typeMethods = new Dictionary<long, MethodDefinition>();

        foreach (var method in targetType.GetMethods())
        {
            var rva = InstructionsParser.GetMethodRva(method);
            typeMethods.Add(rva, method);
        }

        var instructions = _instructionsResolver.GetInstructions(createMethod);
        var analyzer = InstructionsAnalyzer.GetAnalyzer(_instructionsResolver.Architecture);
        var calls = analyzer.AnalyzeCalls(instructions);
        var hasStarted = false;
        var max = 0;
        var cur = 0;

        var endMethod = targetType.Methods.First(m => m.Name == $"End{targetType.Name}");
        var endMethodRva = InstructionsParser.GetMethodRva(endMethod);

        foreach (var call in calls)
        {
            if (string.IsNullOrEmpty(call.Target))
            {
                Log.Warning($"Empty call target found at address 0x{call.Address:X}");
                continue;
            }

            long target;
            if (call.Target.StartsWith("0x"))
            {
                var targetHex = call.Target[2..];
                if (string.IsNullOrEmpty(targetHex) ||
                    !long.TryParse(targetHex, NumberStyles.HexNumber, null, out target))
                {
                    Log.Warning($"Failed to parse hex value '{call.Target}' at address 0x{call.Address:X}");
                    continue;
                }
            }
            else if (call.Target.StartsWith('#'))
            {
                var targetDecimal = call.Target[1..];
                if (string.IsNullOrEmpty(targetDecimal) ||
                    !long.TryParse(targetDecimal, NumberStyles.Integer, null, out target))
                {
                    Log.Warning($"Failed to parse decimal value '{call.Target}' at address 0x{call.Address:X}");
                    continue;
                }
            }
            else
            {
                Log.Warning(
                    $"Invalid call target format '{call.Target}' at address 0x{call.Address:X} - expected 0x or # prefix");
                continue;
            }

            switch (target)
            {
                case var addr when addr == Parser.FlatBufferBuilder.StartObject:
                    hasStarted = true;
                    var cnt = 0;
                    
                    if (call.Args.TryGetValue("w1", out var arg1) && arg1.StartsWith('#'))
                    {
                        var argValue = arg1[1..];
                        int.TryParse(argValue, NumberStyles.Integer, null, out cnt);
                    }
                    else if (call.EdxValue != null)
                    {
                        var edxValue = call.EdxValue;
                        if (edxValue.StartsWith("0x"))
                        {
                            int.TryParse(edxValue[2..], NumberStyles.HexNumber, null, out cnt);
                        }
                        else
                        {
                            int.TryParse(edxValue, NumberStyles.Integer, null, out cnt);
                        }
                    }

                    max = cnt;
                    Log.Debug($"Has started, instance will have {cnt} fields");
                    break;
                case var addr when addr == Parser.FlatBufferBuilder.EndObject:
                    Log.Debug("Has ended");
                    return ret;
                case var addr when addr == endMethodRva:
                    Log.Debug("Stop");
                    return ret;
                default:
                    if (!hasStarted)
                        Log.Global.LogSkippingCall((ulong)target, "StartObject hasn't been called yet");
                    if (!typeMethods.TryGetValue(target, out var method))
                    {
                        Log.Global.LogSkippingCall((ulong)target, $"it's not part of the {targetType.FullName}");
                        continue;
                    }

                    if (cur >= max)
                    {
                        Log.Global.LogSkippingCall((ulong)target, "max amount of fields has been reached");
                        continue;
                    }

                    var index = ParseCalls4AddMethod(method);
                    ret.Add(index, method);
                    cur += 1;
                    continue;
            }
        }

        return ret;
    }

    private int ParseCalls4AddMethod(MethodDefinition createMethod)
    {
        var instructions = _instructionsResolver.GetInstructions(createMethod);
        var analyzer = InstructionsAnalyzer.GetAnalyzer(_instructionsResolver.Architecture);
        var calls = analyzer.AnalyzeCalls(instructions);
        var call = calls.First(m =>
        {
            if (string.IsNullOrEmpty(m.Target))
                return false;

            long target;
            if (m.Target.StartsWith("0x"))
            {
                var targetHex = m.Target[2..];
                if (!long.TryParse(targetHex, NumberStyles.HexNumber, null, out target))
                    return false;
            }
            else if (m.Target.StartsWith('#'))
            {
                var targetDecimal = m.Target[1..];
                if (!long.TryParse(targetDecimal, NumberStyles.Integer, null, out target))
                    return false;
            }
            else
            {
                return false;
            }

            return Parser.FlatBufferBuilder.Methods.ContainsKey(target);
        });
        var cnt = 0;
        
        if (call.Args.TryGetValue("w1", out var arg1) && arg1.StartsWith('#'))
        {
            var argValue = arg1[1..];
            int.TryParse(argValue, NumberStyles.Integer, null, out cnt);
        }
        else if (call.EdxValue != null)
        {
            var edxValue = call.EdxValue;
            if (edxValue.StartsWith("0x"))
            {
                int.TryParse(edxValue[2..], NumberStyles.HexNumber, null, out cnt);
            }
            else
            {
                int.TryParse(edxValue, NumberStyles.Integer, null, out cnt);
            }
        }

        Log.Debug($"Index is {cnt}");

        return cnt;
    }

    public static FlatEnum Type2Enum(TypeDefinition typeDef)
    {
        var retType = typeDef.GetEnumUnderlyingType().Resolve();
        var ret = new FlatEnum(retType, typeDef.Name);

        foreach (var fieldDef in typeDef.Fields.Where(f => f.HasConstant))
        {
            var enumField = new FlatEnumField(fieldDef.Name, Convert.ToInt64(fieldDef.Constant));
            ret.Fields.Add(enumField);
        }

        return ret;
    }
}