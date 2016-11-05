namespace Microsfot.DotNet.Tools.Compiler.Fsc

open System
open System.Collections.Generic
open System.IO
open System.Linq
open System.Reflection
open System.Runtime.InteropServices
open System.Text
open Microsoft.DotNet.Cli
open Microsoft.DotNet.Cli.CommandLine
open Microsoft.DotNet.Cli.Compiler.Common
open Microsoft.DotNet.Cli.Utils
open Microsoft.DotNet.ProjectModel
open Microsoft.DotNet.ProjectModel.Resolution
open Microsoft.DotNet.Tools.Compiler
open Microsoft.FSharp.Linq.NullableOperators

module CompileFscCommand =

    type FscCommandSpec = {
        Spec       : CommandSpec
        FscExeDir  : string
        FscExeFile : string
    }


    let copyRuntimeConfigForFscExe
        (   runtimeConfigFile : string
        ,   commandName       : string
        ,   depsJsonFile      : string
        ,   nugetPackagesRoot : string
        ,   fscPath           : string
        ) =
        let newFscRuntimeConfigDir = Path.GetDirectoryName fscPath
        let newFscRuntimeConfigFile =
            Path.Combine
                (   newFscRuntimeConfigDir
                ,   Path.GetFileNameWithoutExtension fscPath + FileNameSuffixes.RuntimeConfigJson)
        try
            File.Copy(runtimeConfigFile, newFscRuntimeConfigFile, true)
        with
        | ex -> Reporter.Error.WriteLine "Failed to copy fsc runtimeconfig.json"
                raise ex


    let resolveFsc (fscArgs: string ResizeArray) (temp:string) : FscCommandSpec =
        let nugetPackagesRoot = PackageDependencyProvider.ResolvePackagesPath(null, null)
        let depsFile = Path.Combine(AppContext.BaseDirectory, "dotnet-compile-fsc" + FileNameSuffixes.DepsJson)

        let depsJsonCommandResolver = DepsJsonCommandResolver nugetPackagesRoot
        let dependencyContext = depsJsonCommandResolver.LoadDependencyContextFromFile depsFile
        let fscPath = depsJsonCommandResolver.GetCommandPathFromDependencyContext ("fsc", dependencyContext)

        let commandResolverArgs =
            CommandResolverArguments(CommandName = "fsc",CommandArguments = fscArgs,DepsJsonFile = depsFile)

        let fscCommandSpec = depsJsonCommandResolver.Resolve commandResolverArgs

        let runtimeConfigFile =
            Path.Combine
                (   Path.GetDirectoryName (typeof<FscCommandSpec>.GetTypeInfo().Assembly.Location)
                ,   "dotnet-compile-fsc" + FileNameSuffixes.RuntimeConfigJson
                )

        copyRuntimeConfigForFscExe (runtimeConfigFile, "fsc", depsFile, nugetPackagesRoot, fscPath)

        {   Spec        = fscCommandSpec
            FscExeDir   = Path.GetDirectoryName fscPath
            FscExeFile  = fscPath
        }


    let runFsc (fscArgs:string ResizeArray) (temp:string) =
        let fscEnvExe = Environment.GetEnvironmentVariable "DOTNET_FSC_PATH"
        let envar = Environment.GetEnvironmentVariable "DOTNET_FSC_EXEC"
        let envar = if String.IsNullOrEmpty envar then "COREHOST" else envar.ToUpper()
        let muxer = Muxer ()

        if isNotNull fscEnvExe then
            match envar with
            | "RUN" ->
                Command.Create (fscEnvExe, fscArgs.ToArray())
            | "COREHOST" | _ ->
                let host = muxer.MuxerPath
                Command.Create(host, [|fscEnvExe|].Concat(fscArgs).ToArray())
        else
            Command.Create ((resolveFsc fscArgs temp).Spec)


    let getSourceFiles (sourceFiles:IReadOnlyList<string>) (assemblyInfo:string) = seq {
        if not ^ sourceFiles.Any() then
            yield assemblyInfo
        else
            for src in (sourceFiles.Take(sourceFiles.Count - 1)) do
                yield src
            yield assemblyInfo
            yield sourceFiles.Last()
    }

    let [<Literal>] ExitFailed = 1

    [<EntryPoint>]
    let main (args:string []) =
        DebugHelper.HandleDebugSwitch(ref args)
        let app =
            CommandLineApplication
                (   Name                = "dotnet compile-fsc"
                ,   FullName            = ".NET F# Compiler"
                ,   Description         = "F# Compiler for the .NET Platform"
                ,   HandleResponseFiles = true
                )

        app.HelpOption "-h|--help" |> ignore

        let commonCompilerCommandLine = CommonCompilerOptionsCommandLine.AddOptions app
        let assemblyInfoCommandLine   = AssemblyInfoOptionsCommandLine.AddOptions app
        let tempOutputOption          = app.Option   ("--temp-output <arg>", "Compilation temporary directory", SingleValue)
        let outputNameOption          = app.Option   ("--out <arg>", "Name of the output assembly", SingleValue)
        let referencesOption          = app.Option   ("--reference <arg>...", "Path to a compiler metadata reference", MultipleValue)
        let resourcesOption           = app.Option   ("--resource <arg>...", "Resources to embed", MultipleValue)
        let sourcesArgument           = app.Argument ("<source-files>...", "Compilation sources", multipleValues=true)

        app.OnExecute (fun () ->
            if not (tempOutputOption.HasValue()) then
                Reporter.Error.WriteLine "Option '--temp-output' is required"
                ExitFailed
            else
            let commonOptions = commonCompilerCommandLine.GetOptionValues()
            let assemblyInfoOptions = assemblyInfoCommandLine.GetOptionValues()

            // TODO less hacky
            let targetNetCore =
                commonOptions.Defines.Contains "DNXCORE50"
                ||  commonOptions.Defines.Where(fun d -> d.StartsWith("NETSTANDARDAPP1_")).Any()
                ||  commonOptions.Defines.Where(fun d -> d.StartsWith("NETCOREAPP1_")).Any()
                ||  commonOptions.Defines.Where(fun d -> d.StartsWith("NETSTANDARD1_")).Any()

            // Get FSC Path upfront to use it for win32manifest path
            let tempOutDir     = tempOutputOption.Value()
            let fscCommandSpec = resolveFsc (ResizeArray()) tempOutDir
            let fscExeFile     = fscCommandSpec.FscExeFile
            let fscExeDir      = fscCommandSpec.FscExeDir

            // FSC arguments
            let allArgs = ResizeArray<string>()

            //HACK fsc raise error FS0208 if target exe doesnt have extension .exe
            let hackFS0208 : bool = targetNetCore && commonOptions.EmitEntryPoint ?= true

            let mutable outputName = outputNameOption.Value()
            let originalOutputName = outputName

            if isNotNull outputName then
                if hackFS0208 then outputName <- Path.ChangeExtension (outputName, ".exe")

                allArgs.Add ^ sprintf "--out:%s" outputName

            //let's pass debugging type only if options.DebugType is specified, until
            //portablepdb are confirmed to work.
            //so it's possibile to test portable pdb without breaking existing build
            if String.IsNullOrEmpty commonOptions.DebugType then
                //debug info (only windows pdb supported, not portablepdb)
//                if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
//                    allArgs.Add "--debug"
//                    //TODO check if full or pdbonly
//                    allArgs.Add "--debug:pdbonly"
//                else
//                allArgs.Add "--debug-"
                ()
            else
                allArgs.Add "--debug"
                allArgs.Add ^ sprintf "--debug:%s" commonOptions.DebugType

            // Default options
            allArgs.Add "--noframework"
            allArgs.Add "--nologo"
            allArgs.Add "--simpleresolution"
            allArgs.Add "--nocopyfsharpcore"

            // project.json compilationOptions
            if isNotNull commonOptions.Defines then
                allArgs.AddRange(commonOptions.Defines.Select(fun def -> sprintf "--define:%s" def))

            if commonOptions.GenerateXmlDocumentation ?= true then
                allArgs.Add ^ sprintf "--doc:%s" (Path.ChangeExtension (outputName, "xml"))

            if isNotNull commonOptions.KeyFile then allArgs.Add ^ sprintf "--keyfile:%s" commonOptions.KeyFile

            if commonOptions.Optimize ?= true then allArgs.Add "--optimize+"

            //--resource doesnt expect "
            //bad: --resource:"path/to/file",name
            //ok:  --resource:path/to/file,name
            //   allArgs.AddRange(resourcesOption.Values.Select(resource => $"--resource:{resource.Replace("\"", "")}")); // commented to fix syntax highlight

            allArgs.AddRange(referencesOption.Values.Select(fun r -> sprintf "-r:%s" r))

            if commonOptions.EmitEntryPoint.HasValue then
                allArgs.Add "--target:library"
            else
                allArgs.Add "--target:exe"

            //HACK we need default.win32manifest for exe
            let win32manifestPath = Path.Combine (fscExeDir, "..", "..", "runtimes", "any", "native", "default.win32manifest");
            allArgs.Add ^ sprintf "--win32manifest:%s" win32manifestPath

            if isNotNull commonOptions.SuppressWarnings && commonOptions.SuppressWarnings.Any() then
                allArgs.Add ^ "--nowarn:" + String.Join(",", commonOptions.SuppressWarnings.ToArray())

            if isNotNull commonOptions.LanguageVersion then ()
                // Not used in fsc

            if isNotNull commonOptions.Platform then allArgs.Add ^ sprintf "--platform:%s" commonOptions.Platform

            if commonOptions.AllowUnsafe ?= true then ()

            if commonOptions.WarningsAsErrors ?= true then allArgs.Add "--warnaserror"

            //set target framework
            if targetNetCore then allArgs.Add "--targetprofile:netcore"
            if commonOptions.DelaySign ?= true then allArgs.Add "--delaysign+"
            if commonOptions.PublicSign ?= true then ()
            if isNotNull commonOptions.AdditionalArguments then
                // Additional arguments are added verbatim
                allArgs.AddRange commonOptions.AdditionalArguments

            // Generate assembly info
            let assemblyInfo = Path.Combine (tempOutDir, "dotnet-compile.assemblyinfo.fs")
            File.WriteAllText (assemblyInfo, AssemblyInfoFileGenerator.GenerateFSharp assemblyInfoOptions)

            //source files + assemblyInfo
            allArgs.AddRange((getSourceFiles sourcesArgument.Values assemblyInfo).ToArray())

            //TODO check the switch enabled in fsproj in RELEASE and DEBUG configuration

            let rsp = Path.Combine(tempOutDir, "dotnet-compile-fsc.rsp")
            File.WriteAllLines(rsp, allArgs, Encoding.UTF8)

            // Execute FSC!
            let result =
                (runFsc (ResizeArray [|sprintf "@%s" rsp |]) tempOutDir)
                    .ForwardStdErr().ForwardStdOut().Execute()

            let successFsc = result.ExitCode = 0

            if hackFS0208 && File.Exists outputName then
                if File.Exists originalOutputName then
                    File.Delete originalOutputName
                File.Move (outputName, originalOutputName)

            //HACK dotnet build require a pdb (crash without), fsc atm cant generate a portable pdb, so an empty pdb is created
            let pdbPath = Path.ChangeExtension (outputName, ".pdb")
            if successFsc && not(File.Exists pdbPath) then
                File.WriteAllBytes(pdbPath, ([||] : byte []))

            result.ExitCode
        )

        try app.Execute args
        with ex ->
        #if DEBUG
            Reporter.Error.WriteLine ^ ex.ToString()
        #else
            Reporter.Error.WriteLine ^ ex.Message
        #endif
            ExitFailed


