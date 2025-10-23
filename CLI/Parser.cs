using System.Buffers;
using FbsDumper.Assembly;
using FbsDumper.Helpers;
using Mono.Cecil;
using Utf8StringInterpolation;

namespace FbsDumper.CLI;

public static partial class Parser
{
    private static string _dummyAssemblyDir = "DummyDll";
    private static string _outputFileName = "MemoryPack.cs";
    private static string? _customNameSpace = "FlatData";
    public static string? NameSpace2LookFor;
    public static readonly List<TypeDefinition> MemoryPackEnumsToAdd = [];
    public static bool SuppressWarnings;

    private static readonly Dictionary<string, string> TypeMap = new()
    {
        ["System.String"] = "string",
        ["System.Int16"] = "short",
        ["System.UInt16"] = "ushort",
        ["System.Int32"] = "int",
        ["System.UInt32"] = "uint",
        ["System.Int64"] = "long",
        ["System.UInt64"] = "ulong",
        ["System.Boolean"] = "bool",
        ["System.Single"] = "float",
        ["System.Double"] = "double",
        ["System.SByte"] = "sbyte",
        ["System.Byte"] = "byte",
        ["System.Decimal"] = "decimal"
    };

    public static void Execute(string dummyDll, string outputFile, string nameSpace,
        string? namespaceToLookFor, bool verbose, bool suppressWarnings)
    {
        if (verbose) Log.EnableDebugLogging();

        SuppressWarnings = suppressWarnings;

        _dummyAssemblyDir = dummyDll;
        _outputFileName = outputFile;
        _customNameSpace = nameSpace;
        NameSpace2LookFor = namespaceToLookFor;

        if (!Directory.Exists(_dummyAssemblyDir))
        {
            Log.Global.LogDummyDirNotFound(_dummyAssemblyDir);
            Log.Error("Please provide a valid path using -dummydll or -d.");
            Log.Shutdown();
            Environment.Exit(1);
        }

        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(_dummyAssemblyDir);
        var readerParameters = new ReaderParameters
        {
            AssemblyResolver = resolver
        };
        Log.Info("Reading game assemblies...");

        var blueArchiveDllPath = Path.Combine(_dummyAssemblyDir, "BlueArchive.dll");
        if (!File.Exists(blueArchiveDllPath))
        {
            Log.Global.LogFileNotFound("BlueArchive.dll", _dummyAssemblyDir);
            Log.Shutdown();
            Environment.Exit(1);
        }

        var asm = AssemblyDefinition.ReadAssembly(blueArchiveDllPath, readerParameters);

        Log.Info("Getting a list of MemoryPackable types...");

        var typeDefs = TypeHelper.GetAllMemoryPackableTypes(asm.MainModule);

        MemoryPackSchema schema = new();
        var processedTypes = new HashSet<string>();
        var discoveredTypes = new HashSet<string>();

        foreach (var typeDef in typeDefs)
        {
            discoveredTypes.Add(typeDef.FullName);
        }

        var done = 0;

        while (discoveredTypes.Count > 0)
        {
            var typeToProcess = discoveredTypes.First();
            discoveredTypes.Remove(typeToProcess);

            if (processedTypes.Contains(typeToProcess))
                continue;

            processedTypes.Add(typeToProcess);

            var typeDef = asm.MainModule.GetTypes().FirstOrDefault(t => t.FullName == typeToProcess);
            if (typeDef == null)
                continue;

            Log.Global.LogProgress(done + 1, typeDefs.Count);
            var memoryPackClass = MemberParser.TypeToMemoryPackClass(typeDef, discoveredTypes);
            schema.Classes.Add(memoryPackClass);
            done += 1;
        }

        Log.Info("Adding enums...");
        foreach (var fEnum in MemoryPackEnumsToAdd.Select(MemberParser.TypeToEnum))
            schema.Enums.Add(fEnum);

        Log.Info($"Writing C# code to {_outputFileName}...");

        WriteSchema(_outputFileName, schema);

        Log.Info("Done.");
    }

    private static void WriteSchema(string fileName, MemoryPackSchema schema)
    {
        using var buffer = Utf8String.CreateWriter(out var stringWriter);

        var namespaces = new HashSet<string>
        {
            "MemoryPack"
        };

        foreach (var cls in schema.Classes)
        {
            foreach (var member in cls.Members)
            {
                var typeStr = TypeToString(member.Type);
                if (typeStr.Contains("List<") || typeStr.Contains("Dictionary<") || typeStr.Contains("HashSet<"))
                {
                    namespaces.Add("System.Collections.Generic");
                }
            }
        }

        foreach (var ns in namespaces.OrderBy(n => n))
        {
            stringWriter.AppendFormat($"using {ns};\n");
        }
        stringWriter.AppendLine();

        if (!string.IsNullOrEmpty(_customNameSpace))
        {
            stringWriter.AppendFormat($"namespace {_customNameSpace}\n{{\n");
        }

        foreach (var memoryPackEnum in schema.Enums)
        {
            WriteEnum(ref stringWriter, memoryPackEnum);
            stringWriter.AppendLine();
        }

        foreach (var memoryPackClass in schema.Classes)
        {
            WriteClass(ref stringWriter, memoryPackClass);
            stringWriter.AppendLine();
        }

        if (!string.IsNullOrEmpty(_customNameSpace))
        {
            stringWriter.AppendLiteral("}\n");
        }

        stringWriter.Flush();

        File.WriteAllBytes(fileName, buffer.ToArray());
    }

