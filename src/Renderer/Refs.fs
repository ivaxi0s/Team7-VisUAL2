(* 
    VisUAL2 @ Imperial College London
    Project: A user-friendly ARM emulator in F# and Web Technologies ( Github Electron & Fable Compiler )
    Module: Renderer.Refs
    Description: F# references to elements in the DOM + some user settings handling
*)

/// F# References to static parts of renderer DOM
module Refs
open CommonData


open Fable.Core
open Fable.Core.JsInterop
open Fable.Import
open Fable.Import.Browser
open Microsoft.FSharp.Collections
open Node.Exports

// **********************************************************************************
//                                  App Version 
// **********************************************************************************

let appVersion = "0.12.2"

// **********************************************************************************
//                               Types used in this module
// **********************************************************************************

/// Bases to display data in for all Views
/// Udec = unsigned ecimal
type Representations =
    | Hex
    | Bin
    | Dec
    | UDec

/// Select View in RH window
type Views =
    | Registers
    | Memory
    | Symbols

type VSettings = {
    EditorFontSize : string
    SimulatorMaxSteps : string
    EditorTheme: string
    EditorWordWrap: string
    EditorRenderWhitespace: string
    CurrentFilePath: string
    RegisteredKey: string
    }

// ***********************************************************************************
//                       Functions Relating to Right-hand View Panel
// ***********************************************************************************


/// used to get ID of button for each representation
let repToId = 
    Map.ofList [
        Hex, "rep-hex";
        Bin, "rep-bin";
        Dec, "rep-dec";
        UDec, "rep-udec";
    ]

/// used to get ID used in DOM for each View
let viewToIdView = 
    Map.ofList [
        Registers, "view-reg";
        Memory, "view-mem";
        Symbols, "view-sym";
    ]
/// used to get Tab ID in DOM for each View
let viewToIdTab = 
    Map.ofList [
        Registers, "tab-reg";
        Memory, "tab-mem";
        Symbols, "tab-sym"
    ]

// ************************************************************************************
//                         Utility functions used in this module
// ************************************************************************************

[<Emit("__dirname")>]

let appDirName:string  = jsNative


/// compare input with last input: if different, or no last input, execute function
let cacheLastWithActionIfChanged actionFunc =
    let mutable cache: 'a option = None
    fun inDat ->
        match cache with
        | Some i when i = inDat -> ()
        | Some _ 
        | None -> 
            cache <- Some inDat
            actionFunc inDat

    
    
    


