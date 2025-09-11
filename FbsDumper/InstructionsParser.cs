using Gee.External.Capstone;
using Gee.External.Capstone.Arm64;
using Mono.Cecil;

namespace FbsDumper;

internal class InstructionsParser(string gameAssemblyPath)
{
    private readonly byte[] _fileBytes = File.ReadAllBytes(gameAssemblyPath);

    public List<Arm64Instruction> GetInstructions(MethodDefinition targetMethod, bool debug = false)
    {
        var rva = GetMethodRva(targetMethod);
        if (rva != 0) return GetInstructions(rva, debug);
        
        Log.Warning($"Invalid RVA or offset for method: {targetMethod.FullName}");
        
        return [];
    }

    private List<Arm64Instruction> GetInstructions(long rva, bool debug = false)
    {
        var instructions = new List<Arm64Instruction>();

        using var capstone = CapstoneDisassembler.CreateArm64Disassembler(Arm64DisassembleMode.LittleEndian);
        capstone.EnableInstructionDetails = true;
        
        const int instrSize = 4;
        var currentOffset = rva;

        while (currentOffset + instrSize <= _fileBytes.Length)
        {
            var instrBytes = new byte[instrSize];
            Array.Copy(_fileBytes, currentOffset, instrBytes, 0, instrSize);

            var decoded = capstone.Disassemble(instrBytes, currentOffset);
            if (decoded.Length == 0)
                break;

            var instr = decoded[0];
            instructions.Add(instr);

            if (debug)
            {
                Log.Global.LogInstruction((ulong)instr.Address, instr.Mnemonic, instr.Operand);
            }

            currentOffset += instrSize;

            if (instr.Mnemonic == "ret")
                break;
        }

        return instructions;
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