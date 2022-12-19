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
let private getOcelFilesInDir dir =
    dir
    |> Directory.GetFiles 
    |> Array.toList 
    |> List.filter (fun f -> 
        f.EndsWith ".jsonocel" ||
        f.EndsWith ".xmlocel" ||
        f.EndsWith ".db" ||
        f.EndsWith ".litedb"
    )

/// Get the file extension of an OCEL format
let private getFileExtension format =
    match format with
    | OcelFormat.Json -> ".jsonocel"
    | OcelFormat.Xml -> ".xmlocel"
    | OcelFormat.LiteDb -> ".db"
    | _ -> raise (ArgumentOutOfRangeException(nameof(format)))

/// Filter a list of file paths based on an OCEL format
let private filterByFormat format (filePaths: string list) =
    filePaths |> List.filter (fun f ->
        match format with
        | OcelFormat.Json -> getFileExtension OcelFormat.Json |> f.EndsWith
        | OcelFormat.Xml -> getFileExtension OcelFormat.Xml |> f.EndsWith
        | OcelFormat.LiteDb -> getFileExtension OcelFormat.LiteDb |> f.EndsWith || f.EndsWith ".litedb"
        | _ -> raise (new ArgumentOutOfRangeException(nameof(format)))
    )

/// Find all files with the specified OCEL format in a directory
let private findFilesByFormat dir format =
    match format with
    | Some format -> getOcelFilesInDir dir |> filterByFormat format
    | None -> getOcelFilesInDir dir

/// Generate the new file name of an input file, considering its output directory and format
let private getNewFileName (inputFile: string) (outDir: string) format =
    let fileName = inputFile |> Path.GetFileNameWithoutExtension
    let fileFullName = fileName + getFileExtension format
    Path.Combine(outDir, fileFullName)

/// Read an OCEL file into a log object
let private readOcelFile (path: string) =
    match path with
    | p when p.EndsWith ".jsonocel" -> path |> File.ReadAllText |> OCEL.OcelJson.deserialize
    | p when p.EndsWith ".xmlocel" -> path |> File.ReadAllText |> OCEL.OcelXml.deserialize
    | p when p.EndsWith ".db" || p.EndsWith ".litedb" -> new LiteDatabase $"Filename={p};ReadOnly=true;" |> OCEL.OcelLiteDB.deserialize
    | _ -> failwith "File is not of any of the supported formats (.jsonocel, .xmlocel, .db, .litedb)."

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
            )
        | None, Some cmd, None, None -> raise (NotImplementedException())
        | None, None, Some cf, None -> raise (NotImplementedException())
        | None, None, None, Some cmf -> raise (NotImplementedException())
        | _ -> failwith "Only one sub-command allowed at a time."
    with e ->
        printfn "%s" e.Message

    0