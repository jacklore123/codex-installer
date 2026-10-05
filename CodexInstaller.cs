using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace CodexInstaller
{
    internal sealed class InstallOptions
    {
        public string InstallDir;
        public bool UseDeepSeek;
        public string DeepSeekKey;
        public string DeepSeekModel;
        public bool InstallDesktop;
        public bool AddToPath;
        public bool CreateShortcut;
        public bool ForceChinese;
    }

    public sealed class MainForm : Form
    {
        private TextBox pathBox;
        private CheckBox deepSeekBox;
        private MaskedTextBox keyBox;
        private ComboBox modelBox;
        private CheckBox desktopBox;
        private CheckBox pathCheck;
        private CheckBox shortcutCheck;
        private CheckBox chineseCheck;
        private Button installButton;
        private Label statusLabel;
        private ProgressBar progressBar;
        private BackgroundWorker worker;

        public MainForm()
        {
            BuildUi();
            worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.WorkerSupportsCancellation = false;
            worker.DoWork += Worker_DoWork;
            worker.ProgressChanged += Worker_ProgressChanged;
            worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
        }

        private void BuildUi()
        {
            Text = "Codex 中文图形安装程序";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(660, 520);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = new Font("Microsoft YaHei", 9F);

            Label title = new Label();
            title.Text = "Codex + DeepSeek 一键安装";
            title.Font = new Font("Microsoft YaHei", 14F, FontStyle.Bold);
            title.Location = new Point(20, 16);
            title.Size = new Size(620, 34);
            Controls.Add(title);

            Label pathLabel = new Label();
            pathLabel.Text = "安装位置：";
            pathLabel.Location = new Point(20, 72);
            pathLabel.Size = new Size(90, 25);
            Controls.Add(pathLabel);

            pathBox = new TextBox();
            pathBox.Text = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Codex");
            pathBox.Location = new Point(115, 70);
            pathBox.Size = new Size(420, 25);
            Controls.Add(pathBox);

            Button browseButton = new Button();
            browseButton.Text = "浏览...";
            browseButton.Location = new Point(545, 68);
            browseButton.Size = new Size(90, 30);
            browseButton.Click += delegate
            {
                using (FolderBrowserDialog dlg = new FolderBrowserDialog())
                {
                    dlg.Description = "选择 Codex 安装位置";
                    dlg.SelectedPath = pathBox.Text;
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        pathBox.Text = dlg.SelectedPath;
                    }
                }
            };
            Controls.Add(browseButton);

            deepSeekBox = new CheckBox();
            deepSeekBox.Text = "接入 DeepSeek API（可选）";
            deepSeekBox.Location = new Point(20, 120);
            deepSeekBox.Size = new Size(280, 28);
            deepSeekBox.Checked = true;
            Controls.Add(deepSeekBox);

            Label keyLabel = new Label();
            keyLabel.Text = "API Key：";
            keyLabel.Location = new Point(40, 158);
            keyLabel.Size = new Size(80, 25);
            Controls.Add(keyLabel);

            keyBox = new MaskedTextBox();
            keyBox.Location = new Point(125, 156);
            keyBox.Size = new Size(410, 25);
            keyBox.PasswordChar = '*';
            Controls.Add(keyBox);

            Label modelLabel = new Label();
            modelLabel.Text = "模型：";
            modelLabel.Location = new Point(40, 198);
            modelLabel.Size = new Size(80, 25);
            Controls.Add(modelLabel);

            modelBox = new ComboBox();
            modelBox.Location = new Point(125, 196);
            modelBox.Size = new Size(410, 28);
            modelBox.DropDownStyle = ComboBoxStyle.DropDownList;
            modelBox.Items.AddRange(new object[] { "deepseek-flash", "deepseek-v4-pro" });
            modelBox.SelectedIndex = 0;
            Controls.Add(modelBox);

            pathCheck = new CheckBox();
            pathCheck.Text = "把安装目录加入当前用户 PATH";
            pathCheck.Location = new Point(20, 240);
            pathCheck.Size = new Size(600, 28);
            pathCheck.Checked = true;
            Controls.Add(pathCheck);

            shortcutCheck = new CheckBox();
            shortcutCheck.Text = "在桌面创建快捷方式";
            shortcutCheck.Location = new Point(20, 270);
            shortcutCheck.Size = new Size(600, 28);
            shortcutCheck.Checked = true;
            Controls.Add(shortcutCheck);

            desktopBox = new CheckBox();
            desktopBox.Text = "同时安装 Codex 桌面版（图形界面）";
            desktopBox.Location = new Point(20, 300);
            desktopBox.Size = new Size(600, 28);
            desktopBox.Checked = true;
            Controls.Add(desktopBox);

            chineseCheck = new CheckBox();
            chineseCheck.Text = "强制 Codex 使用简体中文回复";
            chineseCheck.Location = new Point(20, 330);
            chineseCheck.Size = new Size(600, 28);
            chineseCheck.Checked = true;
            Controls.Add(chineseCheck);

            statusLabel = new Label();
            statusLabel.Text = "准备就绪。";
            statusLabel.Location = new Point(20, 380);
            statusLabel.Size = new Size(620, 30);
            statusLabel.ForeColor = Color.Gray;
            Controls.Add(statusLabel);

            progressBar = new ProgressBar();
            progressBar.Location = new Point(20, 414);
            progressBar.Size = new Size(620, 22);
            progressBar.Minimum = 0;
            progressBar.Maximum = 100;
            Controls.Add(progressBar);

            installButton = new Button();
            installButton.Text = "开始安装";
            installButton.Location = new Point(430, 462);
            installButton.Size = new Size(100, 34);
            installButton.Click += InstallButton_Click;
            Controls.Add(installButton);

            Button cancelButton = new Button();
            cancelButton.Text = "退出";
            cancelButton.Location = new Point(540, 462);
            cancelButton.Size = new Size(100, 34);
            cancelButton.Click += delegate { Close(); };
            Controls.Add(cancelButton);

            deepSeekBox.CheckedChanged += delegate
            {
                bool enabled = deepSeekBox.Checked;
                keyBox.Enabled = enabled;
                modelBox.Enabled = enabled;
            };
            keyBox.Enabled = deepSeekBox.Checked;
            modelBox.Enabled = deepSeekBox.Checked;
        }

        private void InstallButton_Click(object sender, EventArgs e)
        {
            string path = pathBox.Text.Trim();
            if (path.Length == 0)
            {
                MessageBox.Show(this, "请选择安装位置。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (deepSeekBox.Checked && keyBox.Text.Trim().Length == 0)
            {
                MessageBox.Show(this, "已勾选 DeepSeek，请填写 API Key。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            InstallOptions options = new InstallOptions();
            options.InstallDir = path;
            options.UseDeepSeek = deepSeekBox.Checked;
            options.DeepSeekKey = keyBox.Text.Trim();
            options.DeepSeekModel = modelBox.SelectedItem.ToString();
            options.InstallDesktop = desktopBox.Checked;
            options.AddToPath = pathCheck.Checked;
            options.CreateShortcut = shortcutCheck.Checked;
            options.ForceChinese = chineseCheck.Checked;

            SetBusy(true);
            worker.RunWorkerAsync(options);
        }

        private void SetBusy(bool busy)
        {
            installButton.Enabled = !busy;
            pathBox.Enabled = !busy;
            deepSeekBox.Enabled = !busy;
            keyBox.Enabled = !busy && deepSeekBox.Checked;
            modelBox.Enabled = !busy && deepSeekBox.Checked;
            desktopBox.Enabled = !busy;
            pathCheck.Enabled = !busy;
            shortcutCheck.Enabled = !busy;
            chineseCheck.Enabled = !busy;
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker bw = (BackgroundWorker)sender;
            InstallOptions options = (InstallOptions)e.Argument;
            try
            {
                Installer.Run(options, bw);
            }
            catch (Exception ex)
            {
                e.Result = ex;
            }
        }

        private void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (e.UserState != null)
            {
                statusLabel.Text = e.UserState.ToString();
            }
            progressBar.Value = Math.Min(100, Math.Max(0, e.ProgressPercentage));
        }

        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            SetBusy(false);
            progressBar.Value = 100;
            if (e.Result is Exception)
            {
                Exception ex = (Exception)e.Result;
                statusLabel.Text = "安装失败：" + ex.Message;
                MessageBox.Show(this, ex.Message, "安装失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            statusLabel.Text = "安装完成。";
            MessageBox.Show(this, "安装完成。\n\n桌面快捷方式：\n- Codex：命令行模式\n- Codex-DeepSeek：DeepSeek 模式\n- Codex-Desktop：图形界面", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        [STAThread]
        public static void Main(string[] args)
        {
            if (args != null && args.Length > 0 && string.Equals(args[0], "--selftest", StringComparison.OrdinalIgnoreCase))
            {
                RunSelfTest();
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        private static void RunSelfTest()
        {
            string logPath = Path.Combine(Path.GetTempPath(), "codex-native-selftest.log");
            File.WriteAllText(logPath, "START\r\n", new UTF8Encoding(false));
            InstallOptions options = new InstallOptions();
            options.InstallDir = Path.Combine(Path.GetTempPath(), "codex-native-selftest");
            options.UseDeepSeek = false;
            options.DeepSeekKey = "";
            options.DeepSeekModel = "deepseek-flash";
            options.InstallDesktop = false;
            options.AddToPath = false;
            options.CreateShortcut = false;
            options.ForceChinese = false;

            BackgroundWorker bw = new BackgroundWorker();
            bw.WorkerReportsProgress = true;
            ManualResetEvent done = new ManualResetEvent(false);
            bw.DoWork += delegate(object sender, DoWorkEventArgs e)
            {
                try
                {
                    Installer.Run(options, bw);
                    e.Result = null;
                }
                catch (Exception ex)
                {
                    e.Result = ex;
                }
            };
            bw.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                try
                {
                    if (e.Result is Exception)
                    {
                        File.AppendAllText(logPath, "ERROR: " + ((Exception)e.Result).ToString() + "\r\n", new UTF8Encoding(false));
                    }
                    else
                    {
                        File.AppendAllText(logPath, "OK\r\n", new UTF8Encoding(false));
                    }
                }
                finally
                {
                    done.Set();
                }
            };
            bw.RunWorkerAsync();
            done.WaitOne();
        }
    }

    internal static class Installer
    {
        public static void Run(InstallOptions options, BackgroundWorker worker)
        {
            Report(worker, 2, "正在准备安装...");
            string target = Path.GetFullPath(options.InstallDir);
            string root = Path.GetPathRoot(target);
            if (string.Equals(target.TrimEnd('\\', '/'), root.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception("安装位置不能是磁盘根目录。");
            }
            Directory.CreateDirectory(target);

            string arch = GetArchitecture();
            string nodeVersion = GetLatestNodeVersion();
            string nodeUrl = "https://nodejs.org/dist/v" + nodeVersion + "/node-v" + nodeVersion + "-win-" + arch + ".zip";

            string nodeDir = Path.Combine(target, "runtime", "node");
            string appPrefix = Path.Combine(target, "app");
            string nodeExe = Path.Combine(nodeDir, "node.exe");
            string npmCmd = Path.Combine(nodeDir, "npm.cmd");
            string codexCmd = Path.Combine(appPrefix, "codex.cmd");

            if (!File.Exists(nodeExe))
            {
                Report(worker, 5, "正在获取 Node.js LTS 版本...");
                string workRoot = Path.Combine(Path.GetTempPath(), "codex-native-installer");
                Directory.CreateDirectory(workRoot);
                string zipPath = Path.Combine(workRoot, "node-v" + nodeVersion + "-" + arch + ".zip");
                string extractDir = Path.Combine(workRoot, "node-extract");

                Report(worker, 10, "正在下载 Node.js v" + nodeVersion + "...");
                using (WebClient wc = new WebClient())
                {
                    wc.DownloadFile(nodeUrl, zipPath);
                }

                Report(worker, 40, "正在解压 Node.js...");
                if (Directory.Exists(extractDir))
                {
                    Directory.Delete(extractDir, true);
                }
                ZipFile.ExtractToDirectory(zipPath, extractDir);
                string[] dirs = Directory.GetDirectories(extractDir);
                if (dirs.Length == 0)
                {
                    throw new Exception("解压后未找到 Node.js 目录。");
                }
                if (Directory.Exists(nodeDir))
                {
                    Directory.Delete(nodeDir, true);
                }
                CopyDirectory(dirs[0], nodeDir);
            }

            string pathBackup = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("Path", nodeDir + ";" + pathBackup, EnvironmentVariableTarget.Process);

            Report(worker, 55, "正在验证 Node.js...");
            RunProcess(nodeExe, "--version", nodeDir);

            Report(worker, 62, "正在安装 Codex CLI...");
            Directory.CreateDirectory(appPrefix);
            string npmInstall = "\"" + npmCmd + "\" install -g @openai/codex --prefix \"" + appPrefix + "\" --no-fund --no-audit";
            RunRawCmd(npmInstall, nodeDir);

            if (!File.Exists(codexCmd))
            {
                throw new Exception("安装完成，但未找到 codex.cmd：" + codexCmd);
            }
            Report(worker, 72, "正在校验 Codex CLI...");
            RunRawCmd("\"" + codexCmd + "\" --version", appPrefix);

            Report(worker, 76, "正在创建启动器...");
            string launcher = Path.Combine(target, "Codex.cmd");
            string launcherContent =
                "@echo off\r\n" +
                "setlocal\r\n" +
                "set \"CODEX_DIR=%~dp0\"\r\n" +
                "set \"PATH=%~dp0runtime\\node;%PATH%\"\r\n" +
                "call \"%CODEX_DIR%app\\codex.cmd\" %*\r\n" +
                "endlocal\r\n";
            WriteText(launcher, launcherContent);

            string codexHome = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".codex");

            if (options.UseDeepSeek)
            {
                Report(worker, 78, "正在写入 DeepSeek 配置...");
                string profile = Path.Combine(codexHome, "deepseek.config.toml");
                Directory.CreateDirectory(codexHome);
                WriteText(profile, BuildDeepSeekConfig(options.DeepSeekModel, options.DeepSeekKey, false));

                string deepLauncher = Path.Combine(target, "Codex-DeepSeek.cmd");
                string deepContent =
                    "@echo off\r\n" +
                    "setlocal\r\n" +
                    "set \"CODEX_DIR=%~dp0\"\r\n" +
                    "set \"PATH=%~dp0runtime\\node;%PATH%\"\r\n" +
                    "call \"%CODEX_DIR%app\\codex.cmd\" -p deepseek %*\r\n" +
                    "endlocal\r\n";
                WriteText(deepLauncher, deepContent);
            }

            if (options.ForceChinese)
            {
                Report(worker, 80, "正在设置简体中文回复...");
                Directory.CreateDirectory(codexHome);
                string agentsFile = Path.Combine(codexHome, "AGENTS.md");
                WriteText(agentsFile,
                    "Always respond in Simplified Chinese.\r\n" +
                    "除非用户明确要求其他语言，否则始终使用简体中文回复；代码注释优先中文，同时保留关键英文术语。\r\n");
            }

            if (options.InstallDesktop)
            {
                Report(worker, 82, "正在配置 Codex 桌面版...");
                if (options.UseDeepSeek)
                {
                    WriteDefaultDeepSeekConfig(codexHome, options.DeepSeekModel, options.DeepSeekKey);
                }
                string desktopLauncher = Path.Combine(target, "Codex-Desktop.cmd");
                string desktopContent =
                    "@echo off\r\n" +
                    "setlocal\r\n" +
                    "set \"CODEX_DIR=%~dp0\"\r\n" +
                    "set \"PATH=%~dp0runtime\\node;%PATH%\"\r\n" +
                    "call \"%CODEX_DIR%app\\codex.cmd\" app\r\n" +
                    "endlocal\r\n";
                WriteText(desktopLauncher, desktopContent);
                TryInstallDesktopApp();
            }

            if (options.AddToPath)
            {
                Report(worker, 88, "正在更新用户 PATH...");
                AddToUserPath(target);
            }

            if (options.CreateShortcut)
            {
                Report(worker, 92, "正在创建桌面快捷方式...");
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                CreateShortcut(Path.Combine(desktop, "Codex.lnk"), launcher, target);
                if (options.UseDeepSeek)
                {
                    CreateShortcut(Path.Combine(desktop, "Codex-DeepSeek.lnk"),
                        Path.Combine(target, "Codex-DeepSeek.cmd"), target);
                }
                if (options.InstallDesktop)
                {
                    CreateShortcut(Path.Combine(desktop, "Codex-Desktop.lnk"),
                        Path.Combine(target, "Codex-Desktop.cmd"), target);
                }
            }

            Report(worker, 100, "安装完成。");
        }

        private static void Report(BackgroundWorker worker, int percent, string message)
        {
            worker.ReportProgress(percent, message);
        }

        private static string GetArchitecture()
        {
            string arch = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE");
            if (string.Equals(arch, "AMD64", StringComparison.OrdinalIgnoreCase))
            {
                return "x64";
            }
            if (string.Equals(arch, "ARM64", StringComparison.OrdinalIgnoreCase))
            {
                return "arm64";
            }
            throw new Exception("不支持的系统架构：" + arch + "。本安装程序只支持 64 位 Windows。");
        }

        private static string GetLatestNodeVersion()
        {
            try
            {
                using (WebClient wc = new WebClient())
                {
                    wc.Encoding = Encoding.UTF8;
                    string json = wc.DownloadString("https://nodejs.org/dist/index.json");
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    object deserialized = serializer.DeserializeObject(json);
                    object[] array = deserialized as object[];
                    if (array != null)
                    {
                        foreach (object item in array)
                        {
                            Dictionary<string, object> entry = item as Dictionary<string, object>;
                            if (entry == null)
                            {
                                continue;
                            }
                            object versionObj;
                            object ltsObj;
                            if (entry.TryGetValue("version", out versionObj) &&
                                entry.TryGetValue("lts", out ltsObj) &&
                                ltsObj is string)
                            {
                                return versionObj.ToString().TrimStart('v');
                            }
                        }
                    }
                }
            }
            catch
            {
                // fall back to a known Node.js LTS if the index endpoint cannot be read.
            }
            return "24.21.0";
        }

        private static void RunProcess(string fileName, string arguments, string workingDirectory)
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = fileName;
            psi.Arguments = arguments;
            psi.WorkingDirectory = workingDirectory;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            using (Process process = Process.Start(psi))
            {
                StringBuilder errorBuilder = new StringBuilder();
                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    // Drain stdout so the child process never blocks on a full pipe.
                };
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data != null)
                    {
                        errorBuilder.AppendLine(e.Data);
                    }
                };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
                process.CancelOutputRead();
                process.CancelErrorRead();
                if (process.ExitCode != 0)
                {
                    string err = errorBuilder.ToString();
                    throw new Exception("命令执行失败：" + fileName + " " + arguments + (err.Length > 0 ? "\r\n" + err : ""));
                }
            }
        }

        private static void RunRawCmd(string commandLine, string workingDirectory)
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "cmd.exe";
            psi.Arguments = "/c \"" + commandLine + "\"";
            psi.WorkingDirectory = workingDirectory;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            using (Process process = Process.Start(psi))
            {
                StringBuilder errorBuilder = new StringBuilder();
                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    // Drain stdout so the child process never blocks on a full pipe.
                };
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data != null)
                    {
                        errorBuilder.AppendLine(e.Data);
                    }
                };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
                process.CancelOutputRead();
                process.CancelErrorRead();
                    if (process.ExitCode != 0)
                    {
                        string err = errorBuilder.ToString();
                        throw new Exception("命令执行失败：" + commandLine + (err.Length > 0 ? "\r\n" + err : ""));
                    }
            }
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source))
            {
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
            }
            foreach (string directory in Directory.GetDirectories(source))
            {
                CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
            }
        }

        private static void WriteText(string path, string content)
        {
            File.WriteAllText(path, content, new UTF8Encoding(false));
        }

        private static string BuildDeepSeekConfig(string model, string apiKey, bool includeTopLevel)
        {
            StringBuilder sb = new StringBuilder();
            if (includeTopLevel)
            {
                sb.AppendLine("model = \"" + model + "\"");
                sb.AppendLine("model_provider = \"deepseek\"");
                sb.AppendLine();
            }
            sb.AppendLine("[model_providers.deepseek]");
            sb.AppendLine("name = \"DeepSeek\"");
            sb.AppendLine("base_url = \"https://api.deepseek.com/\"");
            sb.AppendLine("wire_api = \"responses\"");
            sb.AppendLine("requires_openai_auth = false");
            sb.AppendLine("experimental_bearer_token = \"" + apiKey + "\"");
            return sb.ToString();
        }

        private static void WriteDefaultDeepSeekConfig(string codexHome, string model, string apiKey)
        {
            Directory.CreateDirectory(codexHome);
            string configFile = Path.Combine(codexHome, "config.toml");
            if (File.Exists(configFile))
            {
                string backup = configFile + ".backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                File.Copy(configFile, backup, true);
            }
            WriteText(configFile, BuildDeepSeekConfig(model, apiKey, true));
        }

        private static void AddToUserPath(string directory)
        {
            string current = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? "";
            string[] parts = current.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                if (string.Equals(part.Trim(), directory.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
            string newPath = current.TrimEnd(';');
            if (newPath.Length > 0)
            {
                newPath += ";";
            }
            newPath += directory;
            Environment.SetEnvironmentVariable("Path", newPath, EnvironmentVariableTarget.User);
        }

        private static void CreateShortcut(string shortcutPath, string targetFile, string workingDirectory)
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
            {
                return;
            }
            object shell = Activator.CreateInstance(shellType);
            object shortcut = shellType.InvokeMember(
                "CreateShortcut",
                BindingFlags.InvokeMethod,
                null,
                shell,
                new object[] { shortcutPath });

            Type shortcutType = shortcut.GetType();
            shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { "cmd.exe" });
            shortcutType.InvokeMember("Arguments", BindingFlags.SetProperty, null, shortcut,
                new object[] { "/c \"\"" + targetFile + "\"\"" });
            shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut,
                new object[] { workingDirectory });
            shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut,
                new object[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe") + ",0" });
            shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        }

        private static void TryInstallDesktopApp()
        {
            string winget = FindOnPath("winget.exe");
            if (winget == null)
            {
                return;
            }
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = winget;
                psi.Arguments = "install --id 9PLM9XGG6VKS --source msstore --accept-package-agreements --accept-source-agreements --silent";
                psi.UseShellExecute = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                using (Process process = Process.Start(psi))
                {
                    if (!process.WaitForExit(120000))
                    {
                        try { process.Kill(); } catch { }
                    }
                }
            }
            catch
            {
                // The Codex-Desktop shortcut will continue to guide installation.
            }
        }

        private static string FindOnPath(string fileName)
        {
            string path = Environment.GetEnvironmentVariable("Path") ?? "";
            foreach (string directory in path.Split(';'))
            {
                if (directory.Length == 0)
                {
                    continue;
                }
                string candidate = Path.Combine(directory, fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            return null;
        }
    }
}
