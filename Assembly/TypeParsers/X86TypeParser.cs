using System.Globalization;
using FbsDumper.CLI;
using FbsDumper.Helpers;
using FbsDumper.Instructions;
using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace FbsDumper.Assembly.TypeParsers;

internal class X86TypeParser : ITypeParser
{
    public void ProcessFields(ref FlatTable ret, MethodDefinition createMethod, TypeDefinition targetType)
    {
        Dictionary<int, ParameterDefinition> dict;

        try
        {
            dict = ParseCallsForCreateMethod(createMethod, targetType);
        }
        catch (Exception)
        {
            ForceProcessFields(ref ret, createMethod, targetType);
            return;
        }

        dict = dict.OrderBy(t => t.Key).ToDictionary();

        foreach (var kvp in dict)
        {
            var param = kvp.Value;
            var fieldType = param.ParameterType.Resolve();
            var fieldTypeRef = param.ParameterType;
            var fieldName = param.Name;

            fieldTypeRef = FieldParser.ExtractGeneric(fieldTypeRef, ref fieldType);

            FlatField field = new(fieldType, TypeHelper.CleanFieldName(fieldName))
            {
                Offset = kvp.Key
            };

            fieldType = FieldParser.ProcessOffsets(targetType, fieldType, field, fieldName, ref fieldTypeRef);
            fieldType = FieldParser.SetGeneric(fieldTypeRef, fieldType, field);

            FieldParser.SaveEnum(field, fieldType);

            ret.Fields.Add(field);
        }
    }

    private static void ForceProcessFields(ref FlatTable ret, MethodDefinition createMethod, TypeDefinition targetType)
    {
        foreach (var (param, offset) in createMethod.Parameters.Skip(1).Select((p, i) => (p, i + 1)))
        {
            var fieldType = param.ParameterType.Resolve();
            var fieldTypeRef = param.ParameterType;
            var fieldName = param.Name;

            fieldTypeRef = FieldParser.ExtractGeneric(fieldTypeRef, ref fieldType);

            FlatField field = new(fieldType, TypeHelper.CleanFieldName(fieldName))
            {
                Offset = offset
            };

            fieldType = FieldParser.ProcessOffsets(targetType, fieldType, field, fieldName, ref fieldTypeRef);
            fieldType = FieldParser.SetGeneric(fieldTypeRef, fieldType, field);

            FieldParser.SaveEnum(field, fieldType);

            ret.Fields.Add(field);
        }
    }

    private static Dictionary<int, ParameterDefinition> ParseCallsForCreateMethod(MethodDefinition createMethod,
        TypeDefinition targetType)
    {
        Dictionary<int, ParameterDefinition> ret = [];
        Dictionary<long, MethodDefinition> typeMethods = [];

        foreach (var method in createMethod.Parameters[0].ParameterType.Resolve().GetMethods())
        {
            var rva = InstructionsParser.GetMethodRva(method);
            typeMethods.Add(rva, method);
        }

        var calls = TypeHelper.GetAnalyzedCalls(createMethod);

        var hasStarted = false;
        var max = 0;
        var cur = 0;

        var endMethodRva = TypeHelper.GetEndMethodRva(targetType);

        if (calls.All(c => c.ArgIndex is null or 0)) return ret;

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
                    max = ParseEdxValue(call);

                    Log.Debug($"Has started, instance will have {max} fields");
                    break;

                case var _ when target == Parser.FlatBufferBuilder.EndObject:
                case var _ when target == endMethodRva:
                    return ret;

                default:
                    if (!hasStarted)
                        Log.Global.LogSkippingCall((ulong)target, "StartObject hasn't been called yet");

                    if (!typeMethods.TryGetValue(target, out _))
                    {
                        Log.Global.LogSkippingCall((ulong)target, $"it's not part of the {targetType.FullName}");
                        continue;
                    }

                    if (cur >= max)
                    {
                        Log.Global.LogSkippingCall((ulong)target, "max amount of fields has been reached");
                        continue;
                    }

                    var edxAsInt = ParseEdxValue(call);
                    ret.Add(edxAsInt, createMethod.Parameters[(int)call.ArgIndex! - 1]);
                    cur += 1;
                    break;
            }
        }

        return ret;
    }

    private static int ParseEdxValue(InstructionsAnalyzer.CallInfo call)
    {
        if (call.EdxValue == null) return 0;

        var edxValue = call.EdxValue;
        return edxValue.StartsWith("0x")
            ? int.Parse(edxValue[2..], NumberStyles.HexNumber)
            : int.Parse(edxValue, NumberStyles.Integer);
    }
}