namespace Microsoft.DotNet.Cli

module CommandLine =
    open System
    open System.Collections.Generic
    open System.Linq
    open System.Threading.Tasks
    open System.IO
    open System.Text


    type CommandArgument() =
        let values = ResizeArray<string>()

        member val Name = "" with get, set
        member val Description = "" with get, set
        member val MultipleValues = false with get, set
        member __.Value with get() = values.FirstOrDefault()
        member __.Values with get() = values


    type CommandOptionType =
        | MultipleValue
        | SingleValue
        | BoolValue
        | NoValue


    type CommandOption () =
        let values = ResizeArray<string>()

        member val LongName    = "" with get, set
        member val SymbolName  = "" with get, set
        member val ShortName   = "" with get, set
        member val ValueName   = "" with get, set
        member val Description = "" with get, set
        member val Template    = "" with get, set
        member val OptionType  = NoValue with get, set
        member val BoolValue   = Nullable false with get, set
        member __.Values with get() = values

        new (template:string, optionType:CommandOptionType) as self = CommandOption() then
            let isEnglishLetter (c:char) =
                (c >= 'a' && c <= 'z')||(c >= 'A' && c <= 'Z')
            do 
            self.Template   <- template
            self.OptionType <- optionType

            for part in template.Split ([|' ';'|'|], StringSplitOptions.RemoveEmptyEntries) do
                match part with
                | StartsWith "--" part -> 
                    self.LongName <- part.Substring 2
                | StartsWith "-"  part -> 
                    let optName = part.Substring 1
                    if optName.Length = 1 && not (isEnglishLetter (optName.[0])) 
                    then self.SymbolName <- optName
                    else self.ShortName <- optName
                | StartsWith "<" _ & EndsWith ">" part ->
                    self.ValueName <- part.Substring (1, part.Length-2)
                | StartsWith "<" _ & EndsWith ">..." part 
                  when optionType = MultipleValue ->
                    self.ValueName <- part.Substring (1, part.Length-5)
                | _ ->
                    raise ^ ArgumentException ^ sprintf "Invalid template pattern '%s'" template

                if  String.IsNullOrEmpty self.LongName 
                 && String.IsNullOrEmpty self.ShortName 
                 && String.IsNullOrEmpty self.SymbolName then
                    raise ^ ArgumentException ^ sprintf "Invalid template pattern '%s'" template

        member __.HasValue () = values.Any ()

        // TODO - can this be switched to an option?
        member self.Value () = if self.HasValue () then values.[0] else null

        member self.TryParse (value:string) =
            match self.OptionType with
            | MultipleValue -> 
                values.Add value; true
            | SingleValue -> 
                if  values.Any () then false else 
                values.Add value
                true
            | BoolValue ->
                if  values.Any () then false
                elif isNull value then
                    values.Add null
                    self.BoolValue <- Nullable true 
                    true
                else
                match Boolean.TryParse value with
                | _ , false ->  false
                | bl, true ->
                    values.Add value
                    self.BoolValue <- Nullable bl
                    true
            | NoValue ->
                if isNotNull value then false else
                values.Add "on"
                true


    type private  CommandArgumentEnumerator (enumerator:IEnumerator<CommandArgument>) as self =
        interface IEnumerator<CommandArgument> with
            member __.Current with get() : CommandArgument = enumerator.Current    
            member __.Dispose(): unit = enumerator.Dispose() 
    
        interface System.Collections.IEnumerator with 
            member __.Current with get(): obj = upcast (self:>IEnumerator<_>).Current
            member __.MoveNext(): bool = 
                if  isNull (self:>Collections.IEnumerator).Current 
                 || not (self:>IEnumerator<_>).Current.MultipleValues then
                    enumerator.MoveNext()
                // If current argument allows multiple values, we don't move forward and
                // all later values will be added to current CommandArgument.Values
                else true
            member __.Reset(): unit = enumerator.Reset()


    type internal CommandParsingException (command:CommandLineApplication, message:string) =
        inherit Exception (message)
        member val Command = command with get

    and CommandLineApplication (?throwOnUnexpectedArg) =

        // Indicates whether the parser should throw an exception when it runs into an unexpected argument.
        // If this field is set to false, the parser will stop parsing when it sees an unexpected argument, and all
        // remaining arguments, including the first unexpected argument, will be stored in RemainingArguments property.
        let throwOnUnexpectedArg = defaultArg throwOnUnexpectedArg true
        let commands = ResizeArray<CommandLineApplication>()
        let options = ResizeArray<CommandOption>()
        let arguments = ResizeArray<CommandArgument>()
        let remainingArguments = ResizeArray<string>()
        let mutable optionHelp = None : CommandOption option
        let mutable optionVersion = None : CommandOption option
        let mutable isShowingInformation = false

        member val Parent = None with get, set
        member val Name = "" with get, set
        member val FullName = "" with get, set
        member val Syntax = "" with get, set
        member val Description = "" with get, set
        member val HandleResponseFiles = false with get, set
        member val AllowArgumentSeperator = false with get, set    
        member val Invoke = fun () -> 0 with get, set
        member val LongVersionGetter = fun()->"" with get, set
        member val ShortVersionGetter = fun()->"" with get, set

        member __.Options with get() = options
        member __.Arguments with get() = arguments
        member __.Commands with get() = commands
        member __.RemainingArguments with get() = remainingArguments
        member __.OptionHelp with get() = optionHelp
        member __.OptionVersion with get() = optionVersion

        member __.IsShowingInformation 
            with get() = isShowingInformation
            and internal set v = isShowingInformation <- v

        member __.ThrowOnUnexpectedArg
            with internal get() = throwOnUnexpectedArg


        member self.Command (name:string, configuration: CommandLineApplication->unit,?throwOnUnexpectedArg) =
            let throwOnUnexpectedArg = defaultArg throwOnUnexpectedArg true
            let command = CommandLineApplication(throwOnUnexpectedArg,Name=name,Parent=Some self) 
            self.Commands.Add command
            configuration command
            command


        member self.Option (template:string, description:string, optionType:CommandOptionType,
                            configuration : CommandOption -> unit) : CommandOption =
            let option = CommandOption (template, optionType)
            self.Options.Add option; configuration option
            option            


        member self.Option (template, description, optionType) =
            self.Option(template,description,optionType, fun _ ->())


        member self.Argument (name:string, description:string, configuration:CommandArgument->unit, ?multipleValues:bool) =
            let multipleValues = defaultArg multipleValues false
            let lastArg = self.Arguments.LastOrDefault()
            if lastArg.MultipleValues then
                raise ^ InvalidOperationException ^
                    sprintf "The last argument '%s' accepts multiple values. No more arguments can be added" lastArg.Name
            let argument = CommandArgument (Name=name, Description=description,MultipleValues=multipleValues)
            self.Arguments.Add argument; configuration argument
            argument 


        member self.Argument (name:string, description:string, ?multipleValues:bool) =        
            let multipleValues = defaultArg multipleValues false        
            self.Argument(name,description,(fun _ ->()),multipleValues)

        member self.OnExecute (invoke:unit->int) = self.Invoke <- invoke

        member self.OnExecute (invoke:unit->int Task) =
            self.Invoke <- (fun () -> invoke().Result)

        member self.HelpOption (template:string) =
            optionHelp <- Some ^ self.Option(template, "Show help information", CommandOptionType.NoValue)
            self.OptionHelp


        // Helper method that adds a version option
        member self.VersionOption(template:string,shortFormVersionGetter:unit->string,?longFormVersionGetter:unit->string) =
            // Version option is special because we stop parsing once we see it
            // So we store it separately for further use
            optionVersion <- Some ^ self.Option(template, "Show version information", CommandOptionType.NoValue)
            self.ShortVersionGetter <- shortFormVersionGetter
            self.LongVersionGetter <- if longFormVersionGetter.IsNone then shortFormVersionGetter else longFormVersionGetter.Value
            self.OptionVersion


        member self.VersionOption (template:string, shortFormVersion:string,?longFormVersion:string) =
            let longFormVersion = defaultArg longFormVersion null
            if isNull longFormVersion then
                self.VersionOption(template, fun () -> shortFormVersion)
            else
                self.VersionOption(template, (fun()->shortFormVersion),(fun ()->longFormVersion))


        // Show short hint that reminds users to use help option
        member self.ShowHint() = 
            if self.OptionHelp.IsSome then
                Console.WriteLine (sprintf "Specify --%s for a list of available options and commans." optionHelp.Value.LongName)


        member self.ShowVersion () =
            let rec loop (cmd:CommandLineApplication) =
                cmd.IsShowingInformation <- true
                if cmd.Parent.IsNone then () else loop cmd.Parent.Value
            loop self
            Console.WriteLine self.FullName
            Console.WriteLine (self.LongVersionGetter())


        member self.GetFullNameAndVersion () =
            match self.ShortVersionGetter() with
            | "" -> self.FullName
            | short -> sprintf "%s %s" self.FullName short


        member self.ShowRootCommandFullNameAndVersion () =
            let rec loop (cmd:CommandLineApplication) =
                if cmd.Parent.IsNone then cmd else loop cmd.Parent.Value
            let rootCmd = loop self
            Console.WriteLine(rootCmd.GetFullNameAndVersion())         
            Console.WriteLine()

    let inline getName x        = (^a:(member Name:string) x)
    let inline getTemplate x    = (^a:(member Template:string) x)
    let inline getDescription x = (^a:(member Description:string) x)

    let getMaxHelper (elems:IEnumerable<'a>) (getString:'a->string) =
        (0, elems) ||> Seq.fold (fun maxLen item -> 
            let str = getString item in if str.Length > maxLen then str.Length else maxLen)


    type CommandLineApplication with

        member self.MaxOptionTemplateLength (options: IEnumerable<CommandOption>) = getMaxHelper options getTemplate

        member self.MaxCommandLength (commands: IEnumerable<CommandLineApplication>) = getMaxHelper commands getName

        member self.MaxArgumentLength (arguments: IEnumerable<CommandArgument>) = getMaxHelper arguments getName

        member self.HandleUnexpectedArg (command:CommandLineApplication, args:string[], index:int, argTypeName:string) =
            if command.ThrowOnUnexpectedArg then
                command.ShowHint()
                raise ^ CommandParsingException(command, sprintf "Unrecognized %s '%s'" argTypeName args.[index])
            else
            command.RemainingArguments.AddRange(ArraySegment<string>(args, index, args.Length-index))


        member private self.ParseResponseFile (fileName:string) =
            if not self.HandleResponseFiles then Seq.empty 
            elif not ^ File.Exists fileName then
                raise ^ InvalidOperationException ^ sprintf "Response file '%s' doesn't exist" fileName
            else File.ReadAllLines fileName :> _ seq


        member private self.ExpandResponseFiles (args:IEnumerable<string>) = seq {
            for arg in args do
                if not ^ arg.StartsWith("@",StringComparison.Ordinal) then 
                    yield arg
                else 
                let fileName = arg.Substring 1
                let responseFileArguments = self.ParseResponseFile fileName
                if Seq.isEmpty responseFileArguments then 
                    yield arg
                else
                for responseFileArg in responseFileArguments do
                    yield responseFileArg.Trim()
        }
        

        // Show Full Help
        member self.ShowHelp (?commandName) =
            let commandName = defaultArg commandName String.Empty
            let headerBuilder = StringBuilder  "Usage:"

            let rec loop (cmd:CommandLineApplication) =
                cmd.IsShowingInformation <- true
                headerBuilder.Insert (6, sprintf " %s" cmd.Name)|> ignore
                if cmd.Parent.IsNone then () else loop cmd.Parent.Value
            loop self
        
            let target =
                if  isNull commandName 
                 || String.Equals(self.Name,commandName, StringComparison.OrdinalIgnoreCase) then
                    self
                else
                match self.Commands |> Seq.tryFind (fun cmd -> 
                    String.Equals(cmd.Name, commandName,StringComparison.OrdinalIgnoreCase)) with
                | Some target -> 
                    headerBuilder.Append (sprintf " %s" commandName) |> ignore
                    target
                | None -> self

            let optionsBuilder   = StringBuilder()
            let commandsBuilder  = StringBuilder()
            let argumentsBuilder = StringBuilder()

            let inline formattedBuilder(elems:ResizeArray<_>, bracketed:string, elemType:string,  
                                        elemBuilder:StringBuilder, getMaxFn: ResizeArray<'a> -> int, getter:'a ->string) =
                if elems.Any () then
                    headerBuilder.Append bracketed |> ignore
                    elemBuilder.AppendLine().AppendLine elemType |> ignore
                    let maxItemLen = getMaxFn elems in let format = String.Format("  {{0, -{0}}}{{1}}", maxItemLen + 2)
                    for item in elems do 
                        elemBuilder.AppendFormat(format, getter item, getDescription item).AppendLine() |> ignore

            formattedBuilder (target.Arguments, " [arguments]", "Arguments:", argumentsBuilder, self.MaxArgumentLength, getName)
            formattedBuilder (target.Options  , " [options]"  , "Options:"  , optionsBuilder  , self.MaxOptionTemplateLength, getTemplate)
            formattedBuilder (target.Commands , " [command]"  , "Commands:" , commandsBuilder , self.MaxCommandLength, getName)
        
            if self.OptionHelp.IsSome && target.Commands.Any() then
                commandsBuilder.AppendLine()
                    .AppendFormat("Use \"{0} [command] --help\" for more information about a command.", self.Name)
                    .AppendLine () |> ignore

            if target.AllowArgumentSeperator then headerBuilder.Append " [[--] <arg>...]]" |> ignore

            headerBuilder.AppendLine()|>ignore
            let nameAndVersion = StringBuilder().AppendLine(self.GetFullNameAndVersion()).AppendLine()

            Console.Write ("{0}{1}{2}{3}{4}", nameAndVersion, headerBuilder, argumentsBuilder, optionsBuilder, commandsBuilder)


        member self.Execute ([<ParamArray>] args: string[]) : int =
            let args = 
                if not self.HandleResponseFiles then args else
                self.ExpandResponseFiles(args).ToArray()
            
            let mutable command = self
            let mutable option = None : CommandOption option
            let mutable arguments = null : IEnumerator<CommandArgument>

            let mutable returned = false
            let rec loop idx =                 
                if idx = args.Length then () else                
                let arg = args.[idx]
                let mutable index = idx
                let mutable processed = false

                if not processed && option.IsNone then
                    let mutable longOption = [||] : string []
                    let mutable shortOption = [||] : string []

                    if arg.StartsWith "--" then longOption  <- arg.Substring(2).Split ([|':';'='|], 2)
                    if arg.StartsWith "-"  then shortOption <- arg.Substring(1).Split ([|':';'='|], 2)

                    if longOption <> [||] then
                        processed <- true
                        let longOptionName = longOption.[0]

                        match command.Options |> Seq.tryFind (fun opt -> 
                            String.Equals(opt.LongName, longOptionName, StringComparison.Ordinal)) with
                        | None -> option <- None 
                        | Some opt -> option <- Some opt

                        if option.IsNone then
                            if  String.IsNullOrEmpty longOptionName 
                             && not(command.ThrowOnUnexpectedArg && self.AllowArgumentSeperator) then
                                index <- index+1   
                            self.HandleUnexpectedArg(command,args,index,argTypeName="option")                           
                            loop args.Length

                        elif Option.valuesEqual command.OptionHelp option then
                            command.ShowHelp()
                            returned <- true
                            loop args.Length // end recursion, return 0 at end

                        elif Option.valuesEqual command.OptionVersion option then
                            command.ShowVersion()
                            returned <- true
                            loop args.Length  // end recursion, return 0 at end
                    else
                        ()                     
                    if longOption.Length = 2 then
                        if not (option.Value.TryParse(longOption.[1])) then 
                            command.ShowHint()
                            raise ^ CommandParsingException(command, sprintf "Unexpected value '%s' for option '%s'" longOption.[1] option.Value.LongName)
                        option <- None

                    elif option.IsSome && (option.Value.OptionType = NoValue || option.Value.OptionType = BoolValue) then
                                // No value is needed for this option
                                // option.Value.TryParse null <- this was in the C#, but I don't see the point
                        option <- None
                    else
                        ()

                    if shortOption <> [||] then
                        processed <- true
                        match command.Options |> Seq.tryFind (fun opt -> 
                            String.Equals(opt.ShortName, shortOption.[0], StringComparison.Ordinal)) with
                        | None -> option <- None
                        | Some opt -> option <- Some opt
                        
                        if option.IsNone then
                            self.HandleUnexpectedArg(command, args, index,argTypeName="option")                            
                            loop args.Length

                        elif Option.valuesEqual command.OptionHelp option then
                            command.ShowHelp()
                            returned <- true
                            loop args.Length // end recursion, return 0 at end

                        elif Option.valuesEqual command.OptionVersion option then
                            command.ShowVersion()
                            returned <- true
                            loop args.Length // end recursion, return 0 at end
                        else
                            ()
                        
                    if shortOption.Length = 2 then 
                        if not (option.Value.TryParse(shortOption.[1])) then
                            command.ShowHint()
                            raise ^ CommandParsingException(command, sprintf "Unexpected value '%s' for option '%s'" shortOption.[1] option.Value.LongName)
                        option <- None

                    elif  option.IsSome && (option.Value.OptionType = NoValue || option.Value.OptionType = BoolValue) then
                        // No value is needed for this option
                        // option.Value.TryParse null <- this was in the C#, but I don't see the point
                        option <- None
                    else
                        ()
                
                if not processed && option.IsSome then
                    processed <- true
                    if not (option.Value.TryParse arg) then
                        command.ShowHint()
                        raise ^ CommandParsingException(command, sprintf "Unexpected value '%s' for option '%s'" arg option.Value.LongName)
                    option <- None

                if not processed && isNull arguments  then
                    let mutable currentCommand = command
                    let rec innerLoop cnt =
                        if cnt = command.Commands.Count then () else
                        let subcommand = command.Commands.[cnt]
                        if String.Equals(subcommand.Name, arg, StringComparison.OrdinalIgnoreCase) then
                            processed <- true
                            command <- subcommand
                            innerLoop command.Commands.Count
                        else innerLoop (cnt+1)
                    innerLoop 0

                    if command <> currentCommand then
                        processed <- true

                if not processed then
                    if isNull arguments then
                        arguments <- (new CommandArgumentEnumerator(command.Arguments.GetEnumerator()):> IEnumerator<CommandArgument>)
                    if arguments.MoveNext() then
                        processed <- true
                        arguments.Current.Values.Add arg
                if not processed then
                    self.HandleUnexpectedArg(command, args, index, argTypeName="command or argument")
                    loop args.Length
                else
                loop (index+1)
            loop 0
            if returned = true then 0 
            elif option.IsSome then
                command.ShowHint()
                raise ^ CommandParsingException(command, sprintf "Missing value for option '%s'" option.Value.LongName)
            else
                command.Invoke()


                



                    







    
