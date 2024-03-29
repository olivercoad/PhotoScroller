﻿module PhotoScroller.PhotoScroller

open Avalonia.Controls
open Avalonia.FuncUI.DSL
open Avalonia.Layout
open Elmish
open System.IO
open Avalonia.Input
open Avalonia.Media.Imaging
open Avalonia.Media
open System
open Avalonia.Threading
open System.Text.RegularExpressions

[<ReferenceEquality>]
type ApplicationComm = {
    Close: unit -> unit
    ToggleFullscreen: unit -> unit
    SetFocus: unit -> unit
}
    
type State = {
    applicationComm : ApplicationComm
    currentFolder : DirectoryInfo
    dirFiles : FileInfo array option
    fileIndex : int
    slideshowEnabled : bool
    errorMessage : string option
    lastScrollTimestamp : uint64
}

type PhotoState = {
    FileName : string
    Bitmap : Result<Bitmap,string>
}

let goodExts = Set.ofList [ ".jpg"; ".jpeg"; ".png"; ".cr2" ]

let loadBitmapFromFile (fileName:string) =
    let ext = (Path.GetExtension fileName).ToLowerInvariant()
    if Set.contains ext goodExts then
        Ok (new Bitmap(fileName))
    else
        Error "unsupported file extension"

let mutable photoState : PhotoState option = None
let loadPhoto fileName =
    let res =
        try
            loadBitmapFromFile fileName
        with
        | e -> Error e.Message
    match res with
    | Ok _ ->
        System.Console.WriteLine("Loaded photo " + fileName)
    | Error e ->
        System.Console.WriteLine("Error loading photo " + fileName + " - " + e)

    photoState <- Some { FileName = fileName; Bitmap = res }
    res

let getPhoto fileName =
    match photoState with
    | Some { FileName = fn; Bitmap = res } when fn = fileName ->
        res
    | Some { Bitmap = Ok bitmap } ->
        bitmap.Dispose()
        loadPhoto fileName
    | _ ->
        loadPhoto fileName

type Msg =
    | Increment
    | Decrement
    | LoadFilesInc of bool
    | LoadFilesDec of bool
    | LoadFiles
    | ScrollEvent of PointerWheelEventArgs
    | SlideshowTick
    | ToggleSlideshow
    | Quit
    | ToggleFullscreen
    | SetFocus
    | Keypress of KeyEventArgs

let digitsRegex = Regex("\d+")
let toSortable (fi:FileSystemInfo) =
    let matches = digitsRegex.Matches(fi.Name)
    let matches = System.Linq.Enumerable.ToArray(matches)
    matches
    |> Seq.sortByDescending(fun m -> m.Length)
    |> Seq.tryHead
    |> Option.map (fun m -> m.Value)
    |> Option.map (fun s-> s.PadLeft(12, '0') + fi.Name)
    |> Option.defaultValue fi.Name
    |> (+) (Path.DirectorySeparatorChar.ToString())
    |> (+) (Path.GetDirectoryName(fi.FullName))
let sortFilesDirs arr =
    arr |> Array.sortBy(toSortable)

let slideshowSub dispatch =
    let invoke() =
        dispatch SlideshowTick
        true
    DispatcherTimer.Run(Func<bool>(invoke), TimeSpan.FromMilliseconds 500.0) |> ignore

let init (applicationComm, path) =
    try
        if File.Exists path then
            let currentFolder = DirectoryInfo(Path.GetDirectoryName path)
            let dirFiles = currentFolder.GetFiles() |> sortFilesDirs
            let fullName = Path.GetFullPath path
            let idx = Array.findIndex (fun (x:FileInfo) -> fullName = x.FullName) dirFiles
            
            { applicationComm = applicationComm; currentFolder = currentFolder; dirFiles = Some dirFiles; fileIndex = idx; slideshowEnabled = false; errorMessage = None; lastScrollTimestamp = 0uL },
            Cmd.batch [ Cmd.ofMsg SetFocus; Cmd.ofSub slideshowSub ]
        else if Directory.Exists path then
            let currentFolder = DirectoryInfo(path)
            
            { applicationComm = applicationComm; currentFolder = currentFolder; dirFiles = None; fileIndex = 0; slideshowEnabled = false; errorMessage = None; lastScrollTimestamp = 0uL },
            Cmd.batch [
                Cmd.ofMsg SetFocus
                Cmd.ofMsg LoadFiles
                Cmd.ofSub slideshowSub
            ]
        else
            let msg = "path is not a file or directory"
            { applicationComm = applicationComm; currentFolder = null; dirFiles = None; fileIndex = 0; slideshowEnabled = false; errorMessage = Some msg; lastScrollTimestamp = 0uL },
            Cmd.batch [
                Cmd.ofMsg SetFocus
            ]
            
    with
    | e ->
        let msg = e.Message
        { applicationComm = applicationComm; currentFolder = null; dirFiles = None; fileIndex = 0; slideshowEnabled = false; errorMessage = Some msg; lastScrollTimestamp = 0uL },
        Cmd.batch [
            Cmd.ofMsg SetFocus // must set focus even when error so that at least hotkeys to exit work
        ]

