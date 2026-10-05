#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$InstallDir,
    [string]$DeepSeekApiKey,
    [string]$DeepSeekModel = "deepseek-flash",
    [switch]$SkipDeepSeek,
    [switch]$AddToPath,
    [switch]$InstallDesktop,
    [switch]$NoShortcut,
    [switch]$Batch,
    [string]$ConfigHome
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Write-Step {
    param([string]$Message)
    $line = "[" + (Get-Date -Format "HH:mm:ss") + "] " + $Message
    Write-Host $line
}

function Write-Error-Step {
    param([string]$Message)
    Write-Host "ERROR: $Message" -ForegroundColor Red
}

function Get-DefaultInstallDir {
    return Join-Path $env:LOCALAPPDATA "Programs\Codex"
}

function Get-Architecture {
    $arch = $env:PROCESSOR_ARCHITECTURE
    if ($arch -eq "AMD64") { return "x64" }
    if ($arch -eq "ARM64") { return "arm64" }
    throw "不支持的系统架构：$arch。本安装程序目前只支持 64 位 Windows。"
}

function Get-LatestNodeVersion {
    $indexUrl = "https://nodejs.org/dist/index.json"
    try {
        $index = Invoke-RestMethod -Uri $indexUrl -UseBasicParsing
    }
    catch {
        throw "无法获取 Node.js 版本列表：$($_.Exception.Message)"
    }

    $lts = $index | Where-Object { $_.lts -and $_.lts -isnot [bool] } | Select-Object -First 1
    if (-not $lts) {
        $lts = $index | Where-Object { $_.lts } | Select-Object -First 1
    }
    if (-not $lts) { throw "未找到可用的 Node.js LTS 版本。" }
    return $lts.version.TrimStart("v")
}

function Get-NodeDownloadUrl {
    param([string]$Version, [string]$Arch)
    if ($Arch -eq "x64") {
        return "https://nodejs.org/dist/v$Version/node-v$Version-win-x64.zip"
    }
    if ($Arch -eq "arm64") {
        return "https://nodejs.org/dist/v$Version/node-v$Version-win-arm64.zip"
    }
    throw "不支持的架构：$Arch"
}

function Download-File {
    param(
        [string]$Url,
        [string]$Destination
    )
    Write-Step "下载 $Url"
    try {
        Invoke-WebRequest -Uri $Url -OutFile $Destination -UseBasicParsing
    }
    catch {
        throw "下载失败：$($_.Exception.Message)"
    }
}

function Extract-NodeArchive {
    param(
        [string]$ArchivePath,
        [string]$DestinationRoot
    )
    Write-Step "解压 Node.js..."
    if (Test-Path -LiteralPath $DestinationRoot) {
        Remove-Item -LiteralPath $DestinationRoot -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $DestinationRoot | Out-Null

    try {
        [System.IO.Compression.ZipFile]::ExtractToDirectory($ArchivePath, $DestinationRoot)
    }
    catch {
        throw "解压失败：$($_.Exception.Message)"
    }
}

function Copy-DirectoryContents {
    param(
        [string]$Source,
        [string]$Destination
    )
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    Get-ChildItem -LiteralPath $Source -Force | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $Destination -Recurse -Force
    }
}

function Test-CommandAvailable {
    param([string]$CommandPath)
    return (Test-Path -LiteralPath $CommandPath)
}

function Add-ToUserPath {
    param([string]$Directory)
    $current = [Environment]::GetEnvironmentVariable("Path", "User")
    if (-not $current) { $current = "" }
    $parts = $current -split ";"
    if ($parts -notcontains $Directory) {
        $newPath = if ($current.TrimEnd(";")) { $current.TrimEnd(";") + ";" + $Directory } else { $Directory }
        [Environment]::SetEnvironmentVariable("Path", $newPath, "User")
        $env:Path = [Environment]::GetEnvironmentVariable("Path", "Machine") + ";" + [Environment]::GetEnvironmentVariable("Path", "User")
        Write-Step "已添加到当前用户 PATH：$Directory"
    }
    else {
        Write-Step "安装目录已在用户 PATH 中。"
    }
}

