<#
.SYNOPSIS
    署名・パッケージ後の最終サニティチェック。配布事故 (未署名 EXE の混入 /
    バージョン不一致 / ZIP 中身の取り違え) を出荷前に機械的に検出する。

.DESCRIPTION
    sign-installer.ps1 → package-release.ps1 の後に実行する「配布ゲート」。
    人間の目視に頼らず、以下を1コマンドで検証する:

      1. installer\output の Setup EXE が Authenticode 'Valid' で署名されているか
      2. 配布 ZIP の中に同梱された EXE も 'Valid' か
         (= 署名前の古い EXE が ZIP に残っていないか。最頻の配布事故)
      3. EXE / ZIP のファイル名バージョンと、csproj <Version> / iss
         #define AppVersion が全て一致しているか (バージョン同期ルール)

    Authenticode 検証は Get-AuthenticodeSignature (PowerShell 組込) を使うため
    Windows SDK / signtool.exe は不要。PowerShell 5.1 で動作する。

.PARAMETER InstallerPath
    検証する Setup EXE。既定は installer\output\*_Setup_v*.exe の最新。

.PARAMETER ZipPath
    検証する配布 ZIP。既定は installer\output\<Product>_v<Version>.zip。

.PARAMETER CsprojPath
    バージョン照合する csproj。既定は ..\PDFPuzzle\PDFPuzzle.csproj。

.PARAMETER IssPath
    バージョン照合する iss。既定は .\PDFPuzzle.iss。

.PARAMETER AllowUnsigned
    署名状態の不一致を警告に格下げする (CI の途中確認用)。
    既定では署名が Valid でなければ exit 5。

.EXAMPLE
    .\verify-signed-package.ps1
    # 署名・ZIP 化が終わった後、配布前に実行。exit 0 なら出荷可。

.NOTES
    終了コード:
      0  全チェック合格 (出荷可)
      2  入力不正 (EXE / ZIP / csproj / iss が見つからない)
      5  EXE または ZIP 内 EXE が未署名 / 署名無効
      6  バージョン不一致 (csproj / iss / ファイル名のどれかがズレ)
      99 未捕捉例外
#>
[CmdletBinding()]
param(
    [string]$InstallerPath = "",
    [string]$ZipPath = "",
    [string]$CsprojPath = "",
    [string]$IssPath = "",
    [switch]$AllowUnsigned
)

$ErrorActionPreference = "Stop"

function Write-VfLog {
    param([string]$Message, [string]$Level = "INFO")
    $ts = (Get-Date).ToString("HH:mm:ss")
    Write-Host "[verify $ts $Level] $Message"
}

function Resolve-OrFail {
    param([string]$Hint, [string]$Pattern, [string]$Label)
    if (-not [string]::IsNullOrEmpty($Hint)) {
        if (Test-Path $Hint -PathType Leaf) { return (Resolve-Path $Hint).Path }
        Write-VfLog "$Label not found: $Hint" "ERROR"
        exit 2
    }
    $outDir = Join-Path $PSScriptRoot "output"
    $cands = Get-ChildItem -Path $outDir -Filter $Pattern -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending
    if (-not $cands -or @($cands).Count -eq 0) {
        Write-VfLog "No $Pattern under $outDir." "ERROR"
        exit 2
    }
    return @($cands)[0].FullName
}

function Get-FileNameVersion {
    param([string]$Path, [string]$Pattern)
    $stem = [System.IO.Path]::GetFileNameWithoutExtension($Path)
    $m = [regex]::Match($stem, $Pattern)
    if ($m.Success) { return $m.Groups['ver'].Value }
    return "unknown"
}

function Get-CsprojVersion {
    param([string]$Path)
    if (-not (Test-Path $Path -PathType Leaf)) {
        Write-VfLog "csproj not found: $Path" "ERROR"; exit 2
    }
    $m = [regex]::Match((Get-Content -Raw -Path $Path), '<Version>\s*(?<v>[0-9]+\.[0-9]+\.[0-9]+(?:\.[0-9]+)?)\s*</Version>')
    if (-not $m.Success) { Write-VfLog "<Version> not found in $Path" "ERROR"; exit 6 }
    return $m.Groups['v'].Value
}

function Get-IssVersion {
    param([string]$Path)
    if (-not (Test-Path $Path -PathType Leaf)) {
        Write-VfLog "iss not found: $Path" "ERROR"; exit 2
    }
    $m = [regex]::Match((Get-Content -Raw -Path $Path), '#define\s+AppVersion\s+"(?<v>[0-9]+\.[0-9]+\.[0-9]+(?:\.[0-9]+)?)"')
    if (-not $m.Success) { Write-VfLog "#define AppVersion not found in $Path" "ERROR"; exit 6 }
    return $m.Groups['v'].Value
}