/// Determine whether JS value is undefined
[<Emit("$0 === undefined")>]
let isUndefined (_: 'a) : bool = jsNative

/// A reference to the settings for the app
/// persistent using electron-settings
let settings:obj = electron.remote.require "electron-settings"
/// look up a DOM element
let getHtml = Browser.document.getElementById


let mutable vSettings = {
    EditorFontSize = "16"
    SimulatorMaxSteps = "20000"
    EditorTheme = "solarised-dark"
    EditorWordWrap = "off"
    EditorRenderWhitespace = "none"
    CurrentFilePath = Fable.Import.Node.Exports.os.homedir()
    RegisteredKey = ""
    }

let themes =  [
                "one-dark-pro", "One Dark Pro";
                "one-light-pro","One Light Pro";
                "solarised-dark", "Solarised Dark";
                "solarised-light", "Solarised Light";
              ]
let minFontSize = 6L
let maxFontSize = 60L


let checkSettings (vs: VSettings) = 
    let vso = vSettings
    let checkPath (p:string) = 
        match (fs.statSync (U2.Case1 p)).isDirectory() with
        | true -> p
        | false -> os.homedir()
    try
        let checkNum (n:string) (min:int64) (max:int64) (def:string) = 
            match int64 n with
            | x when x > max -> def
            | x when x < min -> def
            | x -> x.ToString()
        {
        vs with 
            EditorTheme = 
                match List.tryFind ( fun (th , _) -> (th = vs.EditorTheme)) themes with
                | Some _ ->  vs.EditorTheme
                | _ ->  printfn "Setting theme to default"
                        vSettings.EditorTheme
            SimulatorMaxSteps = checkNum vs.SimulatorMaxSteps 0L System.Int64.MaxValue vso.SimulatorMaxSteps
            EditorFontSize = checkNum vs.EditorFontSize minFontSize maxFontSize vso.EditorFontSize
            CurrentFilePath = checkPath vs.CurrentFilePath
        }
    with
        | _ ->  printf "Error parsing stored settings: %A" vs
                vs








let setJSONSettings() =
    let setSetting (name : string) (value : string) =
        printf "Saving JSON: %A" value
        settings?set(name, value) |> ignore
    setSetting "JSON" (Fable.Import.JS.JSON.stringify vSettings)


let getJSONSettings() = 
    let json = settings?get("JSON", "undefined") :?> string
    match json = "undefined" with
    | true ->
            printfn "No JSON settings found on this PC"
            setJSONSettings()
            vSettings
    | false -> 
        try
            (Fable.Import.JS.JSON.parse json) :?> VSettings
        with
        | e -> 
            printfn "default settings"
            vSettings

    
  


let showMessage (callBack:int ->unit) (message:string) (detail:string) (buttons:string list) =
    let rem = electron.remote
    let retFn = unbox callBack
    rem.dialog.showMessageBox(
       (let opts = createEmpty<Fable.Import.Electron.ShowMessageBoxOptions>
        opts.title <- FSharp.Core.Option.None
        opts.message <- message |> Some
        opts.detail <- detail |> Some
        opts.``type`` <- "none" |> Some
        opts.buttons <- buttons |> List.toSeq |> ResizeArray |> Some
        opts), retFn)   
    |> ignore

/// extract CSS custom variable value
let getCustomCSS (varName:string) =
    let styles = window.getComputedStyle Browser.document.documentElement
    styles.getPropertyValue varName
/// set a custom CSS variable defined in :root pseudoclass
let setCustomCSS (varName:string) (content:string) =
    let element = Browser.document.documentElement
    element.style.setProperty(varName, content)

/// set the CSS variable that determines dashboard width
let setDashboardWidth (width)=
    setCustomCSS "--dashboard-width" width

/// Element in Register view representing register rNum
let register rNum = getHtml <| sprintf "R%i" rNum

let visualDocsPage name = 
    match EEExtensions.String.split [|'#'|] name |> Array.toList with
    | [""] -> @"https://tomcl.github.io/visual2.github.io/"
    | [ page ] ->sprintf  "https://tomcl.github.io/visual2.github.io/%s.html#content" page
    | [ page; tag ] -> sprintf @"https://tomcl.github.io/visual2.github.io/%s.html#%s" page tag
    | _ -> failwithf "What? Split must return non-empty list!"

/// Run an external URL url in a separate window.
/// Second parameter triggers action (for use in menus)
let runPage url () =
    printf "Running page %s" url
    let rem = electron.remote
    let options = createEmpty<Electron.BrowserWindowOptions>
    // Complete list of window options
    // https://electronjs.org/docs/api/browser-window#new-browserwindowoptions
    options.width <- Some 1200.
    options.height <- Some 800.
    //options.show <- Some false
    let prefs = createEmpty<Electron.WebPreferences>
    prefs.devTools <- Some false 
    prefs.nodeIntegration <- Some false
    options.webPreferences <- Some prefs
    
    options.frame <- Some true
    options.hasShadow <- Some true
    options.backgroundColor <- None
    options.icon <- Some (U2.Case2  "app/visual.ico")
    let window = rem.BrowserWindow.Create(options)
    window.setMenuBarVisibility false
    window.loadURL url
    window.show()


let writeToFile str path =
    let errorHandler _err = // TODO: figure out how to handle errors which can occur
        ()
    fs.writeFile(path, str, errorHandler)


// *************************************************************************************
//                               References to DOM elements
// *************************************************************************************

//--------------------------- Buttons -------------------------------

let openFileBtn = getHtml "explore" :?> HTMLButtonElement
let saveFileBtn = getHtml "save" :?> HTMLButtonElement
let runSimulationBtn: HTMLButtonElement = getHtml "run" :?> HTMLButtonElement
let resetSimulationBtn = getHtml "reset" :?> HTMLButtonElement
let stepForwardBtn = getHtml "stepf" :?> HTMLButtonElement
let stepBackBtn = getHtml "stepb" :?> HTMLButtonElement
/// get byte/word switch button element
let byteViewBtn = getHtml "byte-view"

//----------------------- View pane elements -------------------------

/// Get Flag display element from ID ("C", "V", "N", "Z")
let flag id = getHtml <| sprintf "flag_%s" id

/// get button for specific representation
let representation rep = getHtml repToId.[rep]

/// get View pane element from View
let viewView view = getHtml viewToIdView.[view]

/// get View Tab element from view
let viewTab view = getHtml viewToIdTab.[view]


/// get memory list element
let memList = getHtml "mem-list"

/// get symbol table View element
let symView = getHtml "sym-view"

/// get symbol table element
let symTable = getHtml "sym-table"

//---------------------File tab elements-------------------------------

/// get element containing all tab headers
let fileTabMenu = getHtml "tabs-files"

/// get last (invisible) tab header
let newFileTab = getHtml "new-file-tab"

/// get ID of Tab for Tab number tabID
let fileTabIdFormatter tabID = sprintf "file-tab-%d" tabID

/// get element corresponding to file tab tabID
let fileTab tabID = getHtml <| fileTabIdFormatter tabID

/// get ID of editor window containing file
let fileViewIdFormatter = sprintf "file-view-%d"

/// get element of editor window containing file
let fileView id = getHtml <| fileViewIdFormatter id

/// get pane element containing for tab menu and editors
let fileViewPane = getHtml "file-view-pane"

/// get id of element containing tab name as dispalyed
let tabNameIdFormatter = sprintf "file-view-name-%d"

/// get element containing tab name as displayed
let fileTabName id = getHtml <| tabNameIdFormatter id

/// get id of element containing file path
let tabFilePathIdFormatter = sprintf "file-view-path-%d"
/// get (invisible)  element containing file path
let tabFilePath id = getHtml <| tabFilePathIdFormatter id
/// get the editor window overlay element

//--------------- Simulation Indicator elements----------------------

let darkenOverlay = getHtml "darken-overlay"
/// get element for status-bar button (and indicator)
let statusBar = getHtml "status-bar"

/// Set the background of file panes.
/// This is done based on theme (light or dark) to prevent flicker
let setFilePaneBackground color =
    fileViewPane.setAttribute("style", sprintf "background: %s" color)


// ***********************************************************************************************
//                                       Mutable state
// ***********************************************************************************************

/// Sensible initial value of R13 so that code with subroutines works as expected
let initStackPointer = 0xff000000u

/// initial value of all registers (note special case for SP R13)
let initialRegMap : Map<CommonData.RName, uint32> = 
    [0..15]
    |> List.map ( CommonData.register >> function | R13 -> R13,initStackPointer | rn -> rn,0u)
    |> Map.ofList

let initialFlags =  { N=false ; Z=false; C=false; V=false}  
/// File Tab currently selected (and therefore visible) 
let mutable currentFileTabId = -1 // By default no tab is open
/// List of all in use file tabs
let mutable fileTabList : int list = []
/// Map tabIds to the editors which are contained in them
let mutable editors : Map<int, obj> = Map.ofList []
/// id of tab containing settings form, if this exists
let mutable settingsTab : int option = Microsoft.FSharp.Core.option.None
/// The current number representation being used
let mutable currentRep = Hex
/// indicates what the current DOM symbols display representation is
let mutable displayedCurrentRep = Hex
/// The current View in the right-hand pane
let mutable currentView = Registers
/// Whether the Memory View is byte of word based
let mutable byteView = false
/// Number of instructions imulated before break. If 0 run forever
let mutable maxStepsToRun = 50000
/// Contents of data memory
let mutable memoryMap : Map<uint32, uint32> = Map.empty
/// Contents of CPU registers
let mutable regMap : Map<CommonData.RName,uint32> = initialRegMap
/// Contents of CPU flags
let mutable flags: CommonData.Flags = initialFlags
/// Values of all Defined Symols
let mutable symbolMap : Map<string, uint32*ExecutionTop.SymbolType> = Map.empty
/// version of symbolMap currently displayed
let mutable displayedSymbolMap : Map<string, uint32*ExecutionTop.SymbolType> = Map.empty

/// Current state of simulator
let mutable runMode: ExecutionTop.RunMode = ExecutionTop.ResetMode

// ***********************************************************************************************
//                                  Mini DSL for creating HTML
// ***********************************************************************************************

let ELEMENT elName classes (htmlElements: HTMLElement list) =
    let ele = document.createElement elName
    ele.classList.add (classes |> List.toArray)
    List.iter (ele.appendChild >> ignore) htmlElements
    ele

let INNERHTML html (ele:HTMLElement) = (ele.innerHTML <- html) ; ele
let ID name (ele:HTMLElement) = (ele.id <- name) ; ele
let CLICKLISTENER listener (ele:HTMLElement) = (ele.addEventListener_click listener) ; ele

let DIV = ELEMENT "div"

let BR() = document.createElement "br"

let FORM classes contents = 
    let form = ELEMENT "form" classes contents
        // disable form submission
    form.onsubmit <- ( fun _ -> false)
    form