function Write-DeepSeekProfile {
    param(
        [string]$ApiKey,
        [string]$Model,
        [string]$CodexHome
    )

    if (-not $ApiKey) {
        throw "已选择接入 DeepSeek，但未填写 API Key。"
    }

    $profileFile = Join-Path $CodexHome "deepseek.config.toml"
    New-Item -ItemType Directory -Force -Path $CodexHome | Out-Null

    $content = @"
model = "$Model"
model_provider = "deepseek"

[model_providers.deepseek]
name = "DeepSeek"
base_url = "https://api.deepseek.com/"
wire_api = "responses"
requires_openai_auth = false
experimental_bearer_token = "$ApiKey"
"@

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($profileFile, $content, $utf8NoBom)
    Write-Step "已写入 DeepSeek 配置：$profileFile"
}

function Write-DeepSeekDefaultConfig {
    param(
        [string]$ApiKey,
        [string]$Model,
        [string]$CodexHome
    )

    if (-not $ApiKey) {
        throw "已选择桌面版并接入 DeepSeek，但未填写 API Key。"
    }

    $configFile = Join-Path $CodexHome "config.toml"
    New-Item -ItemType Directory -Force -Path $CodexHome | Out-Null

    if (Test-Path -LiteralPath $configFile) {
        $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
        $backup = "$configFile.backup-$stamp"
        Copy-Item -LiteralPath $configFile -Destination $backup -Force
        Write-Step "已备份原 Codex 配置：$backup"
    }

    $content = @"
model = "$Model"
model_provider = "deepseek"

[model_providers.deepseek]
name = "DeepSeek"
base_url = "https://api.deepseek.com/"
wire_api = "responses"
requires_openai_auth = false
experimental_bearer_token = "$ApiKey"
"@

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($configFile, $content, $utf8NoBom)
    Write-Step "已将 DeepSeek 设为默认模型：$configFile"
}

function Install-CodexDesktop {
    param([string]$TargetDir)

    $desktopLauncher = Join-Path $TargetDir "Codex-Desktop.cmd"
    $launcherContent = @"
@echo off
setlocal
set "CODEX_DIR=%~dp0"
set "PATH=%~dp0runtime\node;%PATH%"
call "%CODEX_DIR%app\codex.cmd" app
endlocal
"@
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($desktopLauncher, $launcherContent, $utf8NoBom)
    Write-Step "已创建桌面版启动器：$desktopLauncher"

    $winget = Get-Command winget.exe -ErrorAction SilentlyContinue
    if ($winget) {
        Write-Step "正在通过 winget 安装 Codex 桌面版..."
        try {
            $proc = Start-Process -FilePath $winget.Source `
                -ArgumentList "install --id 9PLM9XGG6VKS --source msstore --accept-package-agreements --accept-source-agreements --silent" `
                -Wait -PassThru
            if ($proc.ExitCode -eq 0) {
                Write-Step "Codex 桌面版安装完成或已经安装。"
            }
            else {
                Write-Step "winget 未能自动完成桌面版安装。已创建 Codex-Desktop 快捷方式，双击它会继续引导安装。"
            }
        }
        catch {
            Write-Step "winget 安装失败：$($_.Exception.Message)。已创建 Codex-Desktop 快捷方式，双击它会继续引导安装。"
        }
    }
    else {
        Write-Step "未检测到 winget。已创建 Codex-Desktop 快捷方式，双击它会打开 Codex 桌面版安装引导。"
    }
}

function New-Shortcut {
    param(
        [string]$ShortcutPath,
        [string]$TargetPath,
        [string]$Arguments,
        [string]$WorkingDirectory
    )
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($ShortcutPath)
    $shortcut.TargetPath = $TargetPath
    $shortcut.Arguments = $Arguments
    $shortcut.WorkingDirectory = $WorkingDirectory
    $shortcut.IconLocation = "$env:SystemRoot\System32\cmd.exe,0"
    $shortcut.Save()
}

