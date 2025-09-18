using FbsDumper.Instructions;
using Mono.Cecil;

namespace FbsDumper.Assembly;

public class FlatBuilder
{
    public readonly long EndObject;
    public readonly Dictionary<long, MethodDefinition> Methods;
    public readonly long StartObject;

    public FlatBuilder(ModuleDefinition flatBuffersDllModule)
    {
        var flatBufferBuilderType = flatBuffersDllModule.GetType("FlatBuffers.FlatBufferBuilder");

        var methodsWithRva = flatBufferBuilderType.Methods
            .Select(method => new { Method = method, Rva = InstructionsParser.GetMethodRva(method) })
            .ToArray();

        Methods = methodsWithRva.ToDictionary(x => x.Rva, x => x.Method);

        StartObject = methodsWithRva.First(x => x.Method.Name == "StartObject").Rva;
        EndObject = methodsWithRva.First(x => x.Method.Name == "EndObject").Rva;
    }
}