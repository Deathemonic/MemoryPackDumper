using Mono.Cecil;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using System;

namespace FbsDumper;

public class MainApp
{
    private static string DummyAssemblyDir = "DummyDll";
	public static string LibIl2CppPath = "libil2cpp.so"; // change it to the actual path
	private static string OutputFileName = "BlueArchive.fbs";
    private static string? CustomNameSpace = "FlatData"; // can also be String.Empty, "", or null to not specify namespace
    public static bool ForceSnakeCase = false;
	public static string? NameSpace2LookFor = null; // can also be MX.Data.Excel or FlatData to specify different namespaces
	private static readonly string FlatBaseType = "FlatBuffers.IFlatbufferObject";
    public static FlatBufferBuilder flatBufferBuilder;
    public static List<TypeDefinition> flatEnumsToAdd = new List<TypeDefinition>(); // for GetAllFlatBufferTypes -> getting enums part

    public static void Main(string[] args)
    {
        ParseArguments(args);

        if (!Directory.Exists(DummyAssemblyDir))
        {
            Console.WriteLine($"[ERR] Dummy assembly directory '{DummyAssemblyDir}' not found.");
            Console.WriteLine("Please provide a valid path using --dummy-dir or -d.");
            Environment.Exit(1);
        }
        if (!File.Exists(LibIl2CppPath))
        {
            Console.WriteLine($"[ERR] libil2cpp.so path '{LibIl2CppPath}' not found.");
            Console.WriteLine("Please provide a valid path using --libil2cpp-path or -l.");
            Environment.Exit(1);
        }

        DefaultAssemblyResolver resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(DummyAssemblyDir);
        ReaderParameters readerParameters = new ReaderParameters();
        readerParameters.AssemblyResolver = resolver;
        Console.WriteLine("Reading game assemblies...");
        
        string blueArchiveDllPath = Path.Combine(DummyAssemblyDir, "BlueArchive.dll");
        if (!File.Exists(blueArchiveDllPath))
        {
            Console.WriteLine($"[ERR] BlueArchive.dll not found in '{DummyAssemblyDir}'.");
            Environment.Exit(1);
        }
        AssemblyDefinition asm = AssemblyDefinition.ReadAssembly(blueArchiveDllPath, readerParameters);

        string flatBuffersDllPath = Path.Combine(DummyAssemblyDir, "FlatBuffers.dll");
        if (!File.Exists(flatBuffersDllPath))
        {
            Console.WriteLine($"[ERR] FlatBuffers.dll not found in '{DummyAssemblyDir}'.");
            Environment.Exit(1);
        }
        AssemblyDefinition asmFBS = AssemblyDefinition.ReadAssembly(flatBuffersDllPath, readerParameters);
        
        flatBufferBuilder = new FlatBufferBuilder(asmFBS.MainModule);
        TypeHelper typeHelper = new TypeHelper();
        Console.WriteLine("Getting a list of types...");
        List<TypeDefinition> typeDefs = typeHelper.GetAllFlatBufferTypes(asm.MainModule, FlatBaseType);
        FlatSchema schema = new FlatSchema();
        int done = 0;
        foreach (TypeDefinition typeDef in typeDefs)
        {
            Console.Write($"Disassembling types ({done + 1}/{typeDefs.Count})... \r");
            FlatTable? table = typeHelper.Type2Table(typeDef);
            if (table == null)
            {
                Console.WriteLine($"[ERR] Error dumping table for {typeDef.FullName}");
                continue;
            }
            schema.flatTables.Add(table);
            done += 1;
        }
        Console.WriteLine($"Adding enums...");
        foreach (TypeDefinition typeDef in flatEnumsToAdd)
        {
            FlatEnum? fEnum = TypeHelper.Type2Enum(typeDef);
            if (fEnum == null)
            {
                Console.WriteLine($"[ERR] Error dumping enum for {typeDef.FullName}");
                continue;
            }
            schema.flatEnums.Add(fEnum);
        }
        Console.WriteLine($"Writing schema to {OutputFileName}...");
        File.WriteAllText(OutputFileName, SchemaToString(schema));
        Console.WriteLine($"Done.");
    }

