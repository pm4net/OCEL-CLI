open System
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
    | [<AltCommandLine("--v")>] Verbose
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
            | Verbose -> "Prints detailed information about the conversion."
            | ConvertDir _ -> "Convert a directory of OCEL files."
            | ConvertMergeDir _ -> "Convert and merge a directory of OCEL files into a single file."
            | ConvertFiles _ -> "Convert one or more OCEL files."
            | ConvertMergeFiles _ -> "Convert and merge one or more OCEL files into a single file."

[<EntryPoint>]
let main args =
    let argParser = ArgumentParser.Create<Args>(programName = "ocel-cli")
    try
        let results = argParser.ParseCommandLine args
        let outputFormat = results.GetResult OutputFormat
        let inputFormat = results.TryGetResult InputFormat
        let verbose = results.Contains Verbose

        let cmds = 
            results.TryGetResult ConvertDir, 
            results.TryGetResult ConvertMergeDir, 
            results.TryGetResult ConvertFiles, 
            results.TryGetResult ConvertMergeFiles

        match cmds with
        | Some cd, None, None, None -> failwith ""
        | None, Some cmd, None, None -> failwith ""
        | None, None, Some cf, None -> failwith ""
        | None, None, None, Some cmf -> failwith ""
        | _ -> failwith "Only one sub-command allowed at a time."

        ()
    with e ->
        printfn "%s" e.Message

    0