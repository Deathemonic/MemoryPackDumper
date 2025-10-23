using MemoryPackDumper.CLI;
using Mono.Cecil;
using ZLinq;

namespace MemoryPackDumper.Assembly;

public static class TypeReferenceTracker
{
    public static void TrackReferencedType(TypeReference typeRef, HashSet<string> discoveredTypes)
    {
        var typeDef = typeRef.Resolve();
        if (typeDef == null)
            return;

        if (typeDef.IsEnum && !Parser.MemoryPackEnumsToAdd.Contains(typeDef))
        {
            Parser.MemoryPackEnumsToAdd.Add(typeDef);
            return;
        }

        if (IsMemoryPackable(typeDef)) discoveredTypes.Add(typeDef.FullName);

        if (typeRef is not GenericInstanceType genericInstance) return;
        foreach (var genericArg in genericInstance.GenericArguments.AsValueEnumerable())
            TrackReferencedType(genericArg, discoveredTypes);
    }

    private static bool IsMemoryPackable(TypeDefinition typeDef)
    {
        return typeDef.CustomAttributes.AsValueEnumerable().Any(a => a.AttributeType.Name == "MemoryPackableAttribute");
    }
}