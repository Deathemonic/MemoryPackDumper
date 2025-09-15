using FbsDumper.Instructions.Analyzers;

namespace FbsDumper.Instructions;

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