function Test-AuthenticodeOrFail {
    param([string]$Path, [string]$Label)
    $sig = Get-AuthenticodeSignature -FilePath $Path
    Write-VfLog "$Label Authenticode: $($sig.Status)"
    if ($sig.SignerCertificate) {
        Write-VfLog "$Label Signer: $($sig.SignerCertificate.Subject)"
    }
    if ($sig.Status -ne 'Valid') {
        if ($AllowUnsigned) {
            Write-VfLog "$Label not Valid but -AllowUnsigned given." "WARN"
            return $false
        }
        Write-VfLog "$Label is not validly signed (status=$($sig.Status))." "ERROR"
        exit 5
    }
    return $true
}

function Test-ZipEmbeddedExe {
    param([string]$Zip, [string]$ExpectedExeName)
    Add-Type -AssemblyName System.IO.Compression.FileSystem | Out-Null
    Add-Type -AssemblyName System.IO.Compression | Out-Null

    $tmp = Join-Path $env:TEMP ("pdfpz_verify_" + [System.Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $tmp -Force | Out-Null
    try {
        $archive = [System.IO.Compression.ZipFile]::OpenRead($Zip)
        try {
            $entry = $archive.Entries | Where-Object { $_.Name -ieq $ExpectedExeName } | Select-Object -First 1
            if ($null -eq $entry) {
                Write-VfLog "ZIP does not contain expected EXE '$ExpectedExeName'. Entries: $(@($archive.Entries | ForEach-Object Name) -join ', ')" "ERROR"
                exit 5
            }
            $dest = Join-Path $tmp $entry.Name
            [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $dest, $true)
            Write-VfLog "Extracted '$($entry.Name)' from ZIP for signature check."
            return (Test-AuthenticodeOrFail -Path $dest -Label "ZIP-embedded EXE")
        } finally {
            $archive.Dispose()
        }
    } finally {
        Remove-Item -Path $tmp -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Main {
    Write-VfLog "==================================================================="
    Write-VfLog "Pre-distribution verification gate (Sala 2026-06-05)"
    Write-VfLog "==================================================================="

    $InstallerPath = Resolve-OrFail -Hint $InstallerPath -Pattern "*_Setup_v*.exe" -Label "InstallerPath"
    $ZipPath       = Resolve-OrFail -Hint $ZipPath       -Pattern "*_v*.zip"       -Label "ZipPath"
    if ([string]::IsNullOrEmpty($CsprojPath)) {
        $CsprojPath = Join-Path $PSScriptRoot "..\PDFPuzzle\PDFPuzzle.csproj"
    }
    if ([string]::IsNullOrEmpty($IssPath)) {
        $IssPath = Join-Path $PSScriptRoot "PDFPuzzle.iss"
    }

    Write-VfLog "EXE : $InstallerPath"
    Write-VfLog "ZIP : $ZipPath"

    # --- Check 1: EXE signature ---
    Test-AuthenticodeOrFail -Path $InstallerPath -Label "Setup EXE" | Out-Null

    # --- Check 2: ZIP-embedded EXE signature (stale-ZIP trap) ---
    $exeName = Split-Path $InstallerPath -Leaf
    Test-ZipEmbeddedExe -Zip $ZipPath -ExpectedExeName $exeName | Out-Null

    # --- Check 3: version sync ---
    $vCsproj = Get-CsprojVersion -Path $CsprojPath
    $vIss    = Get-IssVersion -Path $IssPath
    $vExe    = Get-FileNameVersion -Path $InstallerPath -Pattern '_Setup_v(?<ver>[0-9]+\.[0-9]+\.[0-9]+(?:\.[0-9]+)?)$'
    $vZip    = Get-FileNameVersion -Path $ZipPath -Pattern '_v(?<ver>[0-9]+\.[0-9]+\.[0-9]+(?:\.[0-9]+)?)$'

    Write-VfLog "Version  csproj=$vCsproj  iss=$vIss  exe=$vExe  zip=$vZip"
    $distinct = @($vCsproj, $vIss, $vExe, $vZip) | Select-Object -Unique
    if (@($distinct).Count -ne 1) {
        Write-VfLog "Version mismatch across csproj / iss / exe / zip. All four must match." "ERROR"
        exit 6
    }

    Write-VfLog "==================================================================="
    Write-VfLog "ALL CHECKS PASSED. Signed, version-consistent, ready for distribution (v$vCsproj)."
    Write-VfLog "==================================================================="
    exit 0
}

try {
    Main
} catch {
    Write-VfLog "Unhandled exception: $($_.Exception.Message)" "ERROR"
    Write-VfLog $_.ScriptStackTrace "ERROR"
    exit 99
}
