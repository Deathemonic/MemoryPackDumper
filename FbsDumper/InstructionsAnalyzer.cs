using System.Text.RegularExpressions;
using Iced.Intel;

namespace FbsDumper;

public enum Architecture
{
    Arm64,
    X86
}

internal abstract class InstructionsAnalyzer
{
    public static IInstructionAnalyzer GetAnalyzer(Architecture architecture)
    {
        return architecture switch
        {
            Architecture.Arm64 => new ArmInstructionAnalyzer(),
            Architecture.X86 => new X86InstructionAnalyzer(),
            _ => throw new ArgumentException($"Unsupported architecture: {architecture}")
        };
    }

    public class CallInfo
    {
        public ulong Address;
        public Dictionary<string, string> Args = [];
        public string? EdxValue;
        public string? Target;
    }
}

internal interface IInstructionAnalyzer
{
    List<InstructionsAnalyzer.CallInfo> AnalyzeCalls(List<InstructionWithAddress> instructions);
}

internal class ArmInstructionAnalyzer : IInstructionAnalyzer
{
    public List<InstructionsAnalyzer.CallInfo> AnalyzeCalls(List<InstructionWithAddress> instructions)
    {
        var result = new List<InstructionsAnalyzer.CallInfo>();
        var regState = new Dictionary<string, string>();

        foreach (var instr in instructions)
        {
            if (instr.Instruction == null) continue;

            var mnemonic = instr.Mnemonic;
            var operands = ParseArmOperands(instr.Operand);

            ProcessArmInstruction(mnemonic, operands, instr, regState, result);
        }

        return result;
    }

    private static string[] ParseArmOperands(string? operandString)
    {
        return string.IsNullOrEmpty(operandString)
            ? []
            : operandString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static void ProcessArmInstruction(string mnemonic, string[] operands, InstructionWithAddress instr,
        Dictionary<string, string> regState, List<InstructionsAnalyzer.CallInfo> result)
    {
        switch (mnemonic)
        {
            case "mov":
            case "movz":
                ProcessArmMoveInstruction(operands, regState);
                break;
            case "movk":
            case "movn":
                ProcessArmMoveVariantInstruction(operands, mnemonic, regState);
                break;
            case "bl":
            case "b":
                ProcessArmBranchInstruction(instr, regState, result);
                break;
            default:
                ProcessArmOtherInstruction(mnemonic);
                break;
        }
    }

    private static void ProcessArmMoveInstruction(string[] operands, Dictionary<string, string> regState)
    {
        if (operands.Length == 2)
            regState[operands[0]] = operands[1];
    }

    private static void ProcessArmMoveVariantInstruction(string[] operands, string mnemonic,
        Dictionary<string, string> regState)
    {
        if (operands.Length >= 1)
            regState[operands[0]] = $"<{mnemonic}>";
    }

    private static void ProcessArmBranchInstruction(InstructionWithAddress instr, Dictionary<string, string> regState,
        List<InstructionsAnalyzer.CallInfo> result)
    {
        var targetAddress = CalculateArmTarget(instr);

        var call = new InstructionsAnalyzer.CallInfo
        {
            Address = instr.Address,
            Target = targetAddress.HasValue ? $"0x{targetAddress.Value:X}" : "<unknown>",
            Args = []
        };

        PopulateArmCallArguments(call, regState);
        result.Add(call);
    }

    private static void PopulateArmCallArguments(InstructionsAnalyzer.CallInfo call,
        Dictionary<string, string> regState)
    {
        for (var i = 0; i <= 7; i++)
        {
            var xReg = $"x{i}";
            var wReg = $"w{i}";

            if (regState.TryGetValue(xReg, out var value))
                call.Args[xReg] = value;
            else if (regState.TryGetValue(wReg, out var value2))
                call.Args[wReg] = value2;
        }
    }

    private static void ProcessArmOtherInstruction(string mnemonic)
    {
        if (mnemonic == "cbz" || mnemonic == "cmp" || mnemonic.StartsWith('b'))
        {
            // Not Implemented
        }
    }

    private static ulong? CalculateArmTarget(InstructionWithAddress instr)
    {
        var operandString = instr.Operand;
        if (string.IsNullOrEmpty(operandString)) return null;
        var ops = operandString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (ops.Length <= 0 || !ops[0].StartsWith('#')) return null;
        var offsetStr = ops[0][1..];
        if (long.TryParse(offsetStr, out var offset)) return instr.Address + (ulong)offset;

        return null;
    }
}

internal partial class X86InstructionAnalyzer : IInstructionAnalyzer
{
    public List<InstructionsAnalyzer.CallInfo> AnalyzeCalls(List<InstructionWithAddress>? instructions)
    {
        var result = new List<InstructionsAnalyzer.CallInfo>();
        if (instructions == null || instructions.Count == 0) return result;

        var totalStackAllocation = AnalyzePrologue(instructions);
        var regState = new Dictionary<Register, ValueSource>();
        var stackState = new Dictionary<ulong, ValueSource>();
        ulong tick = 0;

        InitializeParameterRegisters(regState, tick);

        foreach (var x86Instr in instructions.Select(instr => instr.X86Instruction))
        {
            tick++;
            ProcessInstruction(x86Instr, regState, stackState, totalStackAllocation, tick, result);
        }

        return result;
    }

