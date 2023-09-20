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
    | [<AltCommandLine("--if")>] InputFormat of OcelFormat option

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Dir _ -> "The directory in which the OCEL files are located."
            | OutDir _ -> "The output directory in which to place the converted OCEL files."
            | InputFormat _ -> "Only include files of the specified format."

and ConvertMergeDirArgs =
    | [<Mandatory; AltCommandLine("--d")>] Dir of string
    | [<Mandatory; AltCommandLine("--o")>] Out of string
    | [<AltCommandLine("--if")>] InputFormat of OcelFormat option

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Dir _ -> "The directory in which the OCEL files are located."
            | Out _ -> "The output file to which the merged OCEL files are written."
            | InputFormat _ -> "Only include files of the specified format."

and ConvertFilesArgs =
    | [<Mandatory; AltCommandLine("--f")>] Files of string list

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Files _ -> "The OCEL files to convert. Will be placed in the same directory."

and ConvertMergeFilesArgs =
    | [<Mandatory; AltCommandLine("--f")>] Files of string list
    | [<Mandatory; AltCommandLine("--o")>] Out of string

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Files _ -> "The OCEL files to convert."
            | Out _ -> "The output file to which the merged OCEL files are written."

and Args =
    | [<Mandatory; ExactlyOnce; AltCommandLine("--of")>] OutputFormat of OcelFormat
    | [<AltCommandLine("--i")>] Indented
    | [<AltCommandLine("--ruo")>] RemoveUnknownObjects
    | [<AltCommandLine("--nv")>] NoValidation
    | [<CliPrefix(CliPrefix.None); AltCommandLine("cd")>] ConvertDir of ParseResults<ConvertDirArgs>
    | [<CliPrefix(CliPrefix.None); AltCommandLine("cmd")>] ConvertMergeDir of ParseResults<ConvertMergeDirArgs>
    | [<CliPrefix(CliPrefix.None); AltCommandLine("cf")>] ConvertFiles of ParseResults<ConvertFilesArgs>
    | [<CliPrefix(CliPrefix.None); AltCommandLine("cmf")>] ConvertMergeFiles of ParseResults<ConvertMergeFilesArgs>

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | OutputFormat _ -> "Output format of the conversion."
            | Indented -> "Specifies that output files should be formatted using indentation."
            | RemoveUnknownObjects -> "Remove any object references from events that don't exist in the log."
            | NoValidation -> "Specifies that the deserialized log(s) should not be validated before serializing again."
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
let private readOcelFile removeUnknownObjects (path: string) =
    try
        printfn $"Reading and deserializing {path}"
        let log =
            match path with
            | p when p.EndsWith ".json" || p.EndsWith ".jsonocel" -> path |> File.ReadAllText |> OCEL.OcelJson.deserialize false
            | p when p.EndsWith ".xml" || p.EndsWith ".xmlocel" -> path |> File.ReadAllText |> OCEL.OcelXml.deserialize false
            | p when p.EndsWith ".db" || p.EndsWith ".litedb" -> new LiteDatabase $"Filename={p};ReadOnly=true;" |> OCEL.OcelLiteDB.deserialize
            | _ -> failwith "File is not of any of the supported formats (.jsonocel, .xmlocel, .db, .litedb)."
        match removeUnknownObjects with
        | true -> log.RemoveUnknownObjects() |> Some
        | false -> log |> Some
    with
    | e -> 
        printfn $"Failed to read or deserialize file {path}: {e.Message}."
        None

/// Read multiple OCEL files, merge them, and write them back to an output file in a specified format
let private mergeAndWriteToFile removeUnknownObjects outputFormat formatting validate files out =
    let mergedLog =
        files
        |> List.map (fun f -> readOcelFile removeUnknownObjects f)
        |> List.choose id
        |> fun files ->
            printfn $"Merging {files.Length} files into one."
            files
        |> List.fold (fun (state: OCEL.Types.OcelLog) log -> state.MergeWith log) OCEL.Types.OcelLog.Empty

    match outputFormat with
    | OcelFormat.Json ->
        printfn $"Writing log to {out}."
        let json = OCEL.OcelJson.serialize formatting validate mergedLog
        File.WriteAllText(out, json)
    | OcelFormat.Xml ->
        printfn $"Writing log to {out}."
        let xml = OCEL.OcelXml.serialize formatting validate mergedLog
        File.WriteAllText(out, xml)
    | OcelFormat.LiteDb ->
        printfn $"Writing log to {out}."
        let outDb = new LiteDatabase(out)
        OCEL.OcelLiteDB.serialize false outDb mergedLog
        outDb.Dispose()
    | _ -> raise (new ArgumentOutOfRangeException(nameof(outputFormat)))

