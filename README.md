# FbsDumper
A tool to recover FlatBuffer schema definitions from game assemblies using assembly instruction parsing.

Originally made for Blue Archive, should theoretically work with other games but is untested.

## Usage
```bash
# Build the project
dotnet build

# Run with required parameters
FbsDumper.exe --dummy-dll "path/to/DummyDll" --game-assembly "path/to/libil2cpp.so"

# Show help
FbsDumper.exe --help
```

### Options
- `-d, --dummy-dll`: Specifies the dummy DLL directory (Required)
- `-a, --game-assembly`: Specifies the path to libil2cpp.so (Required)  
- `-o, --output-file`: Specifies the output file (Default: BlueArchive.fbs)
- `-n, --namespace`: Specifies the flatdata namespace (Default: FlatData)
- `-s, --force-snake-case`: Force snake case conversion
- `-nl, --namespace-to-look-for`: Specifies the namespace to look for

## TODO
- Default values support

> [!IMPORTANT]  
> **Disclaimer:** This software is made 100% for educational purposes only. I do not claim any responsibility for any usage of this software.

Copyright© Hiro420