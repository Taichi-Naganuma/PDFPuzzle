<#
.SYNOPSIS
    Inno Setup 等で生成された EXE インストーラに対して SSL.com EV
    コードサイニング証明書 (eSigner cloud HSM) で署名する正式版スクリプト。
    証明書ファイル (.pfx) を持っていない時期は -DryRun で空運転可能。

.DESCRIPTION
    SSL.com Sole Proprietor EV Code Signing Certificate と eSigner クラウド HSM
    を前提とする。EV 証明書では物理 USB トークンは存在せず、署名は SSL.com 公式の
    CodeSignTool.bat (Java ベース CLI) 経由で行うのが推奨経路。
    https://www.ssl.com/guide/esigner-codesigntool-command-guide/

    本ファイルは sign-installer.ps1.template から派生した正式版 (Sala 完成版・2026-06-04)。
    template の TODO(Sala): マーカー (Invoke-CodeSignTool / Test-Signature /
    Resolve-SigntoolPath 候補探索) はすべて解消済み。

.PARAMETER InstallerPath
    署名対象の EXE インストーラへの絶対パス (例: ...\installer\output\PDFPuzzle_Setup_v1.0.0.exe)

.PARAMETER DryRun
    指定すると CodeSignTool / signtool への実呼出を行わず、引数組立結果のみ出力。
    証明書未到着期間中、または CI 環境で機密が無い場合の空運転テスト用。

.PARAMETER UseESigner
    SSL.com eSigner CodeSignTool 経由で署名する (EV 推奨経路)。
    デフォルト $true。$false にすると signtool.exe 直呼びパス
    (本スクリプトでは未対応。SSL.com EV は CodeSignTool 必須)。

.PARAMETER TimestampUrl
    タイムスタンプサーバ URL。デフォルト http://ts.ssl.com。
    CodeSignTool は内部でタイムスタンプを付与する。タイムスタンプなしの署名は
    EV 期限切れ後に検証失敗するため、本パラメータは必ず指定される設計。

.PARAMETER CodeSignToolPath
    SSL.com CodeSignTool.bat の絶対パス。
    既定は $env:ProgramFiles\SSL.com\CodeSignTool\CodeSignTool.bat。
    インストール場所が異なる場合は引数指定。

.PARAMETER SigntoolPath
    Windows SDK 付属 signtool.exe の絶対パス (署名検証用)。
    未指定時は where signtool → Windows SDK 既定パス探索で自動解決を試みる。

.PARAMETER OutputDir
    署名後 EXE の出力ディレクトリ。既定は InstallerPath と同じディレクトリ。
    既定では -override 相当の上書き動作になる。

.EXAMPLE
    # 証明書未到着期 / CI の空運転テスト
    .\sign-installer.ps1 -InstallerPath "C:\...\PDFPuzzle_Setup_v1.0.0.exe" -DryRun

.EXAMPLE
    # 証明書到着後の実署名 (John / オーナーが ESIGNER_* を投入して実施)
    $env:ESIGNER_USERNAME      = "owner@example.com"
    $env:ESIGNER_PASSWORD      = "..."
    $env:ESIGNER_TOTP_SECRET   = "..."
    $env:ESIGNER_CREDENTIAL_ID = "..."
    .\sign-installer.ps1 -InstallerPath "C:\...\PDFPuzzle_Setup_v1.0.0.exe"

.NOTES
    機密情報 (ESIGNER_USERNAME / ESIGNER_PASSWORD / ESIGNER_TOTP_SECRET /
    ESIGNER_CREDENTIAL_ID) はスクリプト内に絶対に書かない。
    すべて環境変数経由で受ける。ログ出力時は password / totp_secret / credential_id を
    "****" でマスクすること。RawArgs (生引数) は決して Write-Host しない。

    本スクリプトは PowerShell 5.1 (Windows 既定) で動作する。
    PowerShell 7 専用機能 (??, ?. 等) は使わない。

    対象製品の固有名 (PDFPuzzle / ImagePuzzle / ExcelPuzzle) はスクリプト内に
    書かない。すべて InstallerPath から派生させる。

    終了コード:
      0  成功 (DryRun 完走を含む)
      1  実署名に必要な ESIGNER_* 環境変数が不足
      2  InstallerPath 不正 (存在しない / .exe でない)
      3  CodeSignTool.bat が見つからない
      4  signtool.exe が見つからない
      5  署名検証に失敗
      6  -UseESigner:$false は非対応
      7  CodeSignTool が非ゼロ終了 (署名失敗)
      99 未捕捉例外
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$InstallerPath,

    [switch]$DryRun,

    [bool]$UseESigner = $true,

    [string]$TimestampUrl = "http://ts.ssl.com",

    [string]$CodeSignToolPath = (Join-Path $env:ProgramFiles "SSL.com\CodeSignTool\CodeSignTool.bat"),

    [string]$SigntoolPath = "",

    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"

