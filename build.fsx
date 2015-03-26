// Author: Cody Russell <cody@jhu.edu>

#I "tools/FAKE/tools"
#I "tools/FSharp.Data/lib/net40"
#r "FakeLib.dll"
#r "FSharp.Data"

open System
open System.IO
open Fake
open Fake.FileUtils
open Fake.FileHelper
open FSharp.Data

type OS =
  Mac | Windows

type Arch =
  X86 | X64

let removeDir subdir project =
  let path = Path.Combine(subdir, project)
  Directory.Delete path, true

let removeBuild project = removeDir "build" project

let sh command args =
  ProcessHelper.ExecProcessAndReturnMessages (fun info ->
    info.FileName <- command
    info.Arguments <- args
  ) TimeSpan.MaxValue

let filenameFromUrl (url:string) =
    url.Split('/')
    |> Array.toList
    |> List.rev
    |> List.head

let installDir = Path.Combine(pwd(), "install")

let extract (file:string) =
  ensureDirectory "build"
  ensureDirectory(Path.Combine("build", "win32"))

  let stacks = Path.Combine("build", "Win32")
  Path.Combine("patches/stack.props") |> CopyFile stacks

  let path = Path.Combine("build", "win32", Path.GetFileNameWithoutExtension(file))
  printfn "About to look for %s" path
  if not (Directory.Exists(path)) then
      printfn "extracting %s" file

      sprintf "x %s -obuild\win32" file
      |> sh "C:\Program Files\7-Zip\7z.exe"
      |> ignore

let from (action: unit -> unit) (path: string) =
    pushd path
    action ()
    popd()

let urls = Map [("atk", "http://dl.hexchat.net/gtk-win32/src/atk-2.14.0.7z");
                ("cairo", "http://dl.hexchat.net/gtk-win32/src/cairo-1.14.0.7z");
                ("fontconfig", "http://dl.hexchat.net/gtk-win32/src/fontconfig-2.8.0.7z");
                ("freetype", "http://dl.hexchat.net/gtk-win32/src/freetype-2.5.5.7z");
                ("gdk-pixbuf", "http://dl.hexchat.net/gtk-win32/src/gdk-pixbuf-2.30.8.7z");
                ("gettext-runtime", "http://dl.hexchat.net/gtk-win32/src/gettext-runtime-0.18.7z");
                ("glib", "http://dl.hexchat.net/gtk-win32/src/glib-2.42.1.7z");
                ("gtk", "http://dl.hexchat.net/gtk-win32/src/gtk-2.24.25.7z");
                ("harfbuzz", "http://dl.hexchat.net/gtk-win32/src/harfbuzz-0.9.37.7z");
                ("libffi", "http://dl.hexchat.net/gtk-win32/src/libffi-3.0.13.7z");
                ("libpng", "http://dl.hexchat.net/gtk-win32/src/libpng-1.6.16.7z");
                ("libxml2", "http://dl.hexchat.net/gtk-win32/src/libxml2-2.9.1.7z");
                ("openssl", "http://dl.hexchat.net/gtk-win32/src/openssl-1.0.1l.7z");
                ("pango", "http://dl.hexchat.net/gtk-win32/src/pango-1.36.8.7z");
                ("pixman", "http://dl.hexchat.net/gtk-win32/src/pixman-0.32.6.7z");
                ("win-iconv", "http://dl.hexchat.net/gtk-win32/src/win-iconv-0.0.6.7z");
                ("zlib", "http://dl.hexchat.net/gtk-win32/src/zlib-1.2.8.7z")]

// Targets
// --------------------------------------------------------
Target "FetchAll" <| fun _ ->
  let downloadFile (url:string) =
    let filename = url.Split('/') |> Array.toList
                                  |> List.rev
                                  |> List.head
    let path = Path.Combine("src", filename)

    match fileExists(path) with
      | true -> printfn "%s already downloaded, skipping." filename
      | false -> match Http.Request(url).Body with
                 | Text text ->
                   printfn "Received text instead of binary from %s" url
                 | Binary bytes ->
                   File.WriteAllBytes(path, bytes)
                   printfn "Downloaded: %s" filename

    path

  ensureDirectory "src"
  urls |> Map.iter (fun k v -> downloadFile(v) |> extract)

Target "freetype" <| fun _ ->
  trace "freetype"

  let srcvcpath = Path.Combine(pwd(), "github", "gtk-win32", "freetype", "builds", "windows", "vc2013")
  let vcpath = Path.Combine(pwd(), "build", "Win32", "freetype-2.5.5", "builds", "windows", "vc2013")

  ensureDirectory vcpath
  CopyRecursive srcvcpath vcpath true
  |> Log "Copying files: "

  let file = Path.Combine(vcpath, "freetype.vcxproj")
  sprintf "%s /p:Platform=%s /p:Configuration=Release /maxcpucount /nodeReuse:True" file "Win32"
  |> sh "msbuild"
  |> ignore

  ensureDirectory installDir

  let sourceDir = Path.Combine(pwd(), "build", "win32", "freetype-2.5.5")
  let includeDir = Path.Combine(installDir, "include")
  ensureDirectory(includeDir)

  let includeSrc = Path.Combine(sourceDir, "include")
  let includeFiles = Directory.GetFiles(Path.Combine(sourceDir, "include"), "*.*", SearchOption.AllDirectories)

  logfn "CopyDir %s -> %s" (Path.Combine(includeDir, "config")) (Path.Combine(includeSrc, "config"))

  CopyFiles includeDir includeFiles

  // XXX Not sure why this doesn't work.
  //CopyDir (Path.Combine(includeDir, "config")) (Path.Combine(includeSrc, "config"))

  ensureDirectory (Path.Combine(includeDir, "config"))
  CopyFiles (Path.Combine(includeDir, "config")) (Directory.GetFiles(Path.Combine(includeSrc, "config"), "*.*", SearchOption.AllDirectories))

  // </XXX>

  let libDir = Path.Combine(installDir, "lib")
  [ Path.Combine(sourceDir, "objs", "vc2013", "Win32", "freetype.lib")]
  |> Copy libDir

