This is a simple program for just scrolling through a bunch of pictures.

Why did I make this instead of just using something like FastStone (or Picasa Photo Viewer)?
 - I wanted to scroll through **multiple adjacent directories**
 - I wanted a simple project to try out using [Avalonia.FuncUI](https://github.com/fsprojects/Avalonia.FuncUI) to make a cross platform GUI application with F#.

## How to run

Use the dotnet cli to restore, build and run. Nuget dependencies should automatically get downloaded.

Provide a path to an image or directory with lots of images.

```
dotnet run -- someimage.jpg
```

## Usage

Make sure to provide a path as an argument.

**Vertical scroll** to scroll through adjacent files. If you get to the start or end of the directory, it will just go to the previous or next directory.

**Horizontal scroll** to skip to the start/end of the next/previous directory.

**Double click** to start (fast) slideshow.

**Esc** to close

**F11** to toggle fullscreen

## Notes

**This is just a quick little test project.**

With that in mind, there has been very little (read "no") consideration for performance or file compatibility, so it works best with jpegs and small file sizes.