# ============================================================
# Section 1: Logging helpers
# ============================================================

function Write-SignLog {
    param([string]$Message, [string]$Level = "INFO")
    $ts = (Get-Date).ToString("HH:mm:ss")
    Write-Host "[sign $ts $Level] $Message"
}

function Write-SignError {
    param([string]$Message)
    Write-SignLog -Message $Message -Level "ERROR"
}

function Mask-Secret {
    param([string]$Value)
    if ([string]::IsNullOrEmpty($Value)) { return "<empty>" }
    return "****"
}

# ============================================================
# Section 2: Pre-flight validation
# ============================================================

function Test-InstallerPath {
    param([string]$Path)
    if (-not (Test-Path -Path $Path -PathType Leaf)) {
        Write-SignError "InstallerPath not found: $Path"
        exit 2
    }
    $ext = [System.IO.Path]::GetExtension($Path)
    if ($ext -ne ".exe") {
        Write-SignError "InstallerPath must be a .exe file. Got: $ext"
        exit 2
    }
    Write-SignLog "Installer file exists: $Path"
}

function Get-ProductMetadata {
    <#
    InstallerPath のファイル名から ProductName と Version を抽出する。
    想定パターン: <ProductName>_Setup_v<Version>.exe
    例: PDFPuzzle_Setup_v1.0.0.exe -> ProductName=PDFPuzzle, Version=1.0.0
    パターン不一致時は ProductName=ファイル名 stem, Version=unknown を返す。
    #>
    param([string]$Path)
    $stem = [System.IO.Path]::GetFileNameWithoutExtension($Path)
    $match = [regex]::Match($stem, '^(?<name>.+?)_Setup_v(?<ver>[0-9]+\.[0-9]+\.[0-9]+(?:\.[0-9]+)?)$')
    if ($match.Success) {
        return @{
            ProductName = $match.Groups['name'].Value
            Version     = $match.Groups['ver'].Value
        }
    }
    Write-SignLog "Filename does not match <Product>_Setup_v<Version>.exe pattern. Falling back to stem." "WARN"
    return @{
        ProductName = $stem
        Version     = "unknown"
    }
}

# ============================================================
# Section 3: Environment variable resolution
# ============================================================

function Get-ESignerCredentials {
    <#
    Real-sign 時は4変数すべてが必須。未設定は exit 1。
    DryRun 時は未設定でも継続するが、set/unset を表示。
    #>
    param([switch]$RequireAll)

    $creds = @{
        Username     = $env:ESIGNER_USERNAME
        Password     = $env:ESIGNER_PASSWORD
        TotpSecret   = $env:ESIGNER_TOTP_SECRET
        CredentialId = $env:ESIGNER_CREDENTIAL_ID
    }

    $missing = @()
    foreach ($k in $creds.Keys) {
        $set = -not [string]::IsNullOrWhiteSpace($creds[$k])
        $status = if ($set) { "set" } else { "unset" }
        Write-SignLog "  env ESIGNER_$($k.ToUpper()): $status"
        if (-not $set) { $missing += $k }
    }

    if ($RequireAll -and $missing.Count -gt 0) {
        Write-SignError "Missing required environment variables for real-sign: $($missing -join ', ')"
        Write-SignError "Set ESIGNER_USERNAME / ESIGNER_PASSWORD / ESIGNER_TOTP_SECRET / ESIGNER_CREDENTIAL_ID and rerun."
        exit 1
    }

    return $creds
}

# ============================================================
# Section 4: CodeSignTool command assembly
# ============================================================