Target "libffi" <| fun _ ->
  trace "libffi"

Target "openssl" <| fun _ ->
  trace "openssl"

Target "gettext-runtime" <| fun _ ->
  trace "gettext-runtime"

  ensureDirectory installDir

  let iconvHeaders = Path.Combine(installDir, "..", "..", "..", "gtk", "Win32", "include")
  let iconvLib = Path.Combine(installDir, "..", "..", "..", "gtk", "Win32", "lib", "iconv.lib")

  Path.Combine(pwd(), "build", "Win32", "gettext-runtime-0.18")
  |> from (fun () ->
        sprintf "-G \"NMake Makefiles\" \"-DCMAKE_INSTALL_PREFIX=%s\" -DCMAKE_BUILD_TYPE=Debug -DICONV_INCLUDE_DIR=%s -DICONV_LIBRARIES=%s" installDir iconvHeaders iconvLib
        |> sh "cmake"
        |> ignore

        sh "nmake" "clean" |> ignore
        sh "nmake" "install" |> ignore
     )
  |> ignore

Target "libxml2" <| fun _ ->
  trace "libxml2"

Target "fontconfig" <| fun _ ->
  trace "fontconfig"

Target "pixman" <| fun _ ->
  trace "pixman"

Target "glib" <| fun _ ->
  trace "glib"

Target "cairo" <| fun _ ->
  trace "cairo"

Target "harfbuzz" <| fun _ ->
  trace "harfbuzz"

Target "atk" <| fun _ ->
  trace "atk"

Target "gdk-pixbuf" <| fun _ ->
  trace "gdk-pixbuf"

Target "pango" <| fun _ ->
  trace "pango"

Target "gtk" <| fun _ ->
  trace "gtk"

Target "zlib" <| fun _ ->
  trace "zlib"

  let vcpath = Path.Combine(pwd(), "slns", "zlib", "contrib", "vstudio", "vc12")
  let file = Path.Combine(vcpath, "zlibvc.sln")
  sprintf "%s /p:Platform=%s /p:Configuration=ReleaseWithoutAsm /maxcpucount /nodeReuse:True" file "Win32"
  |> sh "msbuild"
  |> ignore

  ensureDirectory installDir

  let sourceDir = Path.Combine(pwd(), "build", "Win32", "zlib-1.2.8")
  let includeDir = Path.Combine(installDir, "include")
  ensureDirectory(includeDir)
  [ Path.Combine(sourceDir, "zlib.h") ] |> Copy includeDir

  (*
  Path.Combine(pwd(), "slns", "zlib", "contrib", "vstudio", "vc12")
  |> from (fun () ->
        let binDir = Path.Combine(installDir, "bin")
        ensureDirectory binDir

        [
            Path.Combine("TestZlibDllRelease", "testzlibdll.exe");
            Path.Combine("TestZlibDllRelease", "testzlibdll.pdb");
            Path.Combine("TestZlibReleaseWithoutAsm", "testzlib.exe");
            Path.Combine("TestZlibReleaseWithoutAsm", "testzlib.pdb");
            Path.Combine("ZlibDllReleaseWithoutAsm", "zlib1.dll");
            Path.Combine("ZlibDllReleaseWithoutAsm", "zlib1.map");
            Path.Combine("ZlibDllReleaseWithoutAsm", "zlib1.pdb")
        ] |> Copy binDir
     )
  |> ignore
  *)

Target "win-iconv" <| fun _ ->
  trace "win-iconv"
  ensureDirectory installDir
  Path.Combine(pwd(), "build", "Win32", "win-iconv-0.0.6")
  |> from (fun () ->
        sprintf "-G \"NMake Makefiles\" \"-DCMAKE_INSTALL_PREFIX=%s\" -DCMAKE_BUILD_TYPE=Debug" installDir
        |> sh "cmake"
        |> ignore

        sh "nmake" "clean" |> ignore
        sh "nmake" "install" |> ignore
     )
  |> ignore

Target "libpng" <| fun _ ->
  trace "libpng"

Target "BuildAll" <| fun _ ->
  let config = getBuildParamOrDefault "config" "debug"
  trace("BuildAll " + config)

// Dependencies
// --------------------------------------------------------
"atk" <== ["glib"]
"cairo" <== ["fontconfig"; "glib"; "pixman"]
"fontconfig" <== ["freetype"; "libxml2"]
"gdk-pixbuf" <== ["glib"; "libpng"]
"gettext-runtime" <== ["win-iconv"]
"glib" <== ["gettext-runtime"; "libffi"; "zlib"]
"gtk" <== ["atk"; "gdk-pixbuf"; "pango"]
"harfbuzz" <== ["freetype"; "glib"]
"libpng" <== ["zlib"]
"libxml2" <== ["win-iconv"]
"openssl" <== ["zlib"]
"pango" <== ["cairo"; "harfbuzz"]
"pixman" <== ["libpng"]

"BuildAll" <== ["FetchAll"; "gtk"]

RunTargetOrDefault "BuildAll"