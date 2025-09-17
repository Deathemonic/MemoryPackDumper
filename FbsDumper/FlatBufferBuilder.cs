using Mono.Cecil;

namespace FbsDumper;

public class FlatBufferBuilder
{
    public readonly long EndObject;
    public readonly Dictionary<long, MethodDefinition> Methods;
    public readonly long StartObject;

    public FlatBufferBuilder(ModuleDefinition flatBuffersDllModule)
    {
        Methods = [];
        var flatBufferBuilderType = flatBuffersDllModule.GetType("FlatBuffers.FlatBufferBuilder");
        foreach (var method in flatBufferBuilderType.Methods)
        {
            var rva = InstructionsParser.GetMethodRva(method);
            {
                switch (method.Name)
                {
                    case "StartObject":
                        StartObject = rva;
                        break;
                    case "EndObject":
                        EndObject = rva;
                        break;
                }
            }
            Methods.Add(rva, method);
        }
    }
}