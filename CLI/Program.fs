open System
open System.IO
open LiteDB
open Argu

type OcelFormat =
    | Json = 1
    | Xml = 2
    | LiteDb = 3

and ConvertDirArgs =
    | [<Mandatory; AltCommandLine("--d")>] Dir of string
    | [<Mandatory; AltCommandLine("--o")>] OutDir of string

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Dir _ -> "The directory in which the OCEL files are located."
            | OutDir _ -> "The output directory in which to place the converted OCEL files."

and ConvertMergeDirArgs =
    | [<Mandatory; AltCommandLine("--d")>] Dir of string
    | [<Mandatory; AltCommandLine("--o")>] Out of string

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Dir _ -> "The directory in which the OCEL files are located."
            | Out _ -> "The output file to which the merged OCEL files are written."

and ConvertMergeFilesArgs =
    | [<Mandatory; AltCommandLine("--f")>] Files of string list
    | [<Mandatory; AltCommandLine("--o")>] Out of string

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Files _ -> "The OCEL files to convert."
            | Out _ -> "The output file to which the merged OCEL files are written."

and ConvertFilesArgs =
    | [<Mandatory; AltCommandLine("--f")>] Files of string list

    interface IArgParserTemplate with

        member this.Usage =
            match this with
            | Files _ -> "The OCEL files to convert."

and Args =
    | Version
    | [<Mandatory; ExactlyOnce; AltCommandLine("--of")>] OutputFormat of OcelFormat
    | [<AltCommandLine("--if")>] InputFormat of OcelFormat option
    | Indented
    | [<CliPrefix(CliPrefix.None); AltCommandLine("cd")>] ConvertDir of ParseResults<ConvertDirArgs>
    | [<CliPrefix(CliPrefix.None); AltCommandLine("cmd")>] ConvertMergeDir of ParseResults<ConvertMergeDirArgs>
    | [<CliPrefix(CliPrefix.None); AltCommandLine("cf")>] ConvertFiles of ParseResults<ConvertFilesArgs>
    | [<CliPrefix(CliPrefix.None); AltCommandLine("cmf")>] ConvertMergeFiles of ParseResults<ConvertMergeFilesArgs>

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Version -> "Prints the version of the OCEL CLI."
            | OutputFormat _ -> "Output format of the conversion."
            | InputFormat _ -> "Only include files of the specified format."
            | Indented -> "Specifies that output files should be formatted using indentation."
            | ConvertDir _ -> "Convert a directory of OCEL files."
            | ConvertMergeDir _ -> "Convert and merge a directory of OCEL files into a single file."
            | ConvertFiles _ -> "Convert one or more OCEL files."
            | ConvertMergeFiles _ -> "Convert and merge one or more OCEL files into a single file."

/// Get OCEL files in a directory (throws exception if directory not found)
let private ocelFilesInDir dir =
    dir
    |> Directory.GetFiles 
    |> Array.toList 
    |> List.filter (fun f -> 
        f.EndsWith ".json" ||
        f.EndsWith ".jsonocel" ||
        f.EndsWith ".xml" ||
        f.EndsWith ".xmlocel" ||
        f.EndsWith ".db" ||
        f.EndsWith ".litedb"
    )

/// Get the file extension of an OCEL format
let private fileExtensionForFormat format =
    match format with
    | OcelFormat.Json -> ".jsonocel"
    | OcelFormat.Xml -> ".xmlocel"
    | OcelFormat.LiteDb -> ".db"
    | _ -> raise (ArgumentOutOfRangeException(nameof(format)))

/// Find all files with the specified OCEL format in a directory
let private findFilesByFormat dir format =
    match format with
    | Some format -> 
        ocelFilesInDir dir 
        |> List.filter (fun f ->
            match format with
            | OcelFormat.Json -> f.EndsWith ".json" || f.EndsWith ".jsonocel"
            | OcelFormat.Xml -> f.EndsWith ".xml" || f.EndsWith ".xmlocel"
            | OcelFormat.LiteDb -> f.EndsWith ".db" || f.EndsWith ".litedb"
            | _ -> raise (new ArgumentOutOfRangeException(nameof(format)))
        )
    | None -> ocelFilesInDir dir

/// Generate the new file name of an input file, considering its output directory and format
let private getNewFileName (inputFile: string) (outDir: string) format =
    let fileName = inputFile |> Path.GetFileNameWithoutExtension
    let fileFullName = fileName + fileExtensionForFormat format
    Path.Combine(outDir, fileFullName)

