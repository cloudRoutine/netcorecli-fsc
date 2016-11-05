System.IO.Directory.SetCurrentDirectory __SOURCE_DIRECTORY__
// FAKE build script
// --------------------------------------------------------------------------------------

#r "packages/FAKE/tools/FakeLib.dll"
open System
open Fake.AppVeyor
open Fake
open Fake.Git
open Fake.ReleaseNotesHelper
open Fake.UserInputHelper
open Fake.AssemblyInfoFile

// Information about the project to be used at NuGet and in AssemblyInfo files
// --------------------------------------------------------------------------------------

let project = "dotnet-compile-fsc"
let authors = ["Enrico Sada";"Jared Hester"]

let gitOwner = "dotnet"
let gitHome = "https://github.com/" + gitOwner

let gitName = "netcorecli-fsc"
let gitRaw = environVarOrDefault "gitRaw" "https://raw.githubusercontent.com/dotnet"

// The rest of the code is standard F# build script
// --------------------------------------------------------------------------------------


Target "Clean" (fun _ ->
    !! "src/**/bin"
    ++ "src/**/obj"
    ++ "test/**/bin"
    ++ "test/**/obj"
    ++ "bin"
    |> CleanDirs
)

// to run the build.fsx with a version number -
// build.cmd <TARGET> -ev VersionSuffix <num string> 
let versionSuffix = environVarOrDefault "VersionSuffix" "013072"

let assertExitCodeZero x = 
    if x = 0 then () else 
    failwithf "Command failed with exit code %i" x

let runCmdIn workDir exe = 
    Printf.ksprintf (fun args -> 
        Shell.Exec(exe, args, workDir) |> assertExitCodeZero)

/// Execute a dotnet cli command
let dotnet workDir = runCmdIn workDir "dotnet"


let root = __SOURCE_DIRECTORY__
let srcDir = root</>"src"
let testDir = root</>"test"
let binDir = root</>"bin"
let fsharpSdkDir = srcDir</>"FSharp.NET.Sdk"
let fscDir = srcDir</>"dotnet-compile-fsc"
let pkgOutputDir = root</>"bin"</>"packages"


Target "Build" (fun _ ->
    dotnet root "restore"
    // Build dotnet-compile-fsc nupkg
    dotnet fscDir "restore"
    dotnet fscDir "pack -c Release -o %s --version-suffix %s" pkgOutputDir versionSuffix
    // Build F# SDK nupkg
    dotnet fsharpSdkDir "restore"
    dotnet fsharpSdkDir "pack -c Release --output %s" pkgOutputDir
)


let testAppDir = testDir</>"TestApp"
let testAppWithArgsDir = testDir</>"TestAppWithArgs"
let testLibraryDir = testDir</>"TestLibrary"
let nugetConfig = testDir</>"Nuget.Config"
let consoleExampleDir = root</>"examples"</>"preview2"</>"console"
let libraryExampleDir = root</>"examples"</>"preview2"</>"lib"


Target "Test" (fun _ ->
    
    let restoreTest dir =
        dotnet dir "restore -v Information -f %s --configfile %s" binDir nugetConfig

    let runTest dir =
        restoreTest dir
        dotnet dir "build"
        dotnet dir "run"

    runTest testAppWithArgsDir
    runTest testLibraryDir
    runTest testAppDir

    let restoreFallback dir =
        dotnet dir "restore -v Information -f %s" binDir

    let runExample dir = 
        restoreFallback dir
        dotnet dir "build"
        dotnet dir "run"
    
    runExample consoleExampleDir
    runExample libraryExampleDir
)

"Clean"
==> "Build"
==> "Test" 

RunTargetOrDefault "Build"