using System.Reflection;
using System.Runtime.InteropServices;

// The single source of the version. The window title, the crash log and the EXE's file
// properties all read it from here (Application.ProductVersion = the informational version).
// Bump AssemblyVersion/FileVersion together with InformationalVersion for every release.

[assembly: AssemblyTitle("UV-VIS meas")]
[assembly: AssemblyDescription("Spectrum acquisition for Ocean Optics spectrometers via the OmniDriver .NET API")]
[assembly: AssemblyProduct("UV-VIS meas")]
[assembly: AssemblyVersion("2.1.0.0")]
[assembly: AssemblyFileVersion("2.1.0.0")]
[assembly: AssemblyInformationalVersion("2.1.0")]
[assembly: ComVisible(false)]
[assembly: Guid("8f3c1a62-5b7d-4e19-9c0a-71d2e4b6a83f")]