function Build-CodeSignToolArgs {
    <#
    SSL.com CodeSignTool.bat sign サブコマンドの引数列を組み立てる。
    公式リファレンス: https://www.ssl.com/guide/esigner-codesigntool-command-guide/

    返り値: ハッシュテーブル
      - Raw  : 実呼出用引数配列 (パスワード等が生で含まれる)
      - Safe : ログ出力用引数配列 (パスワード等が **** マスク)
    #>
    param(
        [string]$InputFile,
        [string]$OutputDirectory,
        [hashtable]$Credentials
    )

    $rawArgs = @(
        "sign",
        "-input_file_path=$InputFile",
        "-output_dir_path=$OutputDirectory",
        "-credential_id=$($Credentials.CredentialId)",
        "-username=$($Credentials.Username)",
        "-password=$($Credentials.Password)",
        "-totp_secret=$($Credentials.TotpSecret)",
        "-override"
    )

    $safeArgs = @(
        "sign",
        "-input_file_path=$InputFile",
        "-output_dir_path=$OutputDirectory",
        "-credential_id=$(Mask-Secret $Credentials.CredentialId)",
        "-username=$($Credentials.Username)",
        "-password=$(Mask-Secret $Credentials.Password)",
        "-totp_secret=$(Mask-Secret $Credentials.TotpSecret)",
        "-override"
    )

    return @{
        Raw  = $rawArgs
        Safe = $safeArgs
    }
}

# ============================================================
# Section 5: Signing execution
# ============================================================

function Invoke-CodeSignTool {
    param(
        [string]$ToolPath,
        [string[]]$RawArgs,
        [string[]]$SafeArgs
    )

    if (-not (Test-Path $ToolPath -PathType Leaf)) {
        Write-SignError "CodeSignTool.bat not found at: $ToolPath"
        Write-SignError "Install SSL.com CodeSignTool or specify -CodeSignToolPath."
        exit 3
    }

    Write-SignLog "Invoking CodeSignTool.bat (args masked):"
    Write-SignLog "  $ToolPath $($SafeArgs -join ' ')"

    # 実呼出。RawArgs (password/totp/credential_id を含む) は決してログに出さない。
    # CodeSignTool 自身の標準出力はそのままコンソールへ流す (機密はエコーしない設計)。
    & "$ToolPath" @RawArgs
    $exitCode = $LASTEXITCODE

    if ($null -eq $exitCode) {
        # & がプロセスを起動できなかった等の異常系
        Write-SignError "CodeSignTool did not return an exit code. Invocation may have failed."
        exit 7
    }
    if ($exitCode -ne 0) {
        Write-SignError "CodeSignTool exited with code $exitCode (signing failed)."
        exit 7
    }

    Write-SignLog "CodeSignTool reported success (exit 0)."
}

# ============================================================
# Section 6: Signature verification (post-sign)
# ============================================================

function Resolve-SigntoolPath {
    param([string]$Hint)
    if (-not [string]::IsNullOrEmpty($Hint)) {
        if (Test-Path $Hint -PathType Leaf) { return $Hint }
        Write-SignError "Specified SigntoolPath not found: $Hint"
        exit 4
    }

    # 1) where signtool で PATH 上を解決
    $found = $null
    try {
        $whereOutput = & where.exe signtool 2>$null
        if ($LASTEXITCODE -eq 0 -and $whereOutput) {
            $found = ($whereOutput -split "`r?`n")[0].Trim()
        }
    } catch { }
    if ($found -and (Test-Path $found -PathType Leaf)) {
        Write-SignLog "Resolved signtool via PATH: $found"
        return $found
    }

    # 2) Windows SDK 既定パス配下を新しいバージョン優先で探索
    #    例: C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe
    $sdkRoots = @(
        (Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"),
        (Join-Path $env:ProgramFiles "Windows Kits\10\bin"),
        (Join-Path ${env:ProgramFiles(x86)} "Windows Kits\8.1\bin"),
        (Join-Path $env:ProgramFiles "Windows Kits\8.1\bin")
    )
    foreach ($root in $sdkRoots) {
        if ([string]::IsNullOrEmpty($root)) { continue }
        if (-not (Test-Path $root)) { continue }
        $candidates = Get-ChildItem -Path $root -Filter "signtool.exe" -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '\\x64\\signtool\.exe$' } |
            Sort-Object FullName -Descending
        if ($candidates -and @($candidates).Count -gt 0) {
            $picked = @($candidates)[0].FullName
            Write-SignLog "Resolved signtool via Windows SDK: $picked"
            return $picked
        }
    }

    Write-SignError "signtool.exe not found in PATH or Windows SDK. Install Windows SDK or specify -SigntoolPath."
    exit 4
}

