<#
.SYNOPSIS
    署名済みインストーラ EXE と同梱 README を ZIP にまとめ、配布用パッケージ
    (<Product>_v<Version>.zip) を生成する。

.DESCRIPTION
    Gumroad / STORES 配布用の最終 ZIP を再現可能な1コマンドで生成する。
    既定では Authenticode 署名が "Valid" でない EXE は配布事故防止のため拒否する
    (-AllowUnsigned で上書き可能)。

    想定フロー:
      1. installer\build_release_setup.bat        … 未署名 EXE を生成
      2. installer\sign-installer.ps1              … EV 署名 (要 ESIGNER_* / CodeSignTool)
      3. installer\package-release.ps1  ← 本script … 署名済 EXE + README を ZIP 化

    Authenticode 検証は Get-AuthenticodeSignature (PowerShell 組込) を用いるため
    Windows SDK / signtool.exe は不要。

.PARAMETER InstallerPath
    ZIP に含める EXE への絶対パス。
    既定: installer\output\PDFPuzzle_Setup_v<Version>.exe を自動解決 (output 内の唯一の Setup EXE)。

.PARAMETER ReadmePath
    同梱する README。既定は本スクリプトと同じ installer\ 配下の
    「はじめにお読みください.txt」。

.PARAMETER OutputZip
    出力 ZIP パス。既定は installer\output\<Product>_v<Version>.zip。

.PARAMETER AllowUnsigned
    指定すると Authenticode 検証をスキップ (未署名の暫定配布用)。
    未指定時、署名が Valid でなければ exit 5 で停止する。

.EXAMPLE
    # 署名済み EXE を配布 ZIP 化 (通常運用)
    .\package-release.ps1

.EXAMPLE
    # 未署名のまま暫定 ZIP 化
    .\package-release.ps1 -AllowUnsigned

.NOTES
    PowerShell 5.1 (Windows 既定) で動作。製品固有名は InstallerPath から派生させる。
    終了コード: 0 成功 / 2 入力不正 / 5 未署名拒否 / 99 未捕捉例外
#>
[CmdletBinding()]
param(
    [string]$InstallerPath = "",
    [string]$ReadmePath = "",
    [string]$OutputZip = "",
    [switch]$AllowUnsigned
)

$ErrorActionPreference = "Stop"

function Write-PkgLog {
    param([string]$Message, [string]$Level = "INFO")
    $ts = (Get-Date).ToString("HH:mm:ss")
    Write-Host "[pkg $ts $Level] $Message"
}

function Resolve-InstallerPath {
    param([string]$Hint)
    if (-not [string]::IsNullOrEmpty($Hint)) {
        if (Test-Path $Hint -PathType Leaf) { return (Resolve-Path $Hint).Path }
        Write-PkgLog "InstallerPath not found: $Hint" "ERROR"
        exit 2
    }
    $outDir = Join-Path $PSScriptRoot "output"
    $candidates = Get-ChildItem -Path $outDir -Filter "*_Setup_v*.exe" -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending
    if (-not $candidates -or @($candidates).Count -eq 0) {
        Write-PkgLog "No *_Setup_v*.exe found under $outDir. Build first (build_release_setup.bat)." "ERROR"
        exit 2
    }
    $picked = @($candidates)[0].FullName
    Write-PkgLog "Auto-resolved installer: $picked"
    return $picked
}

function Get-ProductMetadata {
    param([string]$Path)
    $stem = [System.IO.Path]::GetFileNameWithoutExtension($Path)
    $match = [regex]::Match($stem, '^(?<name>.+?)_Setup_v(?<ver>[0-9]+\.[0-9]+\.[0-9]+(?:\.[0-9]+)?)$')
    if ($match.Success) {
        return @{ ProductName = $match.Groups['name'].Value; Version = $match.Groups['ver'].Value }
    }
    Write-PkgLog "Filename does not match <Product>_Setup_v<Version>.exe. Falling back to stem." "WARN"
    return @{ ProductName = $stem; Version = "unknown" }
}