    private static void InitializeParameterRegisters(Dictionary<Register, ValueSource> regState, ulong tick)
    {
        regState[Register.RCX] = new ValueSource("param1", tick);
        regState[Register.RDX] = new ValueSource("param2", tick);
        regState[Register.R8] = new ValueSource("param3", tick);
        regState[Register.R9] = new ValueSource("param4", tick);
    }

    private static void ProcessInstruction(Instruction x86Instr, Dictionary<Register, ValueSource> regState,
        Dictionary<ulong, ValueSource> stackState, ulong totalStackAllocation, ulong tick,
        List<InstructionsAnalyzer.CallInfo> result)
    {
        switch (x86Instr.Mnemonic)
        {
            case Mnemonic.Mov:
            case Mnemonic.Lea:
                ProcessMovInstruction(x86Instr, regState, stackState, totalStackAllocation, tick);
                break;
            case Mnemonic.Xor when IsRegisterXorWithItself(x86Instr):
                regState[GetCanonicalRegister(x86Instr.Op0Register)] = new ValueSource("immediate:0", tick);
                break;
            case Mnemonic.Call:
                ProcessCallInstruction(x86Instr, regState, result);
                break;
        }
    }

    private static bool IsRegisterXorWithItself(Instruction x86Instr)
    {
        return x86Instr.Op0Kind == OpKind.Register && x86Instr.Op0Register == x86Instr.Op1Register;
    }

    private static void ProcessMovInstruction(Instruction x86Instr, Dictionary<Register, ValueSource> regState,
        Dictionary<ulong, ValueSource> stackState, ulong totalStackAllocation, ulong tick)
    {
        var source = GetSourceValue(x86Instr, regState, stackState, totalStackAllocation, tick);
        if (source == null) return;

        AssignToDestination(x86Instr, regState, stackState, source, tick);
    }

    private static ValueSource? GetSourceValue(Instruction x86Instr, Dictionary<Register, ValueSource> regState,
        Dictionary<ulong, ValueSource> stackState, ulong totalStackAllocation, ulong tick)
    {
        return x86Instr.Op1Kind switch
        {
            OpKind.Register => GetRegisterValue(x86Instr.Op1Register, regState),
            OpKind.Memory when x86Instr.MemoryBase == Register.RSP => GetStackValue(x86Instr.MemoryDisplacement64,
                stackState, totalStackAllocation, tick),
            OpKind.Immediate32 or OpKind.Immediate64 => new ValueSource($"immediate:0x{x86Instr.Immediate32:X}", tick),
            _ => null
        };
    }