function Show-InstallerGui {
    $form = New-Object System.Windows.Forms.Form
    $form.Text = "Codex 一键安装程序"
    $form.StartPosition = "CenterScreen"
    $form.ClientSize = New-Object System.Drawing.Size(620, 470)
    $form.FormBorderStyle = "FixedDialog"
    $form.MaximizeBox = $false
    $form.MinimizeBox = $false

    $lblTitle = New-Object System.Windows.Forms.Label
    $lblTitle.Text = "Codex + DeepSeek 便携安装"
    $lblTitle.Font = New-Object System.Drawing.Font("Microsoft YaHei", 13, [System.Drawing.FontStyle]::Bold)
    $lblTitle.Location = New-Object System.Drawing.Point(20, 16)
    $lblTitle.Size = New-Object System.Drawing.Size(580, 34)
    $form.Controls.Add($lblTitle)

    $lblPath = New-Object System.Windows.Forms.Label
    $lblPath.Text = "安装位置："
    $lblPath.Location = New-Object System.Drawing.Point(20, 70)
    $lblPath.Size = New-Object System.Drawing.Size(90, 24)
    $form.Controls.Add($lblPath)

    $txtPath = New-Object System.Windows.Forms.TextBox
    $txtPath.Text = Get-DefaultInstallDir
    $txtPath.Location = New-Object System.Drawing.Point(115, 68)
    $txtPath.Size = New-Object System.Drawing.Size(380, 24)
    $form.Controls.Add($txtPath)

    $btnBrowse = New-Object System.Windows.Forms.Button
    $btnBrowse.Text = "浏览..."
    $btnBrowse.Location = New-Object System.Drawing.Point(505, 66)
    $btnBrowse.Size = New-Object System.Drawing.Size(90, 30)
    $form.Controls.Add($btnBrowse)

    $btnBrowse.Add_Click({
        $dialog = New-Object System.Windows.Forms.FolderBrowserDialog
        $dialog.Description = "选择 Codex 安装位置"
        $dialog.SelectedPath = $txtPath.Text
        if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
            $txtPath.Text = $dialog.SelectedPath
        }
    })

    $chkDeepSeek = New-Object System.Windows.Forms.CheckBox
    $chkDeepSeek.Text = "接入 DeepSeek API（可选）"
    $chkDeepSeek.Location = New-Object System.Drawing.Point(20, 118)
    $chkDeepSeek.Size = New-Object System.Drawing.Size(240, 28)
    $chkDeepSeek.Checked = $true
    $form.Controls.Add($chkDeepSeek)

    $lblKey = New-Object System.Windows.Forms.Label
    $lblKey.Text = "API Key："
    $lblKey.Location = New-Object System.Drawing.Point(38, 156)
    $lblKey.Size = New-Object System.Drawing.Size(80, 24)
    $form.Controls.Add($lblKey)

    $txtKey = New-Object System.Windows.Forms.MaskedTextBox
    $txtKey.Location = New-Object System.Drawing.Point(125, 154)
    $txtKey.Size = New-Object System.Drawing.Size(340, 24)
    $txtKey.PasswordChar = "*"
    $form.Controls.Add($txtKey)

    $lblModel = New-Object System.Windows.Forms.Label
    $lblModel.Text = "模型："
    $lblModel.Location = New-Object System.Drawing.Point(38, 196)
    $lblModel.Size = New-Object System.Drawing.Size(80, 24)
    $form.Controls.Add($lblModel)

    $cboModel = New-Object System.Windows.Forms.ComboBox
    $cboModel.Location = New-Object System.Drawing.Point(125, 194)
    $cboModel.Size = New-Object System.Drawing.Size(340, 28)
    $cboModel.DropDownStyle = "DropDownList"
    $cboModel.Items.AddRange(@("deepseek-flash", "deepseek-v4-pro"))
    $cboModel.SelectedIndex = 0
    $form.Controls.Add($cboModel)

    $chkPath = New-Object System.Windows.Forms.CheckBox
    $chkPath.Text = "把安装目录加入当前用户 PATH（之后可在终端直接输入 codex）"
    $chkPath.Location = New-Object System.Drawing.Point(20, 238)
    $chkPath.Size = New-Object System.Drawing.Size(570, 28)
    $chkPath.Checked = $true
    $form.Controls.Add($chkPath)

    $chkShortcut = New-Object System.Windows.Forms.CheckBox
    $chkShortcut.Text = "在桌面创建快捷方式"
    $chkShortcut.Location = New-Object System.Drawing.Point(20, 270)
    $chkShortcut.Size = New-Object System.Drawing.Size(300, 28)
    $chkShortcut.Checked = $true
    $form.Controls.Add($chkShortcut)

    $chkDesktop = New-Object System.Windows.Forms.CheckBox
    $chkDesktop.Text = "同时安装 Codex 桌面版（图形界面）"
    $chkDesktop.Location = New-Object System.Drawing.Point(20, 300)
    $chkDesktop.Size = New-Object System.Drawing.Size(570, 28)
    $chkDesktop.Checked = $true
    $form.Controls.Add($chkDesktop)

    $lblNote = New-Object System.Windows.Forms.Label
    $lblNote.Text = "说明：安装器会下载 Node.js LTS，并安装 Codex CLI。若同时勾选桌面版和 DeepSeek，安装器会把 DeepSeek 设为默认模型，并自动备份已有的 config.toml。"
    $lblNote.Location = New-Object System.Drawing.Point(20, 338)
    $lblNote.Size = New-Object System.Drawing.Size(580, 58)
    $lblNote.ForeColor = [System.Drawing.Color]::Gray
    $form.Controls.Add($lblNote)

    $btnInstall = New-Object System.Windows.Forms.Button
    $btnInstall.Text = "开始安装"
    $btnInstall.Location = New-Object System.Drawing.Point(390, 416)
    $btnInstall.Size = New-Object System.Drawing.Size(100, 34)
    $form.Controls.Add($btnInstall)

    $btnCancel = New-Object System.Windows.Forms.Button
    $btnCancel.Text = "取消"
    $btnCancel.Location = New-Object System.Drawing.Point(500, 416)
    $btnCancel.Size = New-Object System.Drawing.Size(100, 34)
    $form.Controls.Add($btnCancel)

    $script:GuiInstallDir = $null
    $script:GuiUseDeepSeek = $false
    $script:GuiApiKey = ""
    $script:GuiModel = "deepseek-flash"
    $script:GuiAddPath = $false
    $script:GuiShortcut = $false
    $script:GuiDesktop = $false
    $script:GuiCancelled = $true

    $btnInstall.Add_Click({
        $path = $txtPath.Text.Trim()
        if (-not $path) {
            [System.Windows.Forms.MessageBox]::Show("请选择安装位置。", "提示", "OK", "Warning") | Out-Null
            return
        }
        if ($chkDeepSeek.Checked -and -not $txtKey.Text.Trim()) {
            [System.Windows.Forms.MessageBox]::Show("已勾选 DeepSeek，请填写 API Key。", "提示", "OK", "Warning") | Out-Null
            return
        }
        $script:GuiInstallDir = $path
        $script:GuiUseDeepSeek = $chkDeepSeek.Checked
        $script:GuiApiKey = $txtKey.Text.Trim()
        $script:GuiModel = $cboModel.SelectedItem.ToString()
        $script:GuiAddPath = $chkPath.Checked
        $script:GuiShortcut = $chkShortcut.Checked
        $script:GuiDesktop = $chkDesktop.Checked
        $script:GuiCancelled = $false
        $form.DialogResult = [System.Windows.Forms.DialogResult]::OK
        $form.Close()
    })

    $btnCancel.Add_Click({
        $script:GuiCancelled = $true
        $form.DialogResult = [System.Windows.Forms.DialogResult]::Cancel
        $form.Close()
    })

    $chkDeepSeek.Add_CheckedChanged({
        $enabled = $chkDeepSeek.Checked
        $txtKey.Enabled = $enabled
        $cboModel.Enabled = $enabled
    })
    $txtKey.Enabled = $chkDeepSeek.Checked
    $cboModel.Enabled = $chkDeepSeek.Checked

    [void]$form.ShowDialog()

    if ($script:GuiCancelled) {
        return $null
    }
    return [PSCustomObject]@{
        InstallDir = $script:GuiInstallDir
        UseDeepSeek = $script:GuiUseDeepSeek
        ApiKey = $script:GuiApiKey
        Model = $script:GuiModel
        AddPath = $script:GuiAddPath
        CreateShortcut = $script:GuiShortcut
        InstallDesktop = $script:GuiDesktop
    }
}

