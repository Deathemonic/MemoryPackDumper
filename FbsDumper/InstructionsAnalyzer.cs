using AsmArm64;

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
                    var call = new ArmCallInfo
                    {
                        Address = instr.Address,
                        Target = ops.Length > 0 ? ops[0] : "<unknown>",
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

    public class ArmCallInfo
    {
        public ulong Address;
        public Dictionary<string, string> Args = [];
        public string? Target;
    }
}