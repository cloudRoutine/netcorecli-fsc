namespace Microsoft.DotNet.Cli

[<AutoOpen>]
module Prelude =

    let (^) = (<|)
    
    /// Null coalescing operator
    let (<?>) a b = if isNull a then b else a

    let inline isNotNull v = not (isNull v)

    let (|StartsWith|_|) (pattern:string) (str:string) =
        if str.StartsWith pattern then Some str else None

    let (|EndsWith|_|)  (pattern:string) (str:string) =
        if str.EndsWith pattern then Some str else None


[<RequireQualifiedAccess>]
module Option =

    let valuesEqual (opt1:'a option) (opt2:'a option) =
        match opt1, opt2 with
        | None, _ | _, None -> false
        | Some a, Some b -> a = b

