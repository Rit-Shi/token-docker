param([switch]$Test, [switch]$Offline)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$sdkVersion = '1.0.4191.47'
$sdkRoot = Join-Path $repoRoot ".deps/webview2-$sdkVersion"
$buildRoot = Join-Path $repoRoot 'build'
$frameworkDir = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$compiler = Join-Path $frameworkDir 'csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw 'Windows x64 and .NET Framework 4.x are required.' }
$coreDll = Join-Path $sdkRoot 'lib/net462/Microsoft.Web.WebView2.Core.dll'
if (!(Test-Path -LiteralPath $coreDll)) {
    if ($Offline) { throw 'WebView2 SDK cache missing. Run once without -Offline to restore from NuGet.' }
    [IO.Directory]::CreateDirectory($sdkRoot) | Out-Null
    $archive = Join-Path $repoRoot ".deps/webview2-$sdkVersion.zip"
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -UseBasicParsing -Uri "https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/$sdkVersion/microsoft.web.webview2.$sdkVersion.nupkg" -OutFile $archive
    Expand-Archive -LiteralPath $archive -DestinationPath $sdkRoot -Force
}
[IO.Directory]::CreateDirectory($buildRoot) | Out-Null
$wpfDll = Join-Path $sdkRoot 'lib/net462/Microsoft.Web.WebView2.Wpf.dll'
$loaderDll = Join-Path $sdkRoot 'runtimes/win-x64/native/WebView2Loader.dll'
foreach ($dependency in @($coreDll,$wpfDll,$loaderDll)) {
    if (!(Test-Path -LiteralPath $dependency)) { throw "Missing SDK file: $dependency" }
    Copy-Item -LiteralPath $dependency -Destination $buildRoot -Force
}
Copy-Item -LiteralPath (Join-Path $repoRoot 'licenses/WebView2-LICENSE.txt') -Destination $buildRoot -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'licenses/LobeHub-LICENSE.txt') -Destination $buildRoot -Force
$references = @('PresentationCore.dll','PresentationFramework.dll','WindowsBase.dll') | ForEach-Object { '/reference:' + (Join-Path $frameworkDir "WPF/$_") }
$icon = Join-Path $repoRoot 'assets/tokenbar.ico'
$sources = @('src/Providers.cs','src/Limits.cs','src/Paint.cs','src/Catalog.cs','src/DailyLog.cs','src/BarFace.cs','src/TaskbarFace.cs','src/TaskbarStrip.cs','src/MiMoConsole.cs','src/SettingsWindow.cs','src/GlassBar.cs','src/DeepSeekConsole.cs','tests/GlassChecks.cs') | ForEach-Object { Join-Path $repoRoot $_ }
$resources = @('assets/zhipu.svg,zhipu.svg','assets/deepseek.svg,deepseek.svg','assets/kimi.svg,kimi.svg','assets/kimi-color.svg,kimi-color.svg','assets/minimax.svg,minimax.svg','assets/minimax-color.svg,minimax-color.svg','src/console-bridge.js,console-bridge.js','assets/tokenbar.ico,tokenbar.ico') | ForEach-Object { '/resource:' + (Join-Path $repoRoot $_) }
$executable = Join-Path $buildRoot 'TokenBar-Tray.exe'
& $compiler /nologo /platform:x64 /target:winexe "/out:$executable" "/win32icon:$icon" $resources $references "/reference:$coreDll" "/reference:$wpfDll" /reference:System.Xaml.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.Net.Http.dll /reference:System.Security.dll $sources
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed. Exit any running app in build/ before rebuilding.' }
if ($Test) {
    $process = Start-Process -FilePath $executable -ArgumentList '--test' -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "Tests failed; see $buildRoot/glass-test.txt" }
    Get-Content -LiteralPath (Join-Path $buildRoot 'glass-test.txt')
}
Write-Host "Built: $executable"