[<EntryPoint>]
let main args =
    let argParser = ArgumentParser.Create<Args>(programName = "ocel-cli")
    try
        let results = argParser.ParseCommandLine args
        let outputFormat = results.GetResult OutputFormat
        let formatting = if results.Contains Indented then OCEL.Types.Formatting.Indented else OCEL.Types.Formatting.None
        let validate = results.Contains NoValidation |> not
        let removeUnknownObjects = results.Contains RemoveUnknownObjects

        let cmds = 
            results.TryGetResult ConvertDir, 
            results.TryGetResult ConvertMergeDir, 
            results.TryGetResult ConvertFiles, 
            results.TryGetResult ConvertMergeFiles

        match cmds with
        | Some cd, None, None, None ->
            let dir = cd.GetResult ConvertDirArgs.Dir
            let outDir = cd.GetResult ConvertDirArgs.OutDir
            let inputFormat =
                match cd.TryGetResult ConvertDirArgs.InputFormat with
                | Some(Some inputFormat) -> Some inputFormat
                | _ -> None
            
            findFilesByFormat dir inputFormat
            |> fun files ->
                printfn $"Found {files.Length} matching files in directory."
                files
            |> List.map (fun f -> f, readOcelFile removeUnknownObjects f)
            |> List.iter (fun (name, log) ->
                match log with
                | Some log ->
                    match outputFormat with
                    | OcelFormat.Json ->
                        let fileName = getNewFileName name outDir outputFormat
                        printfn $"Writing log to {fileName}."
                        let json = OCEL.OcelJson.serialize formatting validate log
                        File.WriteAllText(fileName, json)
                    | OcelFormat.Xml ->
                        let fileName = getNewFileName name outDir outputFormat
                        printfn $"Writing log to {fileName}."
                        let xml = OCEL.OcelXml.serialize formatting validate log
                        File.WriteAllText(fileName, xml)
                    | OcelFormat.LiteDb ->
                        let fileName = getNewFileName name outDir outputFormat
                        printfn $"Writing log to {fileName}."
                        let outDb = new LiteDatabase(fileName)
                        OCEL.OcelLiteDB.serialize false outDb log
                        outDb.Dispose()
                    | _ -> raise (new ArgumentOutOfRangeException(nameof(outputFormat)))
                | None -> ()
            )

        | None, Some cmd, None, None ->
            let dir = cmd.GetResult ConvertMergeDirArgs.Dir
            let out = cmd.GetResult ConvertMergeDirArgs.Out
            let inputFormat =
                match cmd.TryGetResult ConvertMergeDirArgs.InputFormat with
                | Some(Some inputFormat) -> Some inputFormat
                | _ -> None

            findFilesByFormat dir inputFormat
            |> fun files ->
                printfn $"Found {files.Length} matching files in directory."
                files
            |> fun files -> mergeAndWriteToFile removeUnknownObjects outputFormat formatting validate files out

        | None, None, Some cf, None ->
            cf.GetResult ConvertFilesArgs.Files
            |> List.map (fun f -> f, readOcelFile removeUnknownObjects f)
            |> List.iter (fun (name, log) ->
                match log with
                | Some log ->
                    let fileName = getNewFileName name (Path.GetDirectoryName name) outputFormat
                    match outputFormat with
                    | OcelFormat.Json ->
                        printfn $"Writing log to {fileName}."
                        let json = OCEL.OcelJson.serialize formatting validate log
                        File.WriteAllText(fileName, json)
                    | OcelFormat.Xml ->
                        printfn $"Writing log to {fileName}."
                        let xml = OCEL.OcelXml.serialize formatting validate log
                        File.WriteAllText(fileName, xml)
                    | OcelFormat.LiteDb ->
                        printfn $"Writing log to {fileName}."
                        let outDb = new LiteDatabase(fileName)
                        OCEL.OcelLiteDB.serialize false outDb log
                        outDb.Dispose()
                    | _ -> raise (new ArgumentOutOfRangeException(nameof(outputFormat)))
                | None -> ()
            )

        | None, None, None, Some cmf ->
            let files = cmf.GetResult ConvertMergeFilesArgs.Files
            let out = cmf.GetResult ConvertMergeFilesArgs.Out
            mergeAndWriteToFile removeUnknownObjects outputFormat formatting validate files out

        | _ -> failwith "Only one sub-command allowed at a time."
    with e ->
        printfn "%s" e.Message

    0