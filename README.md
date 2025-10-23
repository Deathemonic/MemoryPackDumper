# MemoryPackDumper

A tool to recover MemoryPack C# class definitions from IL2CPP game assemblies.

*Originally made for **Blue Archive**, should theoretically work with other games that use MemoryPack.*

## Install

You can download the latest pre-build binaries at [Releases](https://github.com/Deathemonic/MemoryPackDumper/releases)

[Windows](https://github.com/Deathemonic/MemoryPackDumper/releases/latest/download/FbsDumper-v2.1.0-win-x64.zip) | [Linux](https://github.com/Deathemonic/MemoryPackDumper/releases/latest/download/FbsDumper-v2.1.0-linux-x64.zip) | [MacOS](https://github.com/Deathemonic/MemoryPackDumper/releases/latest/download/FbsDumper-v2.1.0-osx-arm64.zip) 


## Usage

```bash
# Show help
FbsDumper.exe --help

# Generate MemoryPack classes
FbsDumper.exe --dummy-dll "path/to/dummydll"

# Specify output file
FbsDumper.exe --dummy-dll "path/to/dummydll" --output-file "MemoryPack.cs"
```

## Build

1. Install [.NET SDK](https://dotnet.microsoft.com/en-us/download)
2. Clone this repository

```sh
git clone https://github.com/ArkanDash/FbsDumper
cd FbsDumper
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