function Install-Codex {
    param(
        [string]$TargetDir,
        [bool]$UseDeepSeek,
        [string]$ApiKey,
        [string]$Model,
        [bool]$AddPath,
        [bool]$CreateShortcut,
        [bool]$InstallDesktop,
        [string]$CodexHome
    )

    $target = [System.IO.Path]::GetFullPath($TargetDir)
    $targetParent = [System.IO.Path]::GetPathRoot($target)
    if ($target -eq $targetParent) {
        throw "安装位置不能是磁盘根目录。"
    }

    Write-Step "安装目录：$target"
    Write-Step "检测网络并获取 Node.js LTS 版本..."
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

    $arch = Get-Architecture
    $nodeVersion = Get-LatestNodeVersion
    $nodeUrl = Get-NodeDownloadUrl -Version $nodeVersion -Arch $arch
    Write-Step "Node.js LTS：v$nodeVersion（$arch）"

    New-Item -ItemType Directory -Force -Path $target | Out-Null

    $nodeDir = Join-Path $target "runtime\node"
    $appPrefix = Join-Path $target "app"
    $nodeExe = Join-Path $nodeDir "node.exe"
    $npmCmd = Join-Path $nodeDir "npm.cmd"
    $codexCmd = Join-Path $appPrefix "codex.cmd"

    $env:Path = "$nodeDir;$env:Path"

    $hasLocalNode = Test-CommandAvailable $nodeExe
    if (-not $hasLocalNode) {
        $workRoot = Join-Path $env:TEMP "codex-portable-installer"
        New-Item -ItemType Directory -Force -Path $workRoot | Out-Null
        $zipPath = Join-Path $workRoot "node-v$nodeVersion-$arch.zip"
        $extractDir = Join-Path $workRoot "node-extract"

        Download-File -Url $nodeUrl -Destination $zipPath
        Extract-NodeArchive -ArchivePath $zipPath -DestinationRoot $extractDir

        $topFolder = Get-ChildItem -LiteralPath $extractDir -Directory | Select-Object -First 1
        if (-not $topFolder) {
            throw "解压后未找到 Node.js 目录。"
        }
        if (Test-Path -LiteralPath $nodeDir) {
            Remove-Item -LiteralPath $nodeDir -Recurse -Force
        }
        New-Item -ItemType Directory -Force -Path $nodeDir | Out-Null
        Copy-DirectoryContents -Source $topFolder.FullName -Destination $nodeDir
    }
    else {
        Write-Step "检测到安装目录内已有 Node.js，跳过下载。"
    }

    Write-Step "验证 Node.js..."
    & $nodeExe --version
    if ($LASTEXITCODE -ne 0) { throw "Node.js 校验失败。" }

    Write-Step "安装 Codex CLI（@openai/codex）..."
    New-Item -ItemType Directory -Force -Path $appPrefix | Out-Null
    & $npmCmd install -g "@openai/codex" --prefix $appPrefix --no-fund --no-audit
    if ($LASTEXITCODE -ne 0) { throw "Codex CLI 安装失败。" }

    if (-not (Test-CommandAvailable $codexCmd)) {
        throw "安装完成，但未找到 codex.cmd：$codexCmd"
    }
    Write-Step "验证 Codex CLI..."
    & $codexCmd --version
    if ($LASTEXITCODE -ne 0) { throw "Codex CLI 校验失败。" }

    $launcher = Join-Path $target "Codex.cmd"
    $launcherContent = @"
@echo off
setlocal
set "CODEX_DIR=%~dp0"
set "PATH=%~dp0runtime\node;%PATH%"
call "%CODEX_DIR%app\codex.cmd" %*
endlocal
"@
    [System.IO.File]::WriteAllText($launcher, $launcherContent, (New-Object System.Text.UTF8Encoding($false)))
    Write-Step "已创建启动器：$launcher"

    if ($UseDeepSeek) {
        Write-DeepSeekProfile -ApiKey $ApiKey -Model $Model -CodexHome $CodexHome
        $deepLauncher = Join-Path $target "Codex-DeepSeek.cmd"
        $deepLauncherContent = @"
@echo off
setlocal
set "CODEX_DIR=%~dp0"
set "PATH=%~dp0runtime\node;%PATH%"
call "%CODEX_DIR%app\codex.cmd" -p deepseek %*
endlocal
"@
        [System.IO.File]::WriteAllText($deepLauncher, $deepLauncherContent, (New-Object System.Text.UTF8Encoding($false)))
        Write-Step "已创建 DeepSeek 启动器：$deepLauncher"
    }

    if ($InstallDesktop) {
        if ($UseDeepSeek) {
            Write-DeepSeekDefaultConfig -ApiKey $ApiKey -Model $Model -CodexHome $CodexHome
        }
        Install-CodexDesktop -TargetDir $target
    }

    if ($AddPath) {
        Add-ToUserPath -Directory $target
    }

    if ($CreateShortcut) {
        $desktop = [Environment]::GetFolderPath("Desktop")
        New-Shortcut -ShortcutPath (Join-Path $desktop "Codex.lnk") `
            -TargetPath $env:ComSpec `
            -Arguments "/c `"`"$launcher`"`"" `
            -WorkingDirectory $target
        Write-Step "已创建桌面快捷方式：Codex"

        if ($UseDeepSeek) {
            $deepLauncherFull = Join-Path $target "Codex-DeepSeek.cmd"
            New-Shortcut -ShortcutPath (Join-Path $desktop "Codex-DeepSeek.lnk") `
                -TargetPath $env:ComSpec `
                -Arguments "/c `"`"$deepLauncherFull`"`"" `
                -WorkingDirectory $target
            Write-Step "已创建桌面快捷方式：Codex-DeepSeek"
        }

        if ($InstallDesktop) {
            $desktopLauncherFull = Join-Path $target "Codex-Desktop.cmd"
            New-Shortcut -ShortcutPath (Join-Path $desktop "Codex-Desktop.lnk") `
                -TargetPath $env:ComSpec `
                -Arguments "/c `"`"$desktopLauncherFull`"`"" `
                -WorkingDirectory $target
            Write-Step "已创建桌面快捷方式：Codex-Desktop"
        }
    }
}