    private static void WriteClass<TBufferWriter>(ref Utf8StringWriter<TBufferWriter> writer, MemoryPackClass memoryPackClass)
        where TBufferWriter : IBufferWriter<byte>
    {
        var indent = string.IsNullOrEmpty(_customNameSpace) ? "" : "    ";

        var attrParams = new List<string>();
        
        if (!string.IsNullOrEmpty(memoryPackClass.GenerateType) && 
            memoryPackClass.GenerateType != "Object" && 
            memoryPackClass.GenerateType != "0")
        {
            attrParams.Add($"GenerateType.{memoryPackClass.GenerateType}");
        }
        
        if (!string.IsNullOrEmpty(memoryPackClass.SerializeLayout) && 
            memoryPackClass.SerializeLayout != "Sequential" &&
            memoryPackClass.SerializeLayout != "0")
        {
            attrParams.Add($"SerializeLayout.{memoryPackClass.SerializeLayout}");
        }
        
        if (attrParams.Count > 0)
        {
            writer.AppendFormat($"{indent}[MemoryPackable({string.Join(", ", attrParams)})]\n");
        }
        else
        {
            writer.AppendFormat($"{indent}[MemoryPackable]\n");
        }

        foreach (var union in memoryPackClass.Unions)
        {
            writer.AppendFormat($"{indent}[MemoryPackUnion({union.Tag}, typeof({union.TypeName}))]\n");
        }

        writer.AppendFormat($"{indent}public partial {memoryPackClass.TypeKeyword} {memoryPackClass.ClassName}\n");
        writer.AppendFormat($"{indent}{{\n");

        foreach (var member in memoryPackClass.Members)
        {
            WriteMember(ref writer, member, indent);
        }

        writer.AppendFormat($"{indent}}}\n");
    }

    private static void WriteMember<TBufferWriter>(ref Utf8StringWriter<TBufferWriter> writer, MemoryPackMember member, string indent)
        where TBufferWriter : IBufferWriter<byte>
    {
        var memberIndent = indent + "    ";

        if (member.Order.HasValue)
        {
            writer.AppendFormat($"{memberIndent}[MemoryPackOrder({member.Order.Value})]\n");
        }

        if (member.IsInclude)
        {
            writer.AppendFormat($"{memberIndent}[MemoryPackInclude]\n");
        }

        if (member.SuppressDefaultInitialization)
        {
            writer.AppendFormat($"{memberIndent}[SuppressDefaultInitialization]\n");
        }

        if (member.AllowSerialize)
        {
            writer.AppendFormat($"{memberIndent}[MemoryPackAllowSerialize]\n");
        }

        // Write custom formatters
        foreach (var formatter in member.CustomFormatters)
        {
            writer.AppendFormat($"{memberIndent}[{formatter}]\n");
        }

        var typeStr = TypeToString(member.Type);

        if (member.IsField)
        {
            writer.AppendFormat($"{memberIndent}public {typeStr} {member.Name};\n");
        }
        else
        {
            writer.AppendFormat($"{memberIndent}public {typeStr} {member.Name} {{ get; set; }}\n");
        }

        writer.AppendLine();
    }

    private static void WriteEnum<TBufferWriter>(ref Utf8StringWriter<TBufferWriter> writer, MemoryPackEnum memoryPackEnum)
        where TBufferWriter : IBufferWriter<byte>
    {
        var indent = string.IsNullOrEmpty(_customNameSpace) ? "" : "    ";

        var enumTypeName = SystemToStringType(memoryPackEnum.Type);
        writer.AppendFormat($"{indent}public enum {memoryPackEnum.EnumName} : {enumTypeName}\n");
        writer.AppendFormat($"{indent}{{\n");

        for (var i = 0; i < memoryPackEnum.Fields.Count; i++)
        {
            var field = memoryPackEnum.Fields[i];
            var isLast = i == memoryPackEnum.Fields.Count - 1;
            writer.AppendFormat($"{indent}    {field.Name} = {field.Value}{(isLast ? "" : ",")}\n");
        }

        writer.AppendFormat($"{indent}}}\n");
    }

    private static string TypeToString(TypeReference typeRef)
    {
        if (typeRef is GenericInstanceType genericInstance)
        {
            var baseType = genericInstance.ElementType.Name;
            
            if (baseType.Contains('`'))
            {
                baseType = baseType[..baseType.IndexOf('`')];
            }

            var genericArgs = string.Join(", ", genericInstance.GenericArguments.Select(TypeToString));
            return $"{baseType}<{genericArgs}>";
        }

        if (typeRef.IsArray)
        {
            var arrayType = typeRef as ArrayType;
            return TypeToString(arrayType!.ElementType) + "[]";
        }

        var typeDef = typeRef.Resolve();
        if (typeDef != null)
        {
            return SystemToStringType(typeDef);
        }

        return typeRef.Name;
    }

    private static string SystemToStringType(TypeDefinition typeDef)
    {
        var fullName = typeDef.FullName;
        if (TypeMap.TryGetValue(fullName, out var type))
            return type;

        var name = typeDef.Name;
        if (name.StartsWith("System."))
            Log.Global.LogUnknownSystemType(name);

        return name;
    }
}
