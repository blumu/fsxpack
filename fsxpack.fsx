/// FsxPack by William Blum.
/// Pack an fsx file and all its DLL references.

let (++) s1 s2 = System.IO.Path.Combine(s1, s2)

module Input =
    let topFsx = 
        match System.Environment.GetCommandLineArgs() with
        | [|fsi; fsxpack; topFsx|] -> 
            topFsx
        | argv -> 
            eprintfn "Invalid parameters: %s" (System.String.Join(" ", argv))
            eprintfn "Syntax is \"fsi.exe fsxpack.fsx <ScriptToPack.fsx>\""
            failwith "Invalid parameters."

    let rootDirectory =
        System.IO.Path.GetDirectoryName(topFsx)

    let outputDir = System.IO.Directory.GetCurrentDirectory() ++ "pack"

open System

module Marker =
    let Include = "#I"
    let Load = "#load"
    let Reference = "#r"

let parseMetas fsxFile =
     System.IO.File.ReadLines fsxFile
     |> Seq.where (fun l -> l.StartsWith("#")) 
     |> Seq.toList

let metas = parseMetas Input.topFsx

let extractMarker (marker:string) (s:string) =
    if s.StartsWith(marker) then
        s.Substring(marker.Length) |> Some
    else
        None

/// Active pattern to parse meta FSX commands
let (|Sharp|_|) marker s = 
    extractMarker marker s

let trimQuotes (s:string) =
    s.Trim([|'"';' '; '@'|]) // yes this is a hack, and we should handle escapes wihtout @ character...


let includes = metas
            |> Seq.where (fun i -> i.StartsWith(Marker.Include))
            |> Seq.choose (extractMarker Marker.Include)
            |> Seq.map trimQuotes
            |> Seq.map (fun i -> Input.rootDirectory ++ i)
            |> Seq.toList
            |> List.append [ Input.rootDirectory ] // add an include for current directory

let tryResolveLoad file =
    includes
    |> Seq.map (fun p -> p ++ file)
    |> Seq.tryFind System.IO.File.Exists
    |> Option.map(System.IO.Path.GetFullPath)

let resolveLoad file =
    match tryResolveLoad file with
    | None -> failwithf "Could not located script %s" file
    | Some path -> System.IO.Path.GetFullPath(path)

let loads = metas 
            |> Seq.choose (extractMarker Marker.Load)
            |> Seq.map trimQuotes
            |> Seq.toList
            |> Seq.map resolveLoad
            |> Seq.toList

let allReferences =
    loads
    |> Seq.map(fun l ->
                    printfn "Reading: [%s]" l
                    parseMetas l)
    |> Seq.concat
    |> Seq.choose (extractMarker "#r")
    |> Seq.map trimQuotes
    |> Seq.toArray
    |> Seq.toList

let resolvedReferences =
    allReferences
    |> Seq.choose (fun r -> 
        match tryResolveLoad r with
        | None -> printfn "Warning: cannot locate reference %s (Ignore this warning if this assembly is expected to be in the GAC." r; None
        | Some s -> Some s)

printfn "Resolving references..."
resolvedReferences
|> Seq.toList

printfn "Packing..."

let copy subdir sourceFile = 
    let name = System.IO.Path.GetFileName sourceFile
    let outputDir = Input.outputDir ++ subdir
    let output = outputDir ++ name
    let o = System.IO.Directory.CreateDirectory outputDir
    System.IO.File.Copy(sourceFile, output, true)
    printf "Copied %s" output

let transform subdir f sourceFile = 
    let name = System.IO.Path.GetFileName sourceFile
    let outputDir = Input.outputDir ++ subdir
    let output = outputDir ++ name
    let o = System.IO.Directory.CreateDirectory outputDir
    printf "Generating [%s]" output
    let transformedContent = (System.IO.File.ReadAllLines sourceFile) |> f
    System.IO.File.WriteAllLines(output, transformedContent)
    printf "File written: %s" output

printfn "Copying references..."
resolvedReferences |> Seq.iter(copy "refs")

let patchReferencesInScript =
    Array.map (fun l ->
                    match l with
                    | Sharp Marker.Include x ->
                        let fix = (System.IO.Path.GetFileName (trimQuotes x))
                        sprintf "/// %s \"%s\" // Commented out by FsxPack" Marker.Include fix
                    | Sharp Marker.Reference x ->
                        let fix = (System.IO.Path.GetFileName (trimQuotes x))
                        sprintf "%s \"%s\" // Path adjusted by FsxPack" Marker.Reference fix
                    | Sharp Marker.Load x ->
                        let fix = (System.IO.Path.GetFileName (trimQuotes x))
                        sprintf "%s \"%s\" // Path adjusted by FsxPack" Marker.Load fix
                    | line ->
                        line)

let addIncludeSearchDirectory content =
    Array.append
        [| "#I \"refs\" // References packed by FsxPack"
        |] content

printfn "Patching top-level script..."
transform  "." (patchReferencesInScript >> addIncludeSearchDirectory) Input.topFsx


printfn "Patching auxiliary scripts..."
loads |> Seq.iter (transform  "." patchReferencesInScript)

printfn "\n\nSuccessfully packed %s to directory %s" Input.topFsx Input.outputDir
printfn "You can run the packed script with the following command:"
printfn "    fsi.exe %s\%s " Input.outputDir (System.IO.Path.GetFileName Input.topFsx)
