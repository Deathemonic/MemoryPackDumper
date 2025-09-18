# FbsDumper
A tool to recover FlatBuffer schema definitions from game assemblies using assembly instruction parsing.

Originally made for **Blue Archive**, should theoretically work with other games but is untested.

## Usage
```bash
# Show help
FbsDumper.exe --help

# Generate schema using assembly
FbsDumper.exe --dummy-dll "path/to/DummyDll" --game-assembly "path/to/libil2cpp.so"

# Generate schema without assembly
FbsDumper.exe --dummy-dll "path/to/DummyDll"
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

### Options
- `-d, --dummy-dll`: Specifies the dummy DLL directory (Required)
- `-a, --game-assembly`: Specifies the path to libil2cpp.so (ARM) or GameAssembly.dll (x86/x64) (Optional: Skip assembly analysis)
- `-o, --output-file`: Specifies the output file (Default: BlueArchive.fbs)
- `-n, --namespace`: Specifies the flatdata namespace (Default: FlatData)
- `-s, --force-snake-case`: Force snake case conversion
- `-nl, --namespace-to-look-for`: Specifies the namespace to look for
- `-f, --force`: Force processing using Add methods when no Create method exists
- `-v, --verbose`: Enable verbose debug logging
- `-sw, --suppress-warnings`: Suppress warning messages

## TODO
- Default values support

> [!IMPORTANT]  
> **Disclaimer:** This software is made solely for educational purposes. I do not claim any responsibility for any usage of this software.

Copyright© [Hiro420](https://github.com/Hiro420)