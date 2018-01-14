# WinRTDesktopInterop
This sample shows how to use custom Windows Runtime component from .NET desktop app.

Please see discussion at https://github.com/Microsoft/cppwinrt/issues/256 for more info.


## How to build

* Install Windows 10 SDK build 17061 - you will need a _cppwinrt.exe_ compiler.
* Build WinRTComponent project. This will create an _include_ folder in the solution dir.
* Download Microsoft Detours library and build it.
* Place _detours.h_ inside _include_ folder and _detours.lib_ for x86 and x64 inside _include\x86_ and _include\x64_ folders respectively.
* Build the other projects in the soltuion.
