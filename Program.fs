namespace PhotoScroller

open Avalonia
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.FuncUI


type App(path) =
    inherit Application()

    override this.Initialize() =
        this.Styles.Load "avares://Avalonia.Themes.Default/DefaultTheme.xaml"
        this.Styles.Load "avares://Avalonia.Themes.Default/Accents/BaseDark.xaml"
        this.Styles.Load "avares://PhotoScroller/Styles.xaml"

    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktopLifetime ->
            desktopLifetime.MainWindow <- Shell.MainWindow(path)
        | _ -> ()

module Program =
    [<EntryPoint>]
    let main (args: string []) =
        let path = Array.tryHead args |> Option.defaultValue ""
        AppBuilder.Configure<App>(fun () -> App(path))
            .UsePlatformDetect()
            .UseSkia()
            .StartWithClassicDesktopLifetime(args)