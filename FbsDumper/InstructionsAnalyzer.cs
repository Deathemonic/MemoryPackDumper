using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gee.External.Capstone;
using Gee.External.Capstone.Arm64;

namespace FbsDumper;

internal class InstructionsAnalyzer
{

	public class ArmCallInfo
	{
		public ulong Address;
		public string Target;
		public Dictionary<string, string> Args = new();
	}

	public List<ArmCallInfo> AnalyzeCalls(List<Arm64Instruction> instructions)
	{
		var result = new List<ArmCallInfo>();
		var regState = new Dictionary<string, string>();

		foreach (var instr in instructions)
		{
			string mnemonic = instr.Mnemonic;
			string[] ops = instr.Operand?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? Array.Empty<string>();

			if (mnemonic == "mov" || mnemonic == "movz")
			{
				if (ops.Length == 2)
				{
					regState[ops[0]] = ops[1];
				}
			}
			else if (mnemonic == "movk" || mnemonic == "movn")
			{
				if (ops.Length >= 1)
					regState[ops[0]] = $"<{mnemonic}>";
			}
			else if (mnemonic == "bl" || mnemonic == "b")
			{
				var call = new ArmCallInfo
				{
					Address = (ulong)instr.Address,
					Target = ops.Length > 0 ? ops[0] : "<unknown>",
					Args = new Dictionary<string, string>()
				};

				for (int i = 0; i <= 7; i++)
				{
					string xReg = $"x{i}";
					string wReg = $"w{i}";

					if (regState.ContainsKey(xReg))
						call.Args[xReg] = regState[xReg];
					else if (regState.ContainsKey(wReg))
						call.Args[wReg] = regState[wReg];
				}

				result.Add(call);
			}
			else if (mnemonic == "cbz" || mnemonic == "cmp" || mnemonic.StartsWith("b"))
			{
				// not handle for now
			}
		}

		return result;
	}

}
