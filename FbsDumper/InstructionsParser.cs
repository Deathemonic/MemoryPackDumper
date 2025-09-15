using AsmArm64;
using Mono.Cecil;

namespace FbsDumper;

internal class InstructionsParser(string gameAssemblyPath)
{
    private readonly byte[] _fileBytes = File.ReadAllBytes(gameAssemblyPath);

    public List<InstructionWithAddress> GetInstructions(MethodDefinition targetMethod, bool debug = false)
    {
        var rva = GetMethodRva(targetMethod);
        if (rva != 0) return GetInstructions(rva, debug);

        Log.Warning($"Invalid RVA or offset for method: {targetMethod.FullName}");

        return [];
    }

    private List<InstructionWithAddress> GetInstructions(long rva, bool debug = false)
    {
        var instructions = new List<InstructionWithAddress>();

        const int instrSize = 4;
        var currentOffset = rva;

        while (currentOffset + instrSize <= _fileBytes.Length)
        {
            var instrBytes = new byte[instrSize];
            Array.Copy(_fileBytes, currentOffset, instrBytes, 0, instrSize);

            var instrValue = BitConverter.ToUInt32(instrBytes, 0);

            try
            {
                var instr = Arm64Instruction.Decode(instrValue);
                var instrWithAddress = new InstructionWithAddress(instr, (ulong)currentOffset);
                instructions.Add(instrWithAddress);

                if (debug)
                {
                    var operandString = GetOperandString(instr);
                    Log.Global.LogInstruction((ulong)currentOffset, instr.Mnemonic.ToString().ToLower(), operandString);
                }

                currentOffset += instrSize;

                if (instr.Mnemonic.ToString().Equals("ret", StringComparison.CurrentCultureIgnoreCase))
                    break;
            }
            catch
            {
                break;
            }
        }

        return instructions;
    }

    private static string GetOperandString(Arm64Instruction instruction)
    {
        if (instruction.Operands.Count == 0)
            return string.Empty;

        var operands = instruction.Operands.Select(operand => operand.ToString()).ToList();
        return string.Join(", ", operands);
    }


    public static long GetMethodRva(MethodDefinition method)
    {
        if (!method.HasCustomAttributes)
            return 0;

        var customAttr = method.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == "AddressAttribute");
        if (customAttr is not { HasFields: true })
            return 0;

        var argRva = customAttr.Fields.First(f => f.Name == "RVA");
        var rva = Convert.ToInt64(argRva.Argument.Value.ToString()?[2..], 16);
        return rva;
    }
}

internal record InstructionWithAddress(Arm64Instruction Instruction, ulong Address)
{
    public string Mnemonic => Instruction.Mnemonic.ToString().ToLower();

    public string? Operand
    {
        get
        {
            if (Instruction.Operands.Count == 0)
                return null;

            var operands = Instruction.Operands.Select(operand => operand.ToString()).ToList();
            return string.Join(", ", operands);
        }
    }
}