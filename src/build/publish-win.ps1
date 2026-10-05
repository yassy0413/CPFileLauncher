# Windows 配布物 bin/<rid>/CPFileLauncher.exe を作る（SPEC §10.4、issue/M6_DISTRIBUTION_WINDOWS.md）。
# 使い方: powershell -ExecutionPolicy Bypass -File src\build\publish-win.ps1 [-Rid win-x64|win-arm64]（既定は両方）
# Windows PowerShell 5.1 で動くように書く（&& / ?? などは使わない）。このファイルは BOM 付き UTF-8 で保存する
param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string[]]$Rid = @('win-x64', 'win-arm64')
)
$ErrorActionPreference = 'Stop'

$Exe = 'CPFileLauncher' # 実行ファイル名（csproj の AssemblyName）
$Root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$Project = Join-Path $Root 'src\FileLauncher.Desktop\FileLauncher.Desktop.csproj'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    $env:PATH = 'C:\Program Files\dotnet;' + $env:PATH
}

$running = Get-Process $Exe -ErrorAction SilentlyContinue
if ($running) {
    throw "$Exe が起動中です。終了してから実行してください（exe がロックされます）"
}

foreach ($r in $Rid) {
    $out = Join-Path $Root "bin\$r"
    Write-Host "==> dotnet publish ($r, Release, self-contained, single-file, ReadyToRun)"
    if (Test-Path $out) { Remove-Item -Recurse -Force $out }

    # ReadyToRun（事前コンパイル）: Mac と同じ方針（SPEC §10.4）。PublishTrimmed は使わない（Avalonia のリフレクションと相性が悪い）。
    # SharpHook / Skia / HarfBuzz / ANGLE のネイティブ dll は exe の隣に出るので IncludeNativeLibrariesForSelfExtract で exe に収める
    # （初回起動時に %TEMP%\.net\CPFileLauncher\<hash>\ へ展開される）。pdb は配らない
    & dotnet publish $Project -c Release -r $r --self-contained `
        -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=none -o $out -nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish に失敗しました ($r)" }

    $file = Get-Item (Join-Path $out "$Exe.exe")
    $others = Get-ChildItem $out -File | Where-Object { $_.Name -ne $file.Name }
    if ($others) {
        # フォントのライセンス文（Resources\Fonts\OFL-*.txt）はフォルダの中なので、ここに出るのは想定外のファイル
        Write-Warning ("exe の隣に想定外のファイルがあります: " + (($others | ForEach-Object { $_.Name }) -join ', '))
    }
    Write-Host ("完了: {0}  {1:N1} MB" -f $file.FullName, ($file.Length / 1MB))
}
