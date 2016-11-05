namespace Microsoft.DotNet.Tools.Compiler

open System
open Microsoft.DotNet.Cli.CommandLine
open Microsoft.DotNet.ProjectModel
open Microsoft.DotNet.Cli.Compiler.Common


module private AssemblyHelpers =

    type internal asmOpt = AssemblyInfoOptions

    let addOption (app:CommandLineApplication ) (optionName:string) (description:string) =
        app.Option(sprintf  "--%s <arg>" optionName, description, CommandOptionType.SingleValue)

    let unescapeNewlines (text:string) =
        if String.IsNullOrEmpty text then text else
        text.Replace("\\r", "\r").Replace("\\n", "\n")

open AssemblyHelpers

type AssemblyInfoOptionsCommandLine () =
    member val VersionOption              = CommandOption () with get, set
    member val TitleOption                = CommandOption () with get, set
    member val DescriptionOption          = CommandOption () with get, set
    member val CopyrightOption            = CommandOption () with get, set
    member val NeutralCultureOption       = CommandOption () with get, set
    member val CultureOption              = CommandOption () with get, set
    member val InformationalVersionOption = CommandOption () with get, set
    member val FileVersionOption          = CommandOption () with get, set
    member val TargetFrameworkOption      = CommandOption () with get, set


    static member AddOptions (app:CommandLineApplication) =
        let addOption = addOption app
        let commandLineOptions = AssemblyInfoOptionsCommandLine ()
        commandLineOptions.VersionOption                  <- addOption asmOpt.AssemblyVersionOptionName      "Assembly version"
        commandLineOptions.TitleOption                    <- addOption asmOpt.TitleOptionName                "Assembly title"
        commandLineOptions.DescriptionOption              <- addOption asmOpt.DescriptionOptionName          "Assembly description"
        commandLineOptions.CopyrightOption                <- addOption asmOpt.CopyrightOptionName            "Assembly copyright"
        commandLineOptions.NeutralCultureOption           <- addOption asmOpt.NeutralCultureOptionName       "Assembly neutral culture"
        commandLineOptions.CultureOption                  <- addOption asmOpt.CultureOptionName              "Assembly culture"
        commandLineOptions.InformationalVersionOption     <- addOption asmOpt.InformationalVersionOptionName "Assembly informational version"
        commandLineOptions.FileVersionOption              <- addOption asmOpt.AssemblyFileVersionOptionName  "Assembly file version"
        commandLineOptions.TargetFrameworkOption          <- addOption asmOpt.TargetFrameworkOptionName      "Assembly target framework"
        commandLineOptions


    member self.GetOptionValues () =
        AssemblyInfoOptions
            (   AssemblyVersion      = unescapeNewlines (self.VersionOption.Value              ())
            ,   Title                = unescapeNewlines (self.TitleOption.Value                ())
            ,   Description          = unescapeNewlines (self.DescriptionOption.Value          ())
            ,   Copyright            = unescapeNewlines (self.CopyrightOption.Value            ())
            ,   NeutralLanguage      = unescapeNewlines (self.NeutralCultureOption.Value       ())
            ,   Culture              = unescapeNewlines (self.CultureOption.Value              ())
            ,   InformationalVersion = unescapeNewlines (self.InformationalVersionOption.Value ())
            ,   AssemblyFileVersion  = unescapeNewlines (self.FileVersionOption.Value          ())
            ,   TargetFramework      = unescapeNewlines (self.TargetFrameworkOption.Value      ())
            )