    private static ValueSource? GetRegisterValue(Register register, Dictionary<Register, ValueSource> regState)
    {
        regState.TryGetValue(GetCanonicalRegister(register), out var source);
        return source;
    }

    private static ValueSource? GetStackValue(ulong offset, Dictionary<ulong, ValueSource> stackState,
        ulong totalStackAllocation, ulong tick)
    {
        if (stackState.TryGetValue(offset, out var knownSource))
            return knownSource;

        const ulong callerSideOffset = 0x28;
        var paramBaseOffset = totalStackAllocation + callerSideOffset;

        if (offset < paramBaseOffset) return null;

        var paramNum = (offset - paramBaseOffset) / 8 - 4;
        return new ValueSource($"param{paramNum}", tick);
    }

    private static void AssignToDestination(Instruction x86Instr, Dictionary<Register, ValueSource> regState,
        Dictionary<ulong, ValueSource> stackState, ValueSource source, ulong tick)
    {
        switch (x86Instr.Op0Kind)
        {
            case OpKind.Register:
                regState[GetCanonicalRegister(x86Instr.Op0Register)] = new ValueSource(source.Id, tick);
                break;
            case OpKind.Memory when x86Instr.MemoryBase == Register.RSP:
                stackState[x86Instr.MemoryDisplacement64] = new ValueSource(source.Id, tick);
                break;
        }
    }

    private static void ProcessCallInstruction(Instruction x86Instr, Dictionary<Register, ValueSource> regState,
        List<InstructionsAnalyzer.CallInfo> result)
    {
        var call = new InstructionsAnalyzer.CallInfo
        {
            Address = x86Instr.IP,
            Target = x86Instr.NearBranchTarget != 0 ? $"0x{x86Instr.NearBranchTarget:X}" : "<dynamic>"
        };

        ProcessR8Register(regState);
        ProcessEdxRegister(regState, call);

        result.Add(call);
    }

    private static void ProcessR8Register(Dictionary<Register, ValueSource> regState)
    {
        if (!regState.TryGetValue(Register.R8, out var r8Source)) return;

        var match = MyRegex().Match(r8Source.Id);
        if (match.Success) _ = int.Parse(match.Groups[1].Value);
    }

    private static void ProcessEdxRegister(Dictionary<Register, ValueSource> regState,
        InstructionsAnalyzer.CallInfo call)
    {
        if (regState.TryGetValue(Register.RDX, out var edxSource))
            call.EdxValue = edxSource.Id.Replace("immediate:", "");
    }

    private static Register GetCanonicalRegister(Register reg)
    {
        return reg.GetFullRegister().GetFullRegister();
    }

    private static ulong AnalyzePrologue(List<InstructionWithAddress> instructions)
    {
        ulong allocation = 0;

        foreach (var x86Instr in instructions.TakeWhile(_ => true)
                     .Select(instr => instr.X86Instruction))
            switch (x86Instr.Mnemonic)
            {
                case Mnemonic.Push:
                    allocation += 8;
                    break;
                case Mnemonic.Sub when x86Instr.Op0Register == Register.RSP &&
                                       (x86Instr.Op1Kind == OpKind.Immediate32 ||
                                        x86Instr.Op1Kind == OpKind.Immediate64):
                    allocation += x86Instr.Immediate64;
                    break;
                case Mnemonic.Mov when x86Instr.Op0Kind == OpKind.Register &&
                                       x86Instr.Op1Kind == OpKind.Register:
                    break;
                default:
                    return allocation;
            }

        return allocation;
    }

    [GeneratedRegex(@"param(\d+)")]
    private static partial Regex MyRegex();

    private class ValueSource(string id, ulong tick)
    {
        public string Id { get; } = id;
        public ulong Tick { get; } = tick;

        public override string ToString()
        {
            return Id;
        }
    }
}