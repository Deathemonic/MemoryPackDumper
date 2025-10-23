# MemoryPackDumper

A tool to recover MemoryPack class definitions from game assemblies.

*Originally made for **Blue Archive**, should theoretically work with other games that use MemoryPack.*

## Install

You can download the latest pre-build binaries at [Releases](https://github.com/Deathemonic/MemoryPackDumper/releases)

[Windows](https://github.com/Deathemonic/MemoryPackDumper/releases/latest/download/MemoryPackDumper-win-x64.zip) | [Linux](https://github.com/Deathemonic/MemoryPackDumper/releases/latest/download/MemoryPackDumper-linux-x64.zip) | [MacOS](https://github.com/Deathemonic/MemoryPackDumper/releases/latest/download/MemoryPackDumper-osx-arm64.zip)

## Usage

```bash
# Show help
MemoryPackDumper.exe --help

# Generate MemoryPack classes
MemoryPackDumper.exe --dummy-dll "path/to/dummydll"

# Specify output file
MemoryPackDumper.exe --dummy-dll "path/to/dummydll" --output-file "MemoryPack.cs"
```

## Build

1. Install [.NET SDK](https://dotnet.microsoft.com/en-us/download)
2. Clone this repository

```sh
git clone https://github.com/Deathemonic/MemoryPackDumper
cd MemoryPackDumper
```

3. Build using `dotnet`

```sh
dotnet build
```

## Options

- `-d, --dummy-dll`: Specifies the dummy DLL directory (Required)
- `-o, --output-file`: Specifies the output file (Default: MemoryPack.cs)
- `-n, --namespace`: Specifies the C# namespace for generated classes (Default: FlatData)
- `-nl, --namespace-to-look-for`: Specifies the namespace to look for (filters types)
- `-v, --verbose`: Enable verbose debug logging
- `-sw, --suppress-warnings`: Suppress warning messages

> [!IMPORTANT]  
> **Disclaimer:** This software is made solely for educational purposes. I do not claim any responsibility for any usage
> of this software.

## Acknowledgement

- [ArkanDash/FbsDumper](https://github.com/ArkanDash/FbsDumper)
- [Hiro420/FbsDumperV2](https://github.com/Hiro420/FbsDumperV2)