namespace Microsoft.DotNet.Tools.Compiler

open System
open Microsoft.DotNet.Cli.CommandLine
open Microsoft.DotNet.ProjectModel
open Microsoft.DotNet.Cli.Compiler.Common


module private CompilerHelpers =

    type internal comOpt = CommonCompilerOptionsExtensions

    let addOption (app:CommandLineApplication ) (optionName:string) (optionType:CommandOptionType) (description:string)  =
        let argSuffix =
            if optionType = CommandOptionType.MultipleValue then "..." else String.Empty
        let argString =
            if optionType = CommandOptionType.BoolValue then String.Empty else sprintf " <arg>%s" argSuffix
        app.Option (sprintf "--%s%s" optionName argString, description, optionType)

open CompilerHelpers

type CommonCompilerOptionsCommandLine() =

    member val DefineOption                   = CommandOption () with get, set
    member val SuppressWarningOption          = CommandOption () with get, set
    member val LanguageVersionOption          = CommandOption () with get, set
    member val PlatformOption                 = CommandOption () with get, set
    member val AllowUnsafeOption              = CommandOption () with get, set
    member val WarningsAsErrorsOption         = CommandOption () with get, set
    member val OptimizeOption                 = CommandOption () with get, set
    member val KeyFileOption                  = CommandOption () with get, set
    member val DelaySignOption                = CommandOption () with get, set
    member val PublicSignOption               = CommandOption () with get, set
    member val DebugTypeOption                = CommandOption () with get, set
    member val EmitEntryPointOption           = CommandOption () with get, set
    member val GenerateXmlDocumentationOption = CommandOption () with get, set
    member val AdditionalArgumentsOption      = CommandOption () with get, set
    member val OutputNameOption               = CommandOption () with get, set

    static member AddOptions (app:CommandLineApplication) : CommonCompilerOptionsCommandLine =
        let addOption = addOption app
        let commandLineOptions = CommonCompilerOptionsCommandLine ()
        commandLineOptions.DefineOption                   <- addOption comOpt.DefineOptionName                   MultipleValue    "Preprocessor definitions"
        commandLineOptions.SuppressWarningOption          <- addOption comOpt.SuppressWarningOptionName          MultipleValue    "Suppresses the specified warning"
        commandLineOptions.LanguageVersionOption          <- addOption comOpt.LanguageVersionOptionName          SingleValue      "The version of the language used to compile"
        commandLineOptions.PlatformOption                 <- addOption comOpt.PlatformOptionName                 SingleValue      "The target platform"
        commandLineOptions.AllowUnsafeOption              <- addOption comOpt.AllowUnsafeOptionName              BoolValue        "Allow unsafe code"
        commandLineOptions.WarningsAsErrorsOption         <- addOption comOpt.WarningsAsErrorsOptionName         BoolValue        "Turn all warnings into errors"
        commandLineOptions.OptimizeOption                 <- addOption comOpt.OptimizeOptionName                 BoolValue        "Enable compiler optimizations"
        commandLineOptions.KeyFileOption                  <- addOption comOpt.KeyFileOptionName                  SingleValue      "Path to file containing the key to strong-name sign the output assembly"
        commandLineOptions.DelaySignOption                <- addOption comOpt.DelaySignOptionName                BoolValue        "Delay-sign the output assembly"
        commandLineOptions.PublicSignOption               <- addOption comOpt.PublicSignOptionName               BoolValue        "Public-sign the output assembly"
        commandLineOptions.DebugTypeOption                <- addOption comOpt.DebugTypeOptionName                SingleValue      "The type of PDB to emit: portable or full"
        commandLineOptions.EmitEntryPointOption           <- addOption comOpt.EmitEntryPointOptionName           BoolValue        "Output an executable console program"
        commandLineOptions.GenerateXmlDocumentationOption <- addOption comOpt.GenerateXmlDocumentationOptionName BoolValue        "Generate XML documentation file"
        commandLineOptions.AdditionalArgumentsOption      <- addOption comOpt.AdditionalArgumentsOptionName      MultipleValue    "Pass the additional argument directly to the compiler"
        commandLineOptions.OutputNameOption               <- addOption comOpt.OutputNameOptionName               SingleValue      "Output assembly name"
        commandLineOptions


    member self.GetOptionValues () =
        CommonCompilerOptions
            (   Defines                  = self.DefineOption.Values
            ,   SuppressWarnings         = self.SuppressWarningOption.Values
            ,   LanguageVersion          = self.LanguageVersionOption.Value()
            ,   Platform                 = self.PlatformOption.Value()
            ,   AllowUnsafe              = self.AllowUnsafeOption.BoolValue
            ,   WarningsAsErrors         = self.WarningsAsErrorsOption.BoolValue
            ,   Optimize                 = self.OptimizeOption.BoolValue
            ,   KeyFile                  = self.KeyFileOption.Value()
            ,   DelaySign                = self.DelaySignOption.BoolValue
            ,   PublicSign               = self.PublicSignOption.BoolValue
            ,   DebugType                = self.DebugTypeOption.Value()
            ,   EmitEntryPoint           = self.EmitEntryPointOption.BoolValue
            ,   GenerateXmlDocumentation = self.GenerateXmlDocumentationOption.BoolValue
            ,   AdditionalArguments      = self.AdditionalArgumentsOption.Values
            ,   OutputName               = self.OutputNameOption.Value()
            )


