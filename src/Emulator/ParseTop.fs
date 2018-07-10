(* 
    VisUAL2 @ Imperial College London
    Project: A user-friendly ARM emulator in F# and Web Technologies ( Github Electron & Fable Compiler )
    Module: Emulator.ParseTop
    Description: Top-level code to parse instructions
*)

/// Top-level code implementing assembler parsing
module ParseTop
    open CommonLex
    open CommonData

    open Errors
    open Expressions

    /// allows different modules to return different instruction types
    type Instr =
        | IMEM of Memory.Instr
        | IDP of DP.Instr
        | IMISC of Misc.Instr
        | IBRANCH of Branch.Instr
        | EMPTY
    

            
    let Blank lab = {
        PCond = Cal
        PInstr = Ok EMPTY
        PLabel = lab
        ISize = 0u
        DSize = 0u
        POpCode = ""
    }

    /// Split line on whitespace into an list
    let splitIntoWords ( line:string ) =
        line.Split( ([|' ';'\t';'\f';'\r';'\n';'\b'|] : char array), 
          System.StringSplitOptions.RemoveEmptyEntries)
        |> Array.collect (function |"" -> [||] | s -> [|s|])
        |> Array.map (fun s -> s.Trim())
        |> Array.toList

    /// Note that Instr in Mem and DP modules is NOT same as Instr in this module
    /// Instr here is all possible isntruction values combines with a D.U.
    /// that tags the Instruction class
    /// Similarly ErrParse
    /// Similarly IMatch here is combination of module IMatches
    let IMatch (ld: LineData) : Parse<Instr> option  =
        let copy cons pa = 
            {
                PCond = pa.PCond
                PInstr = Result.map cons pa.PInstr
                PLabel = pa.PLabel
                POpCode = pa.POpCode
                ISize = pa.ISize
                DSize = pa.DSize
            } |> Some
 
            
        match ld with
        | Memory.IMatch pa -> printfn "mem"; copy IMEM pa 
        | DP.IMatch pa -> printfn "dp"; copy IDP pa
        | Misc.IMatch pa -> printfn "misc"; copy IMISC pa
        | Branch.IMatch pa ->printfn "branch %s" ld.OpCode; copy IBRANCH pa
        | _ -> printfn "Unrecognised %s" ld.OpCode; None
    
    

    type CondInstr = Condition * Instr

    let makeParse labOpt la ins =
        {
            PInstr = ins
            PLabel= labOpt |> Option.map (fun lab -> lab, la)
            ISize = 0u
            DSize = 0u
            PCond = Cal
            POpCode=""
        } 




    let parseLine (symtab: SymbolTable) (loadI:uint32, loadD:uint32) (asmLine:string) =
        let isDataOp op = List.contains op ["DCD";"DCB";"FILL"]
        let isLabel (str:string) =
            str.Length > 0 && System.Char.IsLetter str.[0] && Seq.forall System.Char.IsLetterOrDigit str
        let loadA opcode = if isDataOp opcode then loadD else loadI
        /// put parameters into a LineData record and parse
        let (|TRYPARSE|_|) (words:string list) =
            match words  with
            | label :: opcode :: operands ->
                {
                    OpCode=opcode.ToUpper()
                    Operands= (String.concat " " operands).Trim()
                    Label= match label with "" -> None | _ -> Some label
                    LoadAddr = (if isDataOp opcode then loadD else loadI) |> WA
                    SymTab = symtab
                } |> IMatch
            | _ -> None

        /// remove comments from string
        let removeComment (txt:string) =
            txt.Split(';')
            |> function 
                | [|x|] -> x 
                | [||] -> "" 
                | lineWithComment -> lineWithComment.[0]
        /// try to parse 1st word, or 2nd word, as opcode
        /// If 2nd word is opcode 1st word must be label
        let parseLine' words =
            let defParse lab = makeParse lab (Ok loadI)
            match [""] @ words @ [""] with
                | "" :: TRYPARSE  pa -> pa
                | TRYPARSE pa -> pa
                | ["";label;""] when isLabel label ->  defParse (Some label) (EMPTY |> Ok)
                | ["";""] -> defParse None (EMPTY |> Ok)
                | "" :: opc :: _ ->
                    defParse None (``Unimplemented instruction`` opc |> Error)
                | _ -> failwithf "What: should not be possible!"
        asmLine
        |> removeComment
        |> splitIntoWords
        |> parseLine'



