namespace FbsDumper;

internal abstract class InstructionsAnalyzer
{
    public static List<ArmCallInfo> AnalyzeCalls(List<InstructionWithAddress> instructions)
    {
        var result = new List<ArmCallInfo>();
        var regState = new Dictionary<string, string>();

        foreach (var instr in instructions)
        {
            var mnemonic = instr.Mnemonic;
            var operandString = instr.Operand;
            var ops = string.IsNullOrEmpty(operandString)
                ? []
                : operandString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            switch (mnemonic)
            {
                case "mov":
                case "movz":
                {
                    if (ops.Length == 2) regState[ops[0]] = ops[1];
                    break;
                }
                case "movk":
                case "movn":
                {
                    if (ops.Length >= 1)
                        regState[ops[0]] = $"<{mnemonic}>";
                    break;
                }
                case "bl":
                case "b":
                {
                    var targetAddress = CalculateTarget(instr);

                    var call = new ArmCallInfo
                    {
                        Address = instr.Address,
                        Target = targetAddress.HasValue ? $"0x{targetAddress.Value:X}" : "<unknown>",
                        Args = []
                    };

                    for (var i = 0; i <= 7; i++)
                    {
                        var xReg = $"x{i}";
                        var wReg = $"w{i}";

                        if (regState.TryGetValue(xReg, out var value))
                            call.Args[xReg] = value;
                        else if (regState.TryGetValue(wReg, out var value2))
                            call.Args[wReg] = value2;
                    }

                    result.Add(call);
                    break;
                }
                default:
                {
                    if (mnemonic == "cbz" || mnemonic == "cmp" || mnemonic.StartsWith('b'))
                    {
                        // Not Implemented
                    }

                    break;
                }
            }
        }

        return result;
    }

    private static ulong? CalculateTarget(InstructionWithAddress instr)
    {
        var operandString = instr.Operand;
        if (string.IsNullOrEmpty(operandString)) return null;
        var ops = operandString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (ops.Length <= 0 || !ops[0].StartsWith('#')) return null;
        var offsetStr = ops[0][1..];
        if (long.TryParse(offsetStr, out var offset)) return instr.Address + (ulong)offset;

        return null;
    }

    public class ArmCallInfo
    {
        public ulong Address;
        public Dictionary<string, string> Args = [];
        public string? Target;
    }
}