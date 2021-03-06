// #!/usr/bin/env fsharpi

open System


module Term =
    open System
    
    let blue     = ConsoleColor.Blue
    let yellow   = ConsoleColor.Yellow
    let red      = ConsoleColor.Red
    let darkBlue = ConsoleColor.DarkBlue

    let withColor color action =
        System.Console.ForegroundColor <- color
        action()
        System.Console.ResetColor()

    let printWithColor color (msg: string) =
        System.Console.ForegroundColor <- color
        System.Console.WriteLine(msg)
        System.Console.ResetColor()


module SysUtils =
    open System
               
    let runShellCmd (program: String) (args: string list) =
        let argString = String.Join(" ", args)
        let p = System.Diagnostics.Process.Start(program, argString)
        printfn "Running %s %s" program argString
        p.WaitForExit()
        p.ExitCode

    let joinPath path1 path2 =
        System.IO.Path.Combine(path1, path2)

    let baseName file =
        System.IO.Path.GetFileName file 

    let replaceExt ext file =
        System.IO.Path.ChangeExtension(file, ext)

    let getFilesExt path pattern =
        System.IO.Directory.GetFiles(path, pattern)
        |> Seq.ofArray

    let getCommandLineArgs () =
        let args = Environment.GetCommandLineArgs()
        let idx = Array.tryFindIndex (fun a -> a = "--") args
        match idx with
        | None   -> []
        | Some i -> args.[(i+1)..] |> List.ofArray

    let directoryExists path =
        System.IO.Directory.Exists path 

    let mkdir path =
        if not <| directoryExists path
        then ignore <| System.IO.Directory.CreateDirectory path

    /// Compute File MD5 Sum 
    let fileMD5 file =
        use md5    = System.Security.Cryptography.MD5.Create()
        use stream = System.IO.File.OpenRead file
        System.BitConverter
              .ToString(md5.ComputeHash(stream))
              .Replace("-", "")
              .ToLower()


type FsharpCompiler =
    static member CompileLibrary(sources:      string list 
                                ,dependencies: string list 
                                ,output
                                ,doc
                                 )  =

        let srcList = String.Join(" ", sources)
        
        printfn "Sources = %s" srcList
        
        let args = [ srcList
                   ; "--target:library"
                   ; "--out:" + output
                   ; "--doc:" + doc
                   ; "--debug+"
                   ; "--nologo"
                   ; String.Join(" ", dependencies |> List.map (fun s -> "-r:" + s))
                   ]

        // printfn "%A" args 
        SysUtils.runShellCmd "fsharpc" args 


    static member CompileExecutable(source: string, ?output) =
        let output = defaultArg output (SysUtils.replaceExt "exe" source)
        let args = [ source
                   ; "--target:exe"
                   ; "--out:" + output
                   ; "--debug+"
                   ; "--nologo"                   
                   ]
        SysUtils.runShellCmd "fsharpc" args


    static member CompileExecutableWEXE( sources:      string list 
                                        ,dependencies: string list 
                                        ,output
                                        ,?staticLink
                                        ,?resources
                                        ,?others:       string list 
                                        )  =

        let stLink = match staticLink with
                     | Some xs  -> String.Join(" ", xs |> List.map(fun s -> "--staticlink:" + s))
                     | None     -> ""

        let resourcesList =
            match resources with
            | Some xs  -> String.Join(" ", xs |> List.map(fun s -> "--linkresource:" + s))
            | None     -> ""

        let others = match others with
                     | Some xs -> String.Join(" ", xs)
                     | _       -> ""
        
        let args = [ String.Join(" ", sources)
                   ; "--target:winexe"
                   ; "--out:" + output
                   ; "--debug+"
                   ; "--nologo"
                   ; String.Join(" ", dependencies |> List.map (fun s -> "-r:" + s))
                   ; stLink
                   ; resourcesList
                   ; others
                   ]

        // printfn "%A" args 
        SysUtils.runShellCmd "fsharpc" args 



// ------------------- U S E R   O P T I O N S ---------------- //

let gtkHome = "/usr/lib/mono/gtk-sharp-3.0/"

let gtkDependencies =
    let gtkDlls = [
        "atk-sharp.dll"
        ;"gio-sharp.dll"
        ;"glib-sharp.dll"
        ;"gtk-sharp.dll"
        ;"gdk-sharp.dll"
        ;"cairo-sharp.dll"
        ;"pango-sharp.dll"
        ]
    List.map (fun p -> System.IO.Path.Combine(gtkHome, p)) gtkDlls




let buildLib () =
    let status = FsharpCompiler.CompileLibrary(["src/fxgtk.fsx"; "src/wforms.fsx"]
                                               ,gtkDependencies
                                               ,"bin/fxgtk.dll"
                                               ,"bin/fxgtk.xml"
                                               )

    match status with
    | 0 -> printfn "Build successful. Ok"
    | _ -> printfn "Build failed"

let buildExample example =
    let outputFile = example |> SysUtils.joinPath "bin/"
                             |> SysUtils.replaceExt "exe"

    Term.withColor Term.blue (fun () -> printfn "Building Example: %s\n" example)

    let status = FsharpCompiler.CompileExecutableWEXE([SysUtils.joinPath "examples/" example]
                                                      ,["bin/fxgtk.dll"] @ gtkDependencies
                                                      ,outputFile
                                                      ,staticLink = ["fxgtk"]
                                                      )

    match status with
    | 0 -> Term.printWithColor Term.blue (sprintf "\nBuild %s successful. Ok"  outputFile)
    | _ -> Term.printWithColor Term.red  (sprintf "\nBuild %s Failed." outputFile)
    printfn "-------------------------------------\n\n"


let getExamples () =
    SysUtils.getFilesExt "examples" "*.fsx"
    |> Seq.map SysUtils.baseName


let buildDemo1() =
    let outputFile = "bin/demo1.exe"
    let status = FsharpCompiler.CompileExecutableWEXE(
        [SysUtils.joinPath "examples/" "example1-image-viewer.fsx"]
        ,["bin/fxgtk.dll"] @ gtkDependencies
        ,outputFile
        ,staticLink = ["fxgtk"]
        //,others = ["--win32res:AppIcon.rc"]
        ,resources  = ["icontest.ico,Main.icon.ico"] 
        )
    match status with
    | 0 -> Term.printWithColor Term.blue (sprintf "\nBuild %s successful. Ok"  outputFile)
    | _ -> Term.printWithColor Term.red  (sprintf "\nBuild %s Failed." outputFile)
    printfn "-------------------------------------\n\n"
    

let runArgs args =
    match args with
        
   // Build library fxgtk.dll 
    | ["--lib"] -> buildLib()
        
    // Show all examples  
    | ["--example"]
      -> getExamples () |> Seq.iter (printfn "%s")
         

    // Build all examples   
    | ["--example" ; "--all"]
      -> getExamples() |> Seq.iter buildExample

    | ["--all"]
      ->  buildLib()
          getExamples() |> Seq.iter buildExample
         
    
    // Build a given example 
    | ["--example"; fileName] -> buildExample fileName

    | ["--build"; file]       -> ignore <| FsharpCompiler.CompileExecutable file

    | ["--demo1"]             -> buildDemo1()
    
    | cmd                     -> printfn "Error: Invalid command: %A" cmd



#if INTERACTIVE

let () =
    runArgs <| SysUtils.getCommandLineArgs() 
#else

[<EntryPoint>]
let main(args) =
    runArgs <| List.ofArray args
    1

#endif
