# WaySharp.Scanner
A C# implementation of `wayland-scanner` for generating C# interfaces to be used by `WaySharp`.

Instead of generating concrete classes, an interface is generated that you can feed into `Registry.Bind<T>`, which will dynamically generate a concrete class definition at runtime.

There might be some name clashes or XML errors at this early stage in development. Please report issues accordingly.

I intend to add more flexibility later on, particularly to allow you to generate concrete classes, or to specify a namespace or automatically determine your working namespace from your current project.

## Usage
```bash
dotnet run <protocol XML file> > MyProtocol.cs
```
e.g.
```bash
dotnet run /usr/share/wayland-protocol/staging/cursor-shape/cursor-shape-v1.xml > CursorShape.cs
```

## Note:
Some argument typenames will have to be manually renamed. Currently, `WaySharp.Scanner` doesn't have any awareness of the C# class names of other Wayland interfaces so it's just performing a naïve snake-case to PascalCase translation. Core interfaces like `wl_surface` and `wl_compositor` will need to be manually corrected from `WlSurface` and `WlCompositor` to `Surface` and `Compositor` until I implement a smarter name lookup system.
