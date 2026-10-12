## README

This is a project to test compatablity of Rhino common code with Linux.

The project contains multiple test projects to evaluate either normal RhinoCommon code or ASP Net server hosting with RhinoInside as a solver.:

```text
RhinoLinuxTest/
├── PROGRESS.md
├── README-rhino-compute-linux-repro.md
├── doc/
└── RhinoLinxTest/
    ├── RhinoLinxTest.sln
    ├── RhinoInside.BoxSample/
    ├── RhinoInside.BoxSolver/
    ├── RhinoInside.MinimalHost/
    ├── RhinoInside.AspNetHost/
    ├── RhinoComputeLinuxRepro/
    ├── RhinoComputeLinuxRepro.Runner/
    ├── RhinoLib/
    ├── RhinoLinxTest/
    └── RhinoWindowsTest/
```

  


## Build

Run from this solution directory on Linux:

```bash
dotnet build RhinoLinxTest.sln --configuration Release
```

The runner targets .NET 10 and references the same Rhino.Inside/RhinoCommon package versions as the standalone reproduction. RhinoCommon is compile-only; the standalone mode resolves the installed Rhino runtime through Rhino.Inside.

Use a normal system or local .NET 10 installation. On the tested Ubuntu VM, the Snap-managed `dotnet` host injected Core 22's older `libstdc++.so.6`, causing a pre-start `GLIBCXX_3.4.32` error. That native-loader failure is unrelated to Rhino licensing.
