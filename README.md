# fsxpack
A tool to pack an F# script for easy redistribution.

This tools parses your F# .fsx script, detects all `#r` referenced assemblies and pack everything into a 
self-contained folder that can be redistributed and executed on a differenet computer.
No need for .fsproj file, just edit your F# scripts in Visual Studio and run `fsxpack` to pack your script.


## Syntax

    fsi.exe fsxpack.fsx <YourFsxFile.fsx>
  
This creates a subdirectory `pack` containing `YourFsxFile.fsx` as well as auxiliary scripts loaded in your script with the `#load` meta-command. 
Assemblies referred in your script using `#r` are copied to the `pack\refs` subdirectory. The script itself is
updated to make it point to the `refs` dubdirectory instead of the original location (which might be outside of the fsx directory, for instance a nuget `packages` folder from a parent F# solution).


## Some TODOs

- Handle recursive FSX includes
- Use same assembly location resolution logic as the one used by F# interactive (`fsi.exe`). (Current logic used is a dirty hack but it works for most scripts.)
- Integrate into `fsi.exe` as `--pack` switch
