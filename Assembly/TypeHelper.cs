using FbsDumper.CLI;
using Mono.Cecil;

namespace FbsDumper.Assembly;

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
        if (typeDef.IsAbstract && typeDef.IsSealed)
            return "static class";
        if (typeDef.IsAbstract)
            return "abstract class";
        if (typeDef.IsValueType && !typeDef.IsEnum)
            return "struct";
        return "class";
    }
}
