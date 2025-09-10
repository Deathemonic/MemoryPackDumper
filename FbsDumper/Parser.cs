using System.Text;
using System.Text.RegularExpressions;
using Mono.Cecil;

namespace FbsDumper;

public static partial class Parser
{
    private static string _dummyAssemblyDir = "DummyDll";
    public static string GameAssemblyPath = "libil2cpp.so";
    private static string _outputFileName = "BlueArchive.fbs";
    private static string? _customNameSpace = "FlatData";
    private static bool _forceSnakeCase;
    public static string? NameSpace2LookFor;
    private static readonly string FlatBaseType = "FlatBuffers.IFlatbufferObject";
    public static FlatBufferBuilder FlatBufferBuilder = null!;
    public static readonly List<TypeDefinition> FlatEnumsToAdd = [];

    public static void Execute(string dummyDll, string gameAssembly, string outputFile, string nameSpace,
        bool forceSnakeCase, string? namespaceToLookFor)
    {
        _dummyAssemblyDir = dummyDll;
        GameAssemblyPath = gameAssembly;
        _outputFileName = outputFile;
        _customNameSpace = nameSpace;
        NameSpace2LookFor = namespaceToLookFor;
        _forceSnakeCase = forceSnakeCase;

        if (!Directory.Exists(_dummyAssemblyDir))
        {
            Console.WriteLine($"[ERR] Dummy assembly directory '{_dummyAssemblyDir}' not found.");
            Console.WriteLine("Please provide a valid path using -dummydll or -d.");
            Environment.Exit(1);
        }

        if (!File.Exists(GameAssemblyPath))
        {
            Console.WriteLine($"[ERR] libil2cpp.so path '{GameAssemblyPath}' not found.");
            Console.WriteLine("Please provide a valid path using -gameassembly or -a.");
            Environment.Exit(1);
        }

        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(_dummyAssemblyDir);
        var readerParameters = new ReaderParameters
        {
            AssemblyResolver = resolver
        };
        Console.WriteLine("Reading game assemblies...");

        var blueArchiveDllPath = Path.Combine(_dummyAssemblyDir, "BlueArchive.dll");
        if (!File.Exists(blueArchiveDllPath))
        {
            Console.WriteLine($"[ERR] BlueArchive.dll not found in '{_dummyAssemblyDir}'.");
            Environment.Exit(1);
        }

        var asm = AssemblyDefinition.ReadAssembly(blueArchiveDllPath, readerParameters);

        var flatBuffersDllPath = Path.Combine(_dummyAssemblyDir, "FlatBuffers.dll");
        if (!File.Exists(flatBuffersDllPath))
        {
            Console.WriteLine($"[ERR] FlatBuffers.dll not found in '{_dummyAssemblyDir}'.");
            Environment.Exit(1);
        }

        var asmFbs = AssemblyDefinition.ReadAssembly(flatBuffersDllPath, readerParameters);

        FlatBufferBuilder = new FlatBufferBuilder(asmFbs.MainModule);
        var typeHelper = new TypeHelper();
        Console.WriteLine("Getting a list of types...");
        var typeDefs = TypeHelper.GetAllFlatBufferTypes(asm.MainModule, FlatBaseType);
        var schema = new FlatSchema();
        var done = 0;
        foreach (var typeDef in typeDefs)
        {
            Console.Write($"Disassembling types ({done + 1}/{typeDefs.Count})... \r");
            var table = typeHelper.Type2Table(typeDef);
            if (table == null)
            {
                Console.WriteLine($"[ERR] Error dumping table for {typeDef.FullName}");
                continue;
            }

            schema.FlatTables.Add(table);
            done += 1;
        }

        Console.WriteLine("Adding enums...");
        foreach (var fEnum in FlatEnumsToAdd.Select(TypeHelper.Type2Enum)) schema.FlatEnums.Add(fEnum);

        Console.WriteLine($"Writing schema to {_outputFileName}...");
        File.WriteAllText(_outputFileName, SchemaToString(schema));
        Console.WriteLine("Done.");
    }

    private static string SchemaToString(FlatSchema schema)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrEmpty(_customNameSpace)) sb.AppendLine($"namespace {_customNameSpace};\n");

        foreach (var flatEnum in schema.FlatEnums) sb.AppendLine(TableEnumToString(flatEnum));

        foreach (var table in schema.FlatTables) sb.AppendLine(TableToString(table));

        return sb.ToString();
    }

    private static string TableToString(FlatTable table)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"table {table.TableName} {{");

        if (table.NoCreate)
            sb.AppendLine("\t// No Create method");

        foreach (var field in table.Fields) sb.AppendLine(TableFieldToString(field));

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string TableEnumToString(FlatEnum fEnum)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"enum {fEnum.EnumName} : {SystemToStringType(fEnum.Type)} {{");

        for (var i = 0; i < fEnum.Fields.Count; i++)
        {
            var field = fEnum.Fields[i];
            sb.AppendLine(TableEnumFieldToString(field, i == fEnum.Fields.Count - 1));
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string TableEnumFieldToString(FlatEnumField field, bool isLast = false)
    {
        return $"\t{field.Name} = {field.Value}{(isLast ? "" : ",")}";
    }

    private static string TableFieldToString(FlatField field)
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.Append($"\t{(_forceSnakeCase ? CamelToSnake(field.Name) : field.Name)}: ");

        var fieldType = SystemToStringType(field.Type);

        fieldType = field.IsArray ? $"[{fieldType}]" : fieldType;

        stringBuilder.Append($"{fieldType}; // index 0x{field.Offset:X}");

        return stringBuilder.ToString();
    }

    private static string CamelToSnake(string camelStr)
    {
        var isAllUppercase = camelStr.All(char.IsUpper);
        if (string.IsNullOrEmpty(camelStr) || isAllUppercase)
            return camelStr;
        return MyRegex().Replace(camelStr, "$1_").ToLower();
    }

    private static string SystemToStringType(TypeDefinition field)
    {
        var fieldType = field.Name;

        switch (field.FullName)
        {
            case "System.String":
                fieldType = "string";
                break;
            case "System.Int16":
                fieldType = "short";
                break;
            case "System.UInt16":
                fieldType = "ushort";
                break;
            case "System.Int32":
                fieldType = "int";
                break;
            case "System.UInt32":
                fieldType = "uint";
                break;
            case "System.Int64":
                fieldType = "long";
                break;
            case "System.UInt64":
                fieldType = "ulong";
                break;
            case "System.Boolean":
                fieldType = "bool";
                break;
            case "System.Single":
                fieldType = "float";
                break;
            case "System.SByte":
                fieldType = "int8";
                break;
            case "System.Byte":
                fieldType = "uint8";
                break;
            default:
                if (fieldType.StartsWith("System.")) Console.WriteLine($"[WARN] unknown system type {fieldType}");
                break;
        }

        return fieldType;
    }

    [GeneratedRegex(@"(([a-z])(?=[A-Z][a-zA-Z])|([A-Z])(?=[A-Z][a-z]))")]
    private static partial Regex MyRegex();
}

public class FlatBufferBuilder
{
    public readonly long EndObject;
    public readonly Dictionary<long, MethodDefinition> Methods;
    public readonly long StartObject;

    public FlatBufferBuilder(ModuleDefinition flatBuffersDllModule)
    {
        Methods = new Dictionary<long, MethodDefinition>();
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