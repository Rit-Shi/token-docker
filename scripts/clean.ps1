param(
    [switch]$Preview,
    [switch]$IncludeBuild,
    [switch]$IncludeDependencies
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot)).TrimEnd('\')
# Explicit allowlist. Runtime credentials, browser profile, current executable,
# runtime DLLs and third-party licenses are deliberately absent.
$targets = @(
    'work/build-icon.cjs',
    'work/test-console-bridge.cjs',
    'outputs/使用说明.md',
    'outputs/build-glass.ps1',
    'outputs/compact-preview.png',
    'outputs/glass-test.txt',
    'outputs/connection-status.txt',
    'outputs/TokenBar-Compact.exe',
    'outputs/console-bridge.js',
    'outputs/DeepSeekConsole.cs',
    'outputs/GlassBar.cs',
    'outputs/GlassChecks.cs',
    'outputs/Providers.cs',
    'outputs/zhipu.svg',
    'outputs/deepseek.svg',
    'outputs/tokenbar.svg',
    'outputs/tokenbar.ico',
    'outputs/tokenbar.png'
)
if ($IncludeBuild) { $targets += 'build' }
if ($IncludeDependencies) { $targets += '.deps' }

function Assert-SafeTree([string]$Path) {
    $item = Get-Item -LiteralPath $Path -Force
    if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw "Refusing filesystem link: $Path"
    }
    # A build folder may have been used to run the app since it was built.
    # Never recursively delete a directory containing user settings or sessions.
    if ($item.Name -eq 'settings.dat' -or $item.Name -eq 'console-profile' -or $item.Name -like '*.WebView2') {
        throw "Protected application data found: $Path"
    }
    if ($item.PSIsContainer) {
        foreach ($child in Get-ChildItem -LiteralPath $Path -Force) {
            Assert-SafeTree $child.FullName
        }
    }
}

# These copies must exist before removing old source/assets.
foreach ($required in @('src/GlassBar.cs','src/BarFace.cs','src/Paint.cs','src/Limits.cs','src/Providers.cs','src/DeepSeekConsole.cs','src/console-bridge.js','tests/GlassChecks.cs','tests/test-console-bridge.cjs','scripts/build.ps1','scripts/build-icon.cjs','assets/zhipu.svg','assets/deepseek.svg','assets/tokenbar.svg','assets/tokenbar.ico','assets/tokenbar.png','README.md')) {
    if (!(Test-Path -LiteralPath (Join-Path $repoRoot $required) -PathType Leaf)) {
        throw "Missing organized project file; cleanup cancelled: $required"
    }
}

$deleted = 0
$skipped = 0
foreach ($relative in $targets) {
    try {
        $target = [IO.Path]::GetFullPath((Join-Path $repoRoot $relative))
        if (!$target.StartsWith($repoRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
            throw "Path outside project: $target"
        }
        $parent = Split-Path -Parent $target
        while ($parent.Length -ge $repoRoot.Length) {
            if (Test-Path -LiteralPath $parent) {
                if ((Get-Item -LiteralPath $parent -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
                    throw "Refusing linked parent: $parent"
                }
            }
            if ($parent -eq $repoRoot) { break }
            $parent = Split-Path -Parent $parent
        }
        if (!(Test-Path -LiteralPath $target)) { continue }
        Assert-SafeTree $target
        if ($Preview) { Write-Host "Would delete: $relative"; continue }
        Remove-Item -LiteralPath $target -Recurse -Force
        $deleted++
        Write-Host "Deleted: $relative"
    } catch {
        $skipped++
        Write-Warning "Skipped $relative : $($_.Exception.Message)"
    }
}
if ($Preview) { Write-Host 'Preview complete. No files deleted.' }
else { Write-Host "Done. Deleted targets: $deleted; skipped: $skipped." }
Write-Host 'Tray app, runtime DLLs, licenses, settings and login profile are preserved.'
