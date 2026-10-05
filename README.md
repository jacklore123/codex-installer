# Codex 一键安装程序（Windows）

一个 Windows 版 **Codex 中文图形安装器**：不用提前装 Node.js，不需要管理员权限，
选个目录点「开始安装」，就能得到一个可用的 Codex。

## 它做了什么

- 自动下载并解压 Node.js LTS；
- 把 `@openai/codex` 安装到你选择的目录；
- 可选接入 DeepSeek API；
- 可选安装 Codex 桌面版（图形界面）；
- 可选强制 Codex 使用简体中文回复（写入 `%USERPROFILE%\.codex\AGENTS.md`）；
- 不改动你已有的 Codex 配置。

## 仓库文件

| 文件 | 说明 |
| --- | --- |
| `CodexSetup.exe` | 图形安装器，双击即用 |
| `CodexInstaller.cs` | 安装器源码（C#） |
| `install-codex.ps1` | PowerShell 安装脚本 |
| `安装Codex.bat` | 双击运行的批处理入口 |
| `Codex安装程序.zip` | 打包好的安装器压缩包 |
| `使用说明.md` | 完整说明文档 |

## 快速开始

1. 双击 `CodexSetup.exe`。
2. 选择安装位置（默认 `%LOCALAPPDATA%\Programs\Codex`）。
3. 需要 DeepSeek 就勾选并粘贴 API Key，选择模型。
4. 点「开始安装」，等待完成。

完整说明见 [使用说明.md](使用说明.md)。

## 适用环境

Windows 10 / 11（x64 或 arm64），能访问 `nodejs.org`、`npmjs.com`、`api.deepseek.com`。

## 许可

MIT License，见 [LICENSE](LICENSE)。
