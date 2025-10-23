using MemoryPackDumper.CLI;
using Mono.Cecil;

namespace MemoryPackDumper.Assembly;

internal static class TypeHelper
{
    public static List<TypeDefinition> GetAllMemoryPackableTypes(ModuleDefinition module)
    {
        List<TypeDefinition> ret =
        [
            .. module.GetTypes().Where(t =>
                t.CustomAttributes.Any(a => a.AttributeType.Name == "MemoryPackableAttribute")
            )
        ];

        if (!string.IsNullOrEmpty(Parser.NameSpace2LookFor))
            ret = [.. ret.Where(t => t.Namespace == Parser.NameSpace2LookFor)];

        // Dedupe
        ret = [..ret.DistinctBy(t => t.FullName)];

        return ret;
    }

    public static string GetTypeKeyword(TypeDefinition typeDef)
    {
        if (typeDef.IsInterface)
            return "interface";
        if (typeDef is { IsAbstract: true, IsSealed: true })
            return "static class";
        if (typeDef.IsAbstract)
            return "abstract class";
        if (typeDef is { IsValueType: true, IsEnum: false })
            return "struct";
        return "class";
    }
}