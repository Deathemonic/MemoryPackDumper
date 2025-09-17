using System.Text;
using System.Text.RegularExpressions;
using FbsDumper.Assembly;
using FbsDumper.Helpers;
using FbsDumper.Instructions;
using Mono.Cecil;

namespace FbsDumper.CLI;

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
    public static bool SuppressWarnings;

    public static void Execute(string dummyDll, string gameAssembly, string outputFile, string nameSpace,
        bool forceSnakeCase, string? namespaceToLookFor, bool verbose, bool suppressWarnings)
    {
        if (verbose) Log.EnableDebugLogging();

        SuppressWarnings = suppressWarnings;

        _dummyAssemblyDir = dummyDll;
        GameAssemblyPath = gameAssembly;
        _outputFileName = outputFile;
        _customNameSpace = nameSpace;
        NameSpace2LookFor = namespaceToLookFor;
        _forceSnakeCase = forceSnakeCase;

        if (!Directory.Exists(_dummyAssemblyDir))
        {
            Log.Global.LogDummyDirNotFound(_dummyAssemblyDir);
            Log.Error("Please provide a valid path using -dummydll or -d.");
            Log.Shutdown();
            Environment.Exit(1);
        }

        if (!File.Exists(GameAssemblyPath))
        {
            Log.Global.LogGameAssemblyNotFound(GameAssemblyPath);
            Log.Error("Please provide a valid path using -gameassembly or -a.");
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

        var flatBuffersDllPath = Path.Combine(_dummyAssemblyDir, "FlatBuffers.dll");
        if (!File.Exists(flatBuffersDllPath))
        {
            Log.Global.LogFileNotFound("FlatBuffers.dll", _dummyAssemblyDir);
            Log.Shutdown();
            Environment.Exit(1);
        }

        var asmFbs = AssemblyDefinition.ReadAssembly(flatBuffersDllPath, readerParameters);

        FlatBufferBuilder = new FlatBufferBuilder(asmFbs.MainModule);

        var architecture = TypeHelper.DetectArchitecture(GameAssemblyPath);
        var typeParser = TypeHelper.GetTypeParser(architecture);

        Log.Info($"Detected architecture: {architecture}");
        Log.Info("Getting a list of types...");

        var typeDefs = TypeHelper.GetAllFlatBufferTypes(asm.MainModule, FlatBaseType);
        var schema = new FlatSchema();
        var done = 0;
        foreach (var typeDef in typeDefs)
        {
            Log.Global.LogProgress(done + 1, typeDefs.Count);
            var table = TypeHelper.TypeToTable(typeParser, typeDef);

            schema.FlatTables.Add(table);
            done += 1;
        }

        Log.Info("Adding enums...");
        foreach (var fEnum in FlatEnumsToAdd.Select(TypeHelper.TypeToEnum)) schema.FlatEnums.Add(fEnum);

        Log.Info($"Writing schema to {_outputFileName}...");
        File.WriteAllText(_outputFileName, SchemaToString(schema));
        Log.Info("Done.");
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
                if (fieldType.StartsWith("System.")) Log.Global.LogUnknownSystemType(fieldType);
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