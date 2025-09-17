# FbsDumper
A tool to recover FlatBuffer schema definitions from game assemblies using assembly instruction parsing.

Originally made for **Blue Archive**, should theoretically work with other games but is untested.

FBS Dumper V1 dumped from DummyDll, while V2 utilizes both DummyDll and libil2cpp.

## Usage
```bash
# Build the project
dotnet publish --configuration Release

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
- `-dv, --dump-version`: Specifies the dump version (Default: V2)
- `-f, --force`: Force dump for V1 dumper. (Default: true)
- `-s, --force-snake-case`: Force snake case conversion
- `-nl, --namespace-to-look-for`: Specifies the namespace to look for
- `-v, --verbose`: Enable verbose debug logging
- `-sw, --suppress-warnings`: Suppress warning messages

## TODO
- Default values support

> [!IMPORTANT]  
> **Disclaimer:** This software is made solely for educational purposes. I do not claim any responsibility for any usage of this software.

Copyright© Hiro420