    private static void ParseArguments(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLower())
            {
                case "--dummy-dir":
                case "-d":
                    if (i + 1 < args.Length)
                    {
                        DummyAssemblyDir = args[++i];
                    }
                    else
                    {
                        Console.WriteLine("[ERR] --dummy-dir requires a path.");
                        Environment.Exit(1);
                    }
                    break;
                case "--libil2cpp-path":
                case "-l":
                    if (i + 1 < args.Length)
                    {
                        LibIl2CppPath = args[++i];
                    }
                    else
                    {
                        Console.WriteLine("[ERR] --libil2cpp-path requires a path.");
                        Environment.Exit(1);
                    }
                    break;
                case "--output-file":
                case "-o":
                    if (i + 1 < args.Length)
                    {
                        OutputFileName = args[++i];
                    }
                    else
                    {
                        Console.WriteLine("[ERR] --output-file requires a file name.");
                        Environment.Exit(1);
                    }
                    break;
                case "--namespace":
                case "-n":
                    if (i + 1 < args.Length)
                    {
                        CustomNameSpace = args[++i];
                    }
                    else
                    {
                        Console.WriteLine("[ERR] --namespace requires a namespace string.");
                        Environment.Exit(1);
                    }
                    break;
                case "--force-snake-case":
                case "-s":
                    ForceSnakeCase = true;
                    break;
                case "--namespace-to-look-for":
                case "-nl":
                    if (i + 1 < args.Length)
                    {
                        NameSpace2LookFor = args[++i];
                    }
                    else
                    {
                        Console.WriteLine("[ERR] --namespace-to-look-for requires a namespace string.");
                        Environment.Exit(1);
                    }
                    break;
                case "--help":
                case "-h":
                    PrintHelp();
                    Environment.Exit(0);
                    break;
                default:
                    Console.WriteLine($"[WARN] Unknown argument: {args[i]}. Use --help for usage information.");
                    break;
            }
        }
    }


    private static void PrintHelp()
    {
        Console.WriteLine("Usage: FbsDumper [options]");
        Console.WriteLine("\nOptions:");
        Console.WriteLine("  -d, --dummy-dir <PATH>          (Mandatory) Path to the directory containing dummy assemblies (e.g., DummyDll).");
        Console.WriteLine("  -l, --libil2cpp-path <PATH>     (Mandatory) Path to the libil2cpp.so file.");
        Console.WriteLine("  -o, --output-file <NAME>        (Optional) Name of the output FlatBuffer schema file (default: BlueArchive.fbs).");
        Console.WriteLine("  -n, --namespace <NAMESPACE>     (Optional) Custom namespace for the FlatBuffer schema (default: FlatData).");
        Console.WriteLine("  -s, --force-snake-case          (Optional) Convert field names to snake_case (default: false).");
        Console.WriteLine("  -nl, --namespace-to-look-for <NAMESPACE> (Optional) Specify a namespace to filter types (e.g., MX.Data.Excel or FlatData).");
        Console.WriteLine("  -h, --help                      Show this help message and exit.");
    }

    private static string SchemaToString(FlatSchema schema)
    {
        StringBuilder sb = new StringBuilder();

        if (!string.IsNullOrEmpty(CustomNameSpace))
        {
            sb.AppendLine($"namespace {CustomNameSpace};\n");
        }

        foreach (FlatEnum flatEnum in schema.flatEnums)
        {
            sb.AppendLine(TableEnumToString(flatEnum));
        }

        foreach (FlatTable table in schema.flatTables)
        {
            sb.AppendLine(TableToString(table));
        }

        return sb.ToString();
    }

    private static string TableToString(FlatTable table)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"table {table.tableName} {{");

        if (table.noCreate)
			sb.AppendLine("\t// No Create method");

		foreach (FlatField field in table.fields)
        {
            sb.AppendLine(TableFieldToString(field));
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string TableEnumToString(FlatEnum fEnum)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"enum {fEnum.enumName} : {SystemToStringType(fEnum.type)} {{");

        for (int i = 0; i < fEnum.fields.Count; i++)
        {
            FlatEnumField field = fEnum.fields[i];
            sb.AppendLine(TableEnumFieldToString(field, i == fEnum.fields.Count-1));
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string TableEnumFieldToString(FlatEnumField field, bool isLast = false)
    {
        return $"\t{field.name} = {field.value}{(isLast ? "" : ",")}";
    }

    private static string TableFieldToString(FlatField field)
    {
        StringBuilder stringBuilder = new StringBuilder();
        stringBuilder.Append($"\t{(ForceSnakeCase ? CamelToSnake(field.name) : field.name)}: ");

        string fieldType = SystemToStringType(field.type);

        fieldType = field.isArray ? $"[{fieldType}]" : fieldType;

        stringBuilder.Append($"{fieldType}; // index 0x{field.offset:X}");

        return stringBuilder.ToString();
    }

    static string CamelToSnake(string camelStr)
    {
        bool isAllUppercase = camelStr.All(char.IsUpper); // Beebyte
        if (string.IsNullOrEmpty(camelStr) || isAllUppercase)
            return camelStr;
        return Regex.Replace(camelStr, @"(([a-z])(?=[A-Z][a-zA-Z])|([A-Z])(?=[A-Z][a-z]))", "$1_").ToLower();
    }

    public static string SystemToStringType(TypeDefinition field)
    {
        string fieldType = field.Name;

        switch (field.FullName)
        {
            // all system types to flatbuffer format

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
                if (fieldType.StartsWith("System."))
                {
                    Console.WriteLine($"[WARN] unknown system type {fieldType}");
                }
                break;
        }

        return fieldType;
    }
}

public class FlatBufferBuilder
{
    public long StartObject;
    public long EndObject;
    public Dictionary<long, MethodDefinition> methods;

    public FlatBufferBuilder(ModuleDefinition flatBuffersDllModule)
    {
        methods = new Dictionary<long, MethodDefinition>();
		TypeDefinition FlatBufferBuilderType = flatBuffersDllModule.GetType("FlatBuffers.FlatBufferBuilder");
        foreach (MethodDefinition method in FlatBufferBuilderType.Methods)
        {
            long rva = InstructionsParser.GetMethodRVA(method);
            {
                switch (method.Name)
                {
                    case "StartObject":
                        StartObject = rva;
                        break;
                    case "EndObject":
                        EndObject = rva;
                        break;
                    default:
                        // do nothing
                        break;
				}
            }
            methods.Add(rva, method);
        }
	}
}