function Test-AuthenticodeValid {
    param([string]$Path)
    $sig = Get-AuthenticodeSignature -FilePath $Path
    Write-PkgLog "Authenticode status: $($sig.Status)"
    if ($sig.SignerCertificate) {
        Write-PkgLog "Signer: $($sig.SignerCertificate.Subject)"
    }
    if ($sig.Status -ne 'Valid') {
        if ($AllowUnsigned) {
            Write-PkgLog "Signature not Valid but -AllowUnsigned given. Proceeding with UNSIGNED package." "WARN"
            return
        }
        Write-PkgLog "Installer is not validly signed (status=$($sig.Status)). Refusing to package." "ERROR"
        Write-PkgLog "Run sign-installer.ps1 first, or pass -AllowUnsigned for an interim unsigned bundle." "ERROR"
        exit 5
    }
}

function New-DistributionZip {
    param([string]$ExePath, [string]$Readme, [string]$ZipPath)

    if (Test-Path $ZipPath) {
        Write-PkgLog "Removing existing zip: $ZipPath" "WARN"
        Remove-Item -Path $ZipPath -Force
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem | Out-Null
    # PS 5.1: ZipFile/ZipFileExtensions は FileSystem.dll、ZipArchiveMode/CompressionLevel
    # は System.IO.Compression.dll に定義されるため、両方ロードする。
    Add-Type -AssemblyName System.IO.Compression | Out-Null
    $level = [System.IO.Compression.CompressionLevel]::Optimal
    $zip = [System.IO.Compression.ZipFile]::Open($ZipPath, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $ExePath, (Split-Path $ExePath -Leaf), $level) | Out-Null
        Write-PkgLog "  + $(Split-Path $ExePath -Leaf)"
        if (Test-Path $Readme -PathType Leaf) {
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $Readme, (Split-Path $Readme -Leaf), $level) | Out-Null
            Write-PkgLog "  + $(Split-Path $Readme -Leaf)"
        } else {
            Write-PkgLog "README not found, packaging EXE only: $Readme" "WARN"
        }
    } finally {
        $zip.Dispose()
    }
}

function Main {
    Write-PkgLog "==================================================================="
    Write-PkgLog "Distribution Packager (Sala 2026-06-04)"
    Write-PkgLog "==================================================================="

    # $PSScriptRoot は [CmdletBinding()] 付きスクリプトの param 既定値評価時には
    # 空になる場合がある (-File 起動時に再現)。README 既定値は本文スコープで解決する。
    if ([string]::IsNullOrEmpty($ReadmePath)) {
        $ReadmePath = Join-Path $PSScriptRoot "はじめにお読みください.txt"
    }

    $InstallerPath = Resolve-InstallerPath -Hint $InstallerPath
    $meta = Get-ProductMetadata -Path $InstallerPath
    Write-PkgLog "Product : $($meta.ProductName)"
    Write-PkgLog "Version : $($meta.Version)"

    Test-AuthenticodeValid -Path $InstallerPath

    if ([string]::IsNullOrEmpty($OutputZip)) {
        $outDir = Join-Path $PSScriptRoot "output"
        $OutputZip = Join-Path $outDir ("{0}_v{1}.zip" -f $meta.ProductName, $meta.Version)
    }
    Write-PkgLog "OutputZip: $OutputZip"
    Write-PkgLog "Readme   : $ReadmePath"

    New-DistributionZip -ExePath $InstallerPath -Readme $ReadmePath -ZipPath $OutputZip

    $size = (Get-Item $OutputZip).Length
    Write-PkgLog "==================================================================="
    Write-PkgLog "Package ready: $OutputZip ($([math]::Round($size/1MB,2)) MB)"
    Write-PkgLog "==================================================================="
    exit 0
}

try {
    Main
} catch {
    Write-PkgLog "Unhandled exception: $($_.Exception.Message)" "ERROR"
    Write-PkgLog $_.ScriptStackTrace "ERROR"
    exit 99
}
