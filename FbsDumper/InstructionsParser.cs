using Mono.Cecil;
using Gee.External.Capstone;
using Gee.External.Capstone.Arm64;

namespace FbsDumper;

internal class InstructionsParser
{
	string gameAssemblyPath;
	byte[] fileBytes;

	public InstructionsParser(string _gameAssemblyPath)
	{
		gameAssemblyPath = _gameAssemblyPath;
		fileBytes = File.ReadAllBytes(gameAssemblyPath);
	}

	public List<Arm64Instruction> GetInstructions(MethodDefinition targetMethod, bool debug = false)
	{
		long rva = GetMethodRVA(targetMethod);
		if (rva == 0)
		{
			Console.WriteLine($"[!] Invalid RVA or offset for method: {targetMethod.FullName}");
			return new List<Arm64Instruction>();
		}

		return GetInstructions(rva, debug);
	}

	public List<Arm64Instruction> GetInstructions(long RVA, bool debug = false)
	{
		var instructions = new List<Arm64Instruction>();

		using var capstone = CapstoneDisassembler.CreateArm64Disassembler(Arm64DisassembleMode.LittleEndian);
		capstone.EnableInstructionDetails = true;
		const int instrSize = 4;
		long currentOffset = RVA;

		while (currentOffset + instrSize <= fileBytes.Length)
		{
			var instrBytes = new byte[instrSize];
			Array.Copy(fileBytes, currentOffset, instrBytes, 0, instrSize);

			var decoded = capstone.Disassemble(instrBytes, currentOffset);
			if (decoded.Length == 0)
				break;

			var instr = decoded[0];
			instructions.Add(instr);

			if (debug)
				Console.WriteLine($"\t0x{instr.Address:X}: {instr.Mnemonic} {instr.Operand}");

			currentOffset += instrSize;

			if (instr.Mnemonic == "ret")
				break;
		}

		return instructions;
	}


	public static long GetMethodRVA(MethodDefinition method)
	{
		if (!method.HasCustomAttributes)
			return 0;

		var customAttr = method.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == "AddressAttribute");
		if (customAttr == null || !customAttr.HasFields)
			return 0;

		var argRVA = customAttr.Fields.First(f => f.Name == "RVA");
		long rva = Convert.ToInt64(argRVA.Argument.Value.ToString()?.Substring(2), 16);
		return rva;
	}
}
