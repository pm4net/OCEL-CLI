# OCEL-CLI

A CLI tool to quickly convert and/or merge multiple **Object-Centric Event Log (OCEL)** [1] files to the various different formats. Uses the [OCEL](https://github.com/pm4net/OCEL) library in the background.

# Installation

Run `dotnet tool install --global ocel-cli` to install the tool from NuGet. If you cloned the project locally, run `dotnet tool install --global --add-source .\nupkg\ cli` from the CLI directory.

# Usage

There are 4 main usages with a corresponding command:

1. Convert all OCEL files in a directory to a specific format
2. Convert all OCEL files in a directory to a specific format, and merge them into a single file
3. Convert one or more OCEL files, given their specific path, to a specific format
4. Convert one or more OCEL files, given their specific path, to a specific format, and merge them into a single file

```
USAGE: ocel-cli [--help] --outputformat <json|xml|litedb> [--indented] [--removeunknownobjects]
                [--mergeduplicateobjects] [--novalidation] [<subcommand> [<options>]]

SUBCOMMANDS:

    convertdir, cd <options>
                          Convert a directory of OCEL files.
    convertmergedir, cmd <options>
                          Convert and merge a directory of OCEL files into a single file.
    convertfiles, cf <options>
                          Convert one or more OCEL files.
    convertmergefiles, cmf <options>
                          Convert and merge one or more OCEL files into a single file.

    Use 'ocel-cli <subcommand> --help' for additional information.

OPTIONS:

    --outputformat, --of <json|xml|litedb>
                          Output format of the conversion.
    --indented, --i       Specifies that output files should be formatted using indentation.
    --removeunknownobjects, --ruo
                          Remove any object references from events that don't exist in the log.
    --mergeduplicateobjects, --mdo
                          Specifies that identical objects should be merged into one and all event references updated.
    --novalidation, --nv  Specifies that the deserialized log(s) should not be validated before serializing again.
    --help                display this list of options.
```

## Convert all in directory

```
USAGE: ocel-cli convertdir [--help] --dir <string> --outdir <string> [--inputformat [<json|xml|litedb>]]

OPTIONS:

    --dir, --d <string>   The directory in which the OCEL files are located.
    --outdir, --o <string>
                          The output directory in which to place the converted OCEL files.
    --inputformat, --if [<json|xml|litedb>]
                          Only include files of the specified format.
    --help                display this list of options.
```

## Convert and merge all in directory

```
USAGE: ocel-cli convertmergedir [--help] --dir <string> --out <string> [--inputformat [<json|xml|litedb>]]

OPTIONS:

    --dir, --d <string>   The directory in which the OCEL files are located.
    --out, --o <string>   The output file to which the merged OCEL files are written.
    --inputformat, --if [<json|xml|litedb>]
                          Only include files of the specified format.
    --help                display this list of options.
```

## Convert one or more

```
USAGE: ocel-cli convertfiles [--help] --files [<string>...]

OPTIONS:

    --files, --f [<string>...]
                          The OCEL files to convert. Will be placed in the same directory.
    --help                display this list of options.
```

## Convert and merge one or more

```
USAGE: ocel-cli convertmergefiles [--help] --files [<string>...] --out <string>

OPTIONS:

    --files, --f [<string>...]
                          The OCEL files to convert.
    --out, --o <string>   The output file to which the merged OCEL files are written.
    --help                display this list of options.
```

# Supported formats

The OCEL standard is defined for both JSON and XML. Both include a validation schema that is used by the library to validate input.

An additional useful format is to store OCEL data in document databases such as MongoDB [2]. A very good alternative for .NET is [LiteDB](https://www.litedb.org/), which is an embedded NoSQL database that is similar to MongoDB. It allows writing to files directly and does not require a database server to use. Support for MongoDB will be evaluated in the future.

| Format        | Status        |
| ------------- |:-------------:|
| JSON          | Implemented   |
| XML           | Implemented   |
| LiteDB        | Implemented   |
| MongoDB       | TBD           |

# References

[1] Farhang, A., Park, G. G., Berti, A., & Aalst, W. Van Der. (2020). OCEL Standard. http://ocel-standard.org/

[2] Berti, A., Ghahfarokhi, A. F., Park, G., & van der Aalst, W. M. P. (2021). A Scalable Database for the Storage of Object-Centric Event Logs. CEUR Workshop Proceedings, 3098, 19–20. https://arxiv.org/abs/2202.05639.