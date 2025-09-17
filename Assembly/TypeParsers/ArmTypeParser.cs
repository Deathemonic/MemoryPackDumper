using System.Globalization;
using FbsDumper.CLI;
using FbsDumper.Helpers;
using FbsDumper.Instructions;
using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace FbsDumper.Assembly.TypeParsers;

internal class ArmTypeParser : ITypeParser
{
    public void ProcessFields(ref FlatTable ret, MethodDefinition createMethod, TypeDefinition targetType)
    {
        var dict = ParseCallsForCreateMethod(createMethod, targetType);
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
                fieldType = genericInstance.GenericArguments.First().Resolve();
                fieldTypeRef = genericInstance.GenericArguments.First();
            }

            var field = new FlatField(fieldType, TypeHelper.CleanFieldName(fieldName))
            {
                Offset = kvp.Key
            };

            switch (fieldType.FullName)
            {
                case "FlatBuffers.StringOffset":
                    field.Type = targetType.Module.TypeSystem.String.Resolve();
                    field.Name = fieldName.EndsWith("Offset")
                        ? new string([.. fieldName.SkipLast("Offset".Length)])
                        : fieldName;
                    field.Name = TypeHelper.CleanFieldName(field.Name);
                    break;

                case "FlatBuffers.VectorOffset":
                case "FlatBuffers.Offset":
                    var newFieldName = fieldName.EndsWith("Offset")
                        ? new string([.. fieldName.SkipLast("Offset".Length)])
                        : fieldName;
                    newFieldName = TypeHelper.CleanFieldName(newFieldName);

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

            if (field.Type.IsEnum && !Parser.FlatEnumsToAdd.Contains(fieldType))
                Parser.FlatEnumsToAdd.Add(fieldType);

            ret.Fields.Add(field);
        }
    }

    private static Dictionary<int, MethodDefinition> ParseCallsForCreateMethod(MethodDefinition createMethod,
        TypeDefinition targetType)
    {
        Dictionary<int, MethodDefinition> ret = [];
        Dictionary<long, MethodDefinition> typeMethods = [];
        
        foreach (var method in targetType.GetMethods())
        {
            var rva = InstructionsParser.GetMethodRva(method);
            typeMethods.Add(rva, method);
        }

        var calls = TypeHelper.GetAnalyzedCalls(createMethod);

        var hasStarted = false;
        var max = 0;
        var cur = 0;

        var endMethodRva = TypeHelper.GetEndMethodRva(targetType);

        foreach (var call in calls)
        {
            if (string.IsNullOrEmpty(call.Target))
            {
                Log.Warning($"Empty call target found at address 0x{call.Address:X}");
                continue;
            }

            if (!TypeHelper.TryParseTarget(call.Target, out var target))
            {
                Log.Warning($"Failed to parse call target '{call.Target}' at address 0x{call.Address:X}");
                continue;
            }

            switch (target)
            {
                case var _ when target == Parser.FlatBufferBuilder.StartObject:
                    hasStarted = true;

                    var cnt = ParseArgument(call, "w1");

                    max = cnt;

                    Log.Debug($"Has started, instance will have {cnt} fields");
                    break;

                case var _ when target == Parser.FlatBufferBuilder.EndObject:
                case var _ when target == endMethodRva:
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

                    var index = ParseCallsForAddMethod(method);
                    ret.Add(index, method);
                    cur += 1;
                    break;
            }
        }

        return ret;
    }

    private static int ParseCallsForAddMethod(MethodDefinition createMethod)
    {
        var instructions = TypeHelper.InstructionsResolver.GetInstructions(createMethod);
        var analyzer = InstructionsAnalyzer.GetAnalyzer(TypeHelper.InstructionsResolver.Architecture);
        var calls = analyzer.AnalyzeCalls(instructions);
        var call = calls.First(m =>
        {
            if (string.IsNullOrEmpty(m.Target)) return false;

            return TypeHelper.TryParseTarget(m.Target, out var target) &&
                   Parser.FlatBufferBuilder.Methods.ContainsKey(target);
        });

        var cnt = ParseArgument(call, "w1");

        Log.Debug($"Index is {cnt}");
        return cnt;
    }

    private static int ParseArgument(InstructionsAnalyzer.CallInfo call, string argName)
    {
        if (!call.Args.TryGetValue(argName, out var arg) || !arg.StartsWith('#'))
            return 0;

        var argValue = arg[1..];
        return int.TryParse(argValue, NumberStyles.Integer, null, out var cnt) ? cnt : 0;
    }
}