let handleKeypress (k:KeyEventArgs) =
    match k.KeyModifiers, k.Key with
    | KeyModifiers.None, Key.Left -> Some Decrement
    | KeyModifiers.None, Key.Right -> Some Increment
    | KeyModifiers.None, Key.Q -> Some Quit
    | KeyModifiers.None, Key.Escape -> Some Quit
    | KeyModifiers.Control, Key.W -> Some Quit
    | KeyModifiers.None, Key.F11 -> Some ToggleFullscreen
    | KeyModifiers.None, Key.F -> Some ToggleFullscreen
    | _ -> None

let update (msg: Msg) (state: State) : State * Cmd<Msg> =
    match msg with
    | Keypress k ->
        match handleKeypress k with
        | Some msg ->
            k.Handled <- true
            state, Cmd.ofMsg msg
        | None ->
            state, Cmd.none
    | ToggleSlideshow ->
        { state with slideshowEnabled = not state.slideshowEnabled }, Cmd.none
    | SlideshowTick ->
        state, if state.slideshowEnabled then Cmd.ofMsg Increment else Cmd.none
    | ScrollEvent e ->
        if e.Handled then
            state, Cmd.none
        else
            e.Handled <- true
            // throttle scroll for touchpad that outputs hundreds of fractional deltas
            let isDetentedScroll = (Math.Abs ((e.Delta.X + e.Delta.Y) % 1.)) < Double.Epsilon // delta should be an integer if detented scroll
            let throttleScrollTime = uint64 (50. / (0.1 + Math.Abs (e.Delta.X + e.Delta.Y))) // milliseconds
            if e.Timestamp - state.lastScrollTimestamp < throttleScrollTime && not isDetentedScroll
            then
                state, Cmd.none
            else
                let state = { state with lastScrollTimestamp = e.Timestamp }

                if e.Delta.X < -0.1 then
                    { state with fileIndex = 0 }, Cmd.ofMsg Decrement
                else if e.Delta.X > 0.1 then
                    { state with fileIndex = (match state.dirFiles with | None -> 0 | Some arr -> arr.Length - 1) }, Cmd.ofMsg Increment
                else if e.Delta.Y < 0. then
                    state, Cmd.ofMsg Increment
                else if e.Delta.Y > 0. then
                    state, Cmd.ofMsg Decrement
                else
                    state, Cmd.none
    | Increment ->
        let isEndOfDirectory =
            match state.dirFiles with
            | Some f -> state.fileIndex + 1 >= f.Length
            | None -> true
        if isEndOfDirectory then
            // load next directory
            let parentDir = state.currentFolder.Parent
            let folders = parentDir.GetDirectories() |> sortFilesDirs
            let idx = Array.findIndex (fun (x:DirectoryInfo) -> state.currentFolder.FullName = x.FullName) folders
            let nextFolder, goIn = if idx + 1 >= folders.Length then (parentDir, false) else (folders.[idx + 1], true)
            { state with currentFolder = nextFolder; dirFiles = None; fileIndex = 0 }, Cmd.ofMsg (LoadFilesInc goIn)
        else
            { state with fileIndex = state.fileIndex + 1 }, Cmd.none
    | Decrement ->
        let isStartOfDirectory =
            match state.dirFiles with
            | Some f -> state.fileIndex - 1 < 0 || f.Length = 0
            | None -> true
        if isStartOfDirectory then
            // load into directories
            let directories = state.currentFolder.GetDirectories() |> sortFilesDirs
            if directories.Length > 0 then
                { state with currentFolder = directories.[directories.Length - 1]; dirFiles = None; fileIndex = 0 }, Cmd.ofMsg (LoadFilesDec false)
            else
                // load previous directory
                let parentDir = state.currentFolder.Parent
                let folders = parentDir.GetDirectories() |> sortFilesDirs
                let idx = Array.findIndex (fun (x:DirectoryInfo) -> state.currentFolder.FullName = x.FullName) folders
                if idx > 0 then
                    { state with currentFolder = folders.[idx - 1]; dirFiles = None; fileIndex = 0 }, Cmd.ofMsg (LoadFilesDec false)
                else
                    { state with currentFolder = state.currentFolder.Parent; dirFiles = None; fileIndex = 0 }, Cmd.ofMsg (LoadFilesDec true)
        else
            { state with fileIndex = state.fileIndex - 1 }, Cmd.none
    | LoadFilesInc goIn ->
        let dirFolders = state.currentFolder.GetDirectories() |> sortFilesDirs
        if dirFolders.Length > 0 && goIn then
            { state with currentFolder = dirFolders.[0]; dirFiles = None; fileIndex = -1 }, Cmd.ofMsg (LoadFilesInc goIn)
        else
            let dirFiles = state.currentFolder.GetFiles() |> sortFilesDirs
            { state with dirFiles = Some dirFiles; fileIndex = -1 }, Cmd.ofMsg Increment
    | LoadFilesDec goUp ->
        if goUp then
            // load previous directory
            let parentDir = state.currentFolder.Parent
            let folders = parentDir.GetDirectories() |> sortFilesDirs
            let idx = Array.findIndex (fun (x:DirectoryInfo) -> state.currentFolder.FullName = x.FullName) folders
            if idx > 0 then
                { state with currentFolder = folders.[idx - 1]; dirFiles = None; fileIndex = 0 }, Cmd.ofMsg (LoadFilesDec false)
            else
                { state with currentFolder = state.currentFolder.Parent; dirFiles = None; fileIndex = 0 }, Cmd.ofMsg (LoadFilesDec true)
        else
            let dirFiles = state.currentFolder.GetFiles() |> sortFilesDirs
            { state with dirFiles = Some dirFiles; fileIndex = dirFiles.Length }, Cmd.ofMsg Decrement
    | LoadFiles -> { state with dirFiles = Some (state.currentFolder.GetFiles() |> sortFilesDirs) }, Cmd.none
    | Quit ->
        state.applicationComm.Close ()
        state, Cmd.none
    | ToggleFullscreen ->
        state.applicationComm.ToggleFullscreen ()
        state, Cmd.none
    | SetFocus ->
        state.applicationComm.SetFocus ()
        state, Cmd.none