/// Read an OCEL file into a log object
let private readOcelFile (path: string) =
    try
        match path with
        | p when p.EndsWith ".json" || p.EndsWith ".jsonocel" -> path |> File.ReadAllText |> OCEL.OcelJson.deserialize |> Some
        | p when p.EndsWith ".xml" || p.EndsWith ".xmlocel" -> path |> File.ReadAllText |> OCEL.OcelXml.deserialize |> Some
        | p when p.EndsWith ".db" || p.EndsWith ".litedb" -> new LiteDatabase $"Filename={p};ReadOnly=true;" |> OCEL.OcelLiteDB.deserialize |> Some
        | _ -> failwith "File is not of any of the supported formats (.jsonocel, .xmlocel, .db, .litedb)."
    with
    | e -> 
        printfn $"Failed to read or deserialize file {path}: {e.Message}."
        None

[<EntryPoint>]
let main args =
    let argParser = ArgumentParser.Create<Args>(programName = "ocel-cli")
    try
        let results = argParser.ParseCommandLine args
        let outputFormat = results.GetResult OutputFormat
        let inputFormat = 
            match results.TryGetResult InputFormat with
            | Some(Some inputFormat) -> Some inputFormat
            | _ -> None
        let formatting = if results.Contains Indented then OCEL.Types.Formatting.Indented else OCEL.Types.Formatting.None

        let cmds = 
            results.TryGetResult ConvertDir, 
            results.TryGetResult ConvertMergeDir, 
            results.TryGetResult ConvertFiles, 
            results.TryGetResult ConvertMergeFiles

        match cmds with
        | Some cd, None, None, None ->
            let dir = cd.GetResult ConvertDirArgs.Dir
            let outDir = cd.GetResult ConvertDirArgs.OutDir
            
            findFilesByFormat dir inputFormat
            |> fun files ->
                printfn $"Found {files.Length} matching files in directory."
                files
            |> List.map (fun f -> f, readOcelFile f)
            |> List.iter (fun (name, log) ->
                match log with
                | Some log ->
                    match outputFormat with
                    | OcelFormat.Json ->
                        let fileName = getNewFileName name outDir outputFormat
                        let json = OCEL.OcelJson.serialize formatting log
                        printfn $"Writing log to {fileName}."
                        File.WriteAllText(fileName, json)
                    | OcelFormat.Xml ->
                        let fileName = getNewFileName name outDir outputFormat
                        let xml = OCEL.OcelXml.serialize formatting log
                        printfn $"Writing log to {fileName}."
                        File.WriteAllText(fileName, xml)
                    | OcelFormat.LiteDb ->
                        let fileName = getNewFileName name outDir outputFormat
                        let outDb = new LiteDatabase(fileName)
                        printfn $"Writing log to {fileName}."
                        OCEL.OcelLiteDB.serialize outDb log
                    | _ -> raise (new ArgumentOutOfRangeException(nameof(outputFormat)))
                | None -> ()
            )
        | None, Some cmd, None, None ->
            let dir = cmd.GetResult ConvertMergeDirArgs.Dir
            let out = cmd.GetResult ConvertMergeDirArgs.Out

            let mergedLog =
                findFilesByFormat dir inputFormat
                |> fun files ->
                    printfn $"Found {files.Length} matching files in directory."
                    files
                |> List.map (fun f -> readOcelFile f)
                |> List.choose id
                |> fun files ->
                    printfn $"Merging {files.Length} files into one."
                    files
                |> List.fold (fun (state: OCEL.Types.OcelLog) log -> state.MergeWith log) OCEL.Types.OcelLog.Empty

            match outputFormat with
            | OcelFormat.Json ->
                let json = OCEL.OcelJson.serialize formatting mergedLog
                printfn $"Writing log to {out}."
                File.WriteAllText(out, json)
            | OcelFormat.Xml ->
                let xml = OCEL.OcelXml.serialize formatting mergedLog
                printfn $"Writing log to {out}."
                File.WriteAllText(out, xml)
            | OcelFormat.LiteDb ->
                let outDb = new LiteDatabase(out)
                printfn $"Writing log to {out}."
                OCEL.OcelLiteDB.serialize outDb mergedLog
            | _ -> raise (new ArgumentOutOfRangeException(nameof(outputFormat)))
        | None, None, Some cf, None -> raise (NotImplementedException())
        | None, None, None, Some cmf -> raise (NotImplementedException())
        | _ -> failwith "Only one sub-command allowed at a time."
    with e ->
        printfn "%s" e.Message

    0