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

type MainWindow(path) as this =
    inherit Window()

    do
        this.Title <- "Photo Scroller"
        this.Width <- 800.0
        this.Height <- 600.0
        this.MinWidth <- 400.0
        this.MinHeight <- 300.0
        // this.WindowState <- WindowState.FullScreen

        //this.VisualRoot.VisualRoot.Renderer.DrawFps <- true
        //this.VisualRoot.VisualRoot.Renderer.DrawDirtyRects <- true
        this.ExtendClientAreaToDecorationsHint <- true

        do
            let assets =
                AvaloniaLocator.Current.GetService<Avalonia.Platform.IAssetLoader>()

            use ic =
                assets.Open(System.Uri("avares://PhotoScroller/Assets/icon.ico"))

            this.Icon <- WindowIcon(ic)

        let toggleFullscreen () =
            match this.WindowState with
            | WindowState.FullScreen -> this.WindowState <- WindowState.Normal
            | WindowState.Normal
            | WindowState.Maximized
            | WindowState.Minimized
            | _ -> this.WindowState <- WindowState.FullScreen

        let content = Grid()
        this.Content <- content


        let host = HostControl()
        content.Children.Add host

        let setFocus () =
            let g = host.Content :?> Grid
            if g.Name <> "main-grid-for-focus" then
                failwith "Host content is not the main grid to focus"
            else
                let doFocus () =
                    printfn "Setting focus on %s" g.Name
                    g.Focus()
                if g.IsInitialized then doFocus ()
                else
                    printfn "Grid %s is not yet initialized. Will set focus when initialized" g.Name
                    g.Initialized.Add (fun _ -> doFocus ())

#if DEBUG
        this.AttachDevTools() // use F12 to open devtools
#endif

        // with ExtendClientAreaToDecorationsHint and rendering the image where the titlebar would be,
        // we need to make something draggable in the absence of draggable titlebar.
        // this makes the entire photoscroller draggable.
        host.PointerPressed.Add
            (fun e ->
                if this.WindowState = WindowState.FullScreen then
                    () // don't drag window if fullscreen
                else
                    this.BeginMoveDrag e)

        let applicationComm = {
            PhotoScroller.ToggleFullscreen = toggleFullscreen
            PhotoScroller.Close = this.Close
            PhotoScroller.SetFocus = setFocus
        }

        Elmish.Program.mkProgram PhotoScroller.init PhotoScroller.update PhotoScroller.view
        |> Program.withHost host
        |> Program.withErrorHandler (fun (text, ex) -> eprintfn "%s: %A" text ex)
        |> Program.runWith (applicationComm, path)

