module PhotoScroller.Shell


open Elmish
open Avalonia
open Avalonia.Controls
open Avalonia.Input
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI
open Avalonia.FuncUI.Builder
open Avalonia.FuncUI.Components.Hosts
open Avalonia.FuncUI.Elmish
open Avalonia.Media

type Command(action) =
    interface System.Windows.Input.ICommand with
        member this.CanExecute(parameter: obj) : bool = true

        [<CLIEvent>]
        override this.CanExecuteChanged : IEvent<System.EventHandler, System.EventArgs> = (new Event<_, _>()).Publish

        member this.Execute(parameter: obj) : unit = action ()



type MainWindow(path) as this =
    inherit Window()

    do
        this.Title <- "Photo Scroller"
        this.Width <- 800.0
        this.Height <- 600.0
        this.MinWidth <- 400.0
        this.MinHeight <- 300.0
        this.WindowState <- WindowState.FullScreen

        //this.VisualRoot.VisualRoot.Renderer.DrawFps <- true
        //this.VisualRoot.VisualRoot.Renderer.DrawDirtyRects <- true
        this.ExtendClientAreaToDecorationsHint <- true

        do
            let assets =
                AvaloniaLocator.Current.GetService<Avalonia.Platform.IAssetLoader>()

            use ic =
                assets.Open(System.Uri("avares://PhotoScroller/Assets/icon.ico"))

            this.Icon <- WindowIcon(ic)

        let escHotkey = KeyBinding()
        escHotkey.Gesture <- KeyGesture(Key.Escape)
        escHotkey.Command <- Command(this.Close)

        base.KeyBindings.Add(escHotkey)


        let toggleFullscreen () =
            match this.WindowState with
            | WindowState.FullScreen -> this.WindowState <- WindowState.Normal
            | WindowState.Normal
            | WindowState.Maximized
            | WindowState.Minimized
            | _ -> this.WindowState <- WindowState.FullScreen

        let fullscreenHotkey = KeyBinding()
        fullscreenHotkey.Gesture <- KeyGesture(Key.F11)
        fullscreenHotkey.Command <- Command(toggleFullscreen)

        base.KeyBindings.Add(fullscreenHotkey)

        let content = Grid()
        this.Content <- content


        let host = HostControl()
        content.Children.Add host

        // with ExtendClientAreaToDecorationsHint and rendering the image where the titlebar would be,
        // we need to make something draggable in the absence of draggable titlebar.
        // this makes the entire photoscroller draggable.
        host.PointerPressed.Add
            (fun e ->
                if this.WindowState = WindowState.FullScreen then
                    () // don't drag window if fullscreen
                else
                    this.BeginMoveDrag e)

        Elmish.Program.mkProgram (fun () -> PhotoScroller.init path) PhotoScroller.update PhotoScroller.view
        |> Program.withHost host
        |> Program.withErrorHandler (fun (text, ex) -> eprintfn "%s: %A" text ex)
        |> Program.run