function Test-Signature {
    param([string]$Path, [string]$Signtool)

    Write-SignLog "Verifying signature: signtool verify /pa /v `"$Path`""

    $output = & "$Signtool" verify /pa /v "$Path" 2>&1
    $exitCode = $LASTEXITCODE

    foreach ($line in $output) { Write-SignLog "  $line" }

    if ($exitCode -ne 0) {
        Write-SignError "signtool verify failed (exit $exitCode). Signature is NOT valid."
        exit 5
    }

    $joined = ($output | Out-String)
    if ($joined -notmatch "Successfully verified") {
        Write-SignError "signtool returned 0 but 'Successfully verified' not found in output. Treating as failure."
        exit 5
    }

    Write-SignLog "Signature verified successfully."
}

# ============================================================
# Section 7: Main flow
# ============================================================

function Main {
    Write-SignLog "==================================================================="
    Write-SignLog "Code Signing Script (production version / Sala 2026-06-04)"
    Write-SignLog "Mode: $(if ($DryRun) { 'DryRun' } else { 'real-sign' })"
    Write-SignLog "InstallerPath: $InstallerPath"
    Write-SignLog "==================================================================="

    # Step 1: validate installer
    Test-InstallerPath -Path $InstallerPath

    # Step 2: derive metadata
    $meta = Get-ProductMetadata -Path $InstallerPath
    Write-SignLog "Product : $($meta.ProductName)"
    Write-SignLog "Version : $($meta.Version)"

    # Step 3: determine output dir
    if ([string]::IsNullOrEmpty($OutputDir)) {
        $OutputDir = [System.IO.Path]::GetDirectoryName($InstallerPath)
    }
    Write-SignLog "OutputDir: $OutputDir"

    # Step 4: env vars
    Write-SignLog "Checking ESIGNER_* environment variables..."
    $creds = Get-ESignerCredentials -RequireAll:(-not $DryRun)

    # Step 5: assemble args
    $built = Build-CodeSignToolArgs -InputFile $InstallerPath -OutputDirectory $OutputDir -Credentials $creds

    Write-SignLog "CodeSignTool.bat path: $CodeSignToolPath"
    Write-SignLog "CodeSignTool args (masked):"
    foreach ($a in $built.Safe) { Write-SignLog "  $a" }

    # Step 6: DryRun branch
    if ($DryRun) {
        Write-SignLog "TimestampUrl: $TimestampUrl"
        Write-SignLog "UseESigner  : $UseESigner"
        Write-SignLog ""
        Write-SignLog "DryRun complete. No actual signing performed."
        Write-SignLog "Next step: set ESIGNER_* environment variables and rerun without -DryRun."
        exit 0
    }

    # Step 7: real-sign
    if (-not $UseESigner) {
        Write-SignError "-UseESigner:`$false (signtool.exe direct path) is not supported."
        Write-SignError "SSL.com EV requires CodeSignTool. Keep -UseESigner `$true."
        exit 6
    }

    Invoke-CodeSignTool -ToolPath $CodeSignToolPath -RawArgs $built.Raw -SafeArgs $built.Safe

    # Step 8: verify
    $signtool = Resolve-SigntoolPath -Hint $SigntoolPath
    Test-Signature -Path $InstallerPath -Signtool $signtool

    Write-SignLog "==================================================================="
    Write-SignLog "Signing + verification complete for $($meta.ProductName) v$($meta.Version)"
    Write-SignLog "==================================================================="
    exit 0
}

# ============================================================
# Entry point
# ============================================================

try {
    Main
} catch {
    Write-SignError "Unhandled exception: $($_.Exception.Message)"
    Write-SignError $_.ScriptStackTrace
    exit 99
}
