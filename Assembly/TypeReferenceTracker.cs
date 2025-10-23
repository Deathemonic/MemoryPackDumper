using FbsDumper.CLI;
using Mono.Cecil;

namespace FbsDumper.Assembly;

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

        if (IsMemoryPackable(typeDef))
        {
            discoveredTypes.Add(typeDef.FullName);
        }

        if (typeRef is GenericInstanceType genericInstance)
        {
            foreach (var genericArg in genericInstance.GenericArguments)
            {
                TrackReferencedType(genericArg, discoveredTypes);
            }
        }
    }

    private static bool IsMemoryPackable(TypeDefinition typeDef)
    {
        return typeDef.CustomAttributes.Any(a => a.AttributeType.Name == "MemoryPackableAttribute");
    }
}