let viewFile (file:FileInfo) =
    let fileName = file.FullName
    match getPhoto fileName with
    | Ok bitmap ->
        Image.create [
            Image.verticalAlignment VerticalAlignment.Center
            Image.horizontalAlignment HorizontalAlignment.Center
            Image.stretch Stretch.Uniform
            Image.source bitmap
        ] :> Avalonia.FuncUI.Types.IView
    | Error msg ->
        TextBlock.create [
            TextBlock.fontSize 24.
            TextBlock.textAlignment TextAlignment.Center
            TextBlock.verticalAlignment VerticalAlignment.Center
            TextBlock.text msg
        ] :> Avalonia.FuncUI.Types.IView

let view (state: State) (dispatch) =
    Grid.create [
        Grid.onPointerWheelChanged (ScrollEvent >> dispatch)
        Grid.onDoubleTapped (fun _ -> dispatch ToggleSlideshow)
        Grid.onKeyDown (Keypress >> dispatch)
        Grid.focusable true
        Grid.background "black"
        Grid.name "main-grid-for-focus" // used to identify control to focus on startup

        Grid.children [
            match state.errorMessage with
            | Some message ->
                TextBlock.create [
                    TextBlock.text message
                    TextBlock.fontSize 24.
                    TextBlock.textAlignment TextAlignment.Center
                    TextBlock.verticalAlignment VerticalAlignment.Center
                ]
            | None ->
                let dirFiles =
                    match state.dirFiles with
                    | Some dirFiles ->
                        dirFiles
                    | None -> 
                        // dispatch LoadFiles
                        [||]
                let idx = state.fileIndex
                let file, text =
                    if Array.isEmpty dirFiles then
                        null, sprintf "(no files) %s" state.currentFolder.FullName
                    else if idx < 0 || state.fileIndex >= dirFiles.Length then
                        null, sprintf "(%03i/%03i index error) %s" (idx + 1) dirFiles.Length state.currentFolder.FullName
                    else
                        let file = dirFiles.[idx]
                        file, sprintf "(%03i/%03i)  %s" (idx + 1) dirFiles.Length file.FullName

                if (isNull file) then
                    TextBlock.create [
                        TextBlock.text "No file"
                        TextBlock.fontSize 24.
                        TextBlock.textAlignment TextAlignment.Center
                        TextBlock.verticalAlignment VerticalAlignment.Center
                    ]
                else
                    viewFile file

                TextBlock.create [
                    TextBlock.text text
                    TextBlock.verticalAlignment VerticalAlignment.Bottom
                    TextBlock.horizontalAlignment HorizontalAlignment.Left
                    TextBlock.padding (0., 3., 3., 0.)
                    TextBlock.classes [ "fileinfo" ]
                ]
        ]
    ]