function Invoke-Installer {
    $useDeepSeek = $false
    $apiKey = ""
    $model = $DeepSeekModel
    $addPath = [bool]$AddToPath
    $createShortcut = -not [bool]$NoShortcut
    $installDesktop = [bool]$InstallDesktop
    $codexHome = if ($ConfigHome) { [System.IO.Path]::GetFullPath($ConfigHome) } else { Join-Path $env:USERPROFILE ".codex" }

    if (-not $InstallDir) {
        if ($Batch) {
            throw "批处理模式需要指定 -InstallDir。"
        }
        $gui = Show-InstallerGui
        if (-not $gui) {
            Write-Step "用户已取消。"
            exit 0
        }
        $InstallDir = $gui.InstallDir
        $useDeepSeek = $gui.UseDeepSeek -and (-not [bool]$SkipDeepSeek)
        $apiKey = $gui.ApiKey
        $model = $gui.Model
        $addPath = $gui.AddPath
        $createShortcut = $gui.CreateShortcut
        $installDesktop = $gui.InstallDesktop
    }
    else {
        $useDeepSeek = (-not [bool]$SkipDeepSeek) -and [bool]$DeepSeekApiKey
        $apiKey = $DeepSeekApiKey
    }

    if ($useDeepSeek -and -not $apiKey) {
        throw "已启用 DeepSeek，但没有 API Key。"
    }

    Install-Codex -TargetDir $InstallDir `
        -UseDeepSeek $useDeepSeek `
        -ApiKey $apiKey `
        -Model $model `
        -AddPath $addPath `
        -CreateShortcut $createShortcut `
        -InstallDesktop $installDesktop `
        -CodexHome $codexHome

    Write-Step ""
    Write-Step "安装完成。"
    Write-Step "启动方式："
    Write-Step "  1. 双击桌面快捷方式；"
    if ($useDeepSeek) {
        Write-Step "  2. 使用 DeepSeek 模型请双击 Codex-DeepSeek；"
    }
    Write-Step "  3. 或在终端运行：codex"
    if ($useDeepSeek) {
        Write-Step "  4. DeepSeek 模式终端命令：codex -p deepseek"
    }
    if ($installDesktop) {
        Write-Step "  5. 图形界面请双击 Codex-Desktop；首次运行会继续引导安装桌面版。"
    }
}

try {
    Invoke-Installer
}
catch {
    Write-Error-Step $_.Exception.Message
    if (-not $Batch) {
        [System.Windows.Forms.MessageBox]::Show($_.Exception.Message, "安装失败", "OK", "Error") | Out-Null
    }
    exit 1
}
