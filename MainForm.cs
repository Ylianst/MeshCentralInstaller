/*
Copyright 2009-2021 Intel Corporation

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Reflection;
using System.Diagnostics;
using System.Collections;
using System.Windows.Forms;
using System.ServiceProcess;
using System.Security.Principal;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace MeshCentralInstaller
{
    public partial class MainForm : Form
    {
        private Thread workerThread = null;
        private int currentPanel = 0;
        private MeshDiscovery scanner = null;
        private DateTime refreshTime = DateTime.Now;
        private Version currentNodeVersion = null;
        private string windowsTempPath = Path.GetTempPath();
        private bool logging = false;
        private string g_optionalModules = "node-windows@0.1.14 loadavg-windows@1.1.1";

        // NodeJS version
        public const string nodeVersion = "22.19.0";

        // 64 bit
        public const string nodeUrl64 = "https://nodejs.org/dist/v" + nodeVersion + "/node-v" + nodeVersion + "-x64.msi";
        public const string nodeFile64 = "node-v" + nodeVersion + "-x64.msi";
        public const string nodeHash64 = "e1139a6b8b14fe4aaabd1462bf4978cde68dbf62f44e7a94cdfdfabcaeb735ba28395c304428d3ac42a11f2670398899";

        // 32 bit
        public const string nodeUrl32 = "https://nodejs.org/dist/v" + nodeVersion + "/node-v" + nodeVersion + "-x86.msi";
        public const string nodeFile32 = "node-v" + nodeVersion + "-x86.msi";
        public const string nodeHash32 = "dc02ef6bffb5abad2ef2da0d0888b53ab8ccaa3f4a38b7d4f16f3cb05b65567ab15353cf24f7acb65b708650cde657db";

        // Installation Settings
        string[] args;
        string ServerInstallPath = null;
        int ServerMode = 0;
        //bool ServerSelfUpdate = false;
        bool ServerMultiUser = true;
        string ServerName = null;
        System.Drawing.Color windowColor;
        bool ServerAlreadyInstalled = false;
        string onlineVersion = null;
        ServiceController MeshCentralServiceController = new ServiceController("MeshCentral.exe");

        public MainForm(string[] args)
        {
            this.args = args;
            InitializeComponent();
            Translate.TranslateControl(this);
            mainPanel.Controls.Add(panel1);
            mainPanel.Controls.Add(panel2);
            mainPanel.Controls.Add(panel3);
            mainPanel.Controls.Add(panel4);
            mainPanel.Controls.Add(panel6);
            mainPanel.Controls.Add(panel7);
            mainPanel.Controls.Add(panel8);
            mainPanel.Controls.Add(panel9);
            mainPanel.Controls.Add(panel10);
            pictureBox1.SendToBack();
            Version version = Assembly.GetEntryAssembly().GetName().Version;
            versionLabel.Text = "v" + version.Major + "." + version.Minor + "." + version.Build;

            foreach (string arg in this.args)
            {
                if (arg.ToLower() == "-log") { logging = true; }
            }
            logNoTime("MeshCentral Installer started at " + DateTime.Now.ToString());
        }

        public void logNoTime(string msg)
        {
            if (logging == true) { try { File.AppendAllText("log.txt", msg + "\r\n"); } catch (Exception) { } }
        }

        public void log(string msg)
        {
            if (logging == true) { try { File.AppendAllText("log.txt", DateTime.Now.TimeOfDay.ToString() + ": " + msg + "\r\n"); } catch (Exception) { } }
        }

        private delegate void displayDebugMessageHandler(string msg);
        private void displayDebugMessage(string msg)
        {
            if (this.InvokeRequired) { this.Invoke(new displayDebugMessageHandler(displayDebugMessage), msg); return; }
            debugTextBox.AppendText(msg);
            log("Debug: " + msg);
        }

        private void PerformNodeHashing()
        {
            WebClient Client1 = new WebClient();
            Client1.DownloadFileCompleted += Client_DownloadFileCompletedNodeHash64;
            Client1.DownloadFileAsync(new Uri(MainForm.nodeUrl64), MainForm.nodeFile64);
            WebClient Client2 = new WebClient();
            Client2.DownloadFileCompleted += Client_DownloadFileCompletedNodeHash32;
            Client2.DownloadFileAsync(new Uri(MainForm.nodeUrl32), MainForm.nodeFile32);
        }

        private void Client_DownloadFileCompletedNodeHash64(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            if (e.Error == null) { displayDebugMessage("public const string nodeHash64 = \"" + MainForm.CalculateSHA384(MainForm.nodeFile64) + "\";\r\n"); } else { displayDebugMessage("hash64: ERROR\r\n"); }
        }

        private void Client_DownloadFileCompletedNodeHash32(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            if (e.Error == null) { displayDebugMessage("public const string nodeHash32 = \"" + MainForm.CalculateSHA384(MainForm.nodeFile32) + "\";\r\n"); } else { displayDebugMessage("hash32: ERROR\r\n"); }
        }

        private void setPanel(int newPanel)
        {
            log("setPanel " + newPanel);
            if (currentPanel == newPanel) return;
            if (newPanel == 4) { updatePanel4(); }
            panel1.Visible = (newPanel == 1);
            panel2.Visible = (newPanel == 2);
            panel3.Visible = (newPanel == 3);
            panel4.Visible = (newPanel == 4);
            panel6.Visible = (newPanel == 6);
            panel7.Visible = (newPanel == 7);
            panel8.Visible = (newPanel == 8);
            panel9.Visible = (newPanel == 9);
            panel10.Visible = (newPanel == 10);
            currentPanel = newPanel;

            // Start the multicast scanner
            if ((newPanel == 8) && (scanner == null))
            {
                scanner = new MeshDiscovery();
                scanner.OnNotify += Scanner_OnNotify;
                scanner.MulticastPing();
            }
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            if (args.Length > 0 && args[0].ToLower() == "-nodehash") {
                setPanel(10);
                PerformNodeHashing();
                return;
            }

            //Text += " - v" + Application.ProductVersion;
            installPathTextBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Open Source", "MeshCentral");
            serverModeComboBox.SelectedIndex = 0;
            windowColor = serverNameTextBox.BackColor;
            SetStartPanel();
        }

        private void SetStartPanel()
        {
            log("SetStartPanel()");

            // Check if MeshCentral2 is already installed
            string existingPath = null;
            RegistryKey myKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Open Source\\MeshCentral2", true);
            if (myKey != null)
            {
                try { existingPath = (string)myKey.GetValue("InstallPath", null); } catch (Exception) { }
                try { serverNameTextBox.Text = (string)myKey.GetValue("ServerName"); } catch (Exception) { }
                try { multiUserCheckBox.Checked = ((int)myKey.GetValue("ServerMultiUser") == 1); } catch (Exception) { }
                //try { autoUpdateCheckBox.Checked = ((int)myKey.GetValue("ServerSelfUpdate") == 1); } catch (Exception) { }
                try { serverModeComboBox.SelectedIndex = (int)myKey.GetValue("ServerMode"); } catch (Exception) { }
                myKey.Close();
            }
            if ((existingPath != null) && (Directory.Exists(existingPath) == false)) { existingPath = null; } // Registry points to bad path, MeshCentral is not installed.
            if (existingPath != null)
            {
                log("MeshCentral looks to be already installed.");
                ServerAlreadyInstalled = true;
                installPathTextBox.Text = ServerInstallPath = existingPath;
                setPanel(4);
            }
            else
            {
                log("MeshCentral does not look to be installed.");
                setPanel(1);
            }
        }

        private ServiceControllerStatus GetMeshCentralServiceState()
        {
            ServiceController sc = new ServiceController("meshcentral");
            sc.Refresh();
            return sc.Status;
        }

        private void updatePanel4()
        {
            log("updatePanel4()");

            //ServerState s = readServerStateEx(installPathTextBox.Text);
            //if (s.state == ServerStateEnum.Running) { label7.Text = "MeshCentral is running this computer."; }
            //else if (s.state == ServerStateEnum.Unknown) { label7.Text = "MeshCentral is installed on this computer."; }

            MeshCentralServiceController.Refresh();
            ServiceControllerStatus st = 0;
            try { st = MeshCentralServiceController.Status; } catch (Exception) { }
            if (st == 0) {
                label7.Text = "MeshCentral is not installed on this computer.";
            } else {
                switch (st)
                {
                    case ServiceControllerStatus.ContinuePending:
                        label7.Text = "MeshCentral is in continue pending state on this computer.";
                        break;
                    case ServiceControllerStatus.Paused:
                        label7.Text = "MeshCentral is paused on this computer.";
                        break;
                    case ServiceControllerStatus.PausePending:
                        label7.Text = "MeshCentral is in pause pending state on this computer.";
                        break;
                    case ServiceControllerStatus.Running:
                        label7.Text = "MeshCentral is running on this computer.";
                        break;
                    case ServiceControllerStatus.StartPending:
                        label7.Text = "MeshCentral is in start pending state on this computer.";
                        break;
                    case ServiceControllerStatus.Stopped:
                        label7.Text = "MeshCentral is stopped on this computer.";
                        break;
                    case ServiceControllerStatus.StopPending:
                        label7.Text = "MeshCentral is in stopped pending state on this computer.";
                        break;
                    default:
                        label7.Text = "MeshCentral is in an unknown state (" + st.ToString() + ") on this computer.";
                        break;
                }
            }
            log(label7.Text);

            ServerState s = readServerStateEx(installPathTextBox.Text);
            if (s.url != null) {
                linkLabel2.Text = s.url;
                linkLabel2.Visible = true;
            } else {
                linkLabel2.Visible = false;
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            log("User exit selected.");
            Application.Exit();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
        }

        private delegate void displayMessageHandler(string msg, int buttons, string extra, int progress);
        private void displayMessage(string msg, int buttons = 0, string extra = "", int progress = 0)
        {
            if (this.InvokeRequired) { this.Invoke(new displayMessageHandler(displayMessage), msg, buttons, extra, progress); return; }
            string progressStr = "";
            if (progress != 0) { progressStr = ", " + progressStr + "%"; }
            if (extra != "") {
                log("displayMessage: " + msg + ", extra: " + extra + progressStr);
            } else {
                log("displayMessage: " + msg + progressStr);
            }
            if (msg != null) { statusLabel.Text = msg; loadingLabel.Text = msg; }
            if ((msg != null) || (msg != "Installation Completed.")) {
                statusLabel2.Text = extra;
                label4.Text = extra;
            }
            nextButton3.Enabled = (buttons == 1);
            backButton3.Enabled = (buttons == 2);
            mainProgressBar.Visible = (progress > 0);
            if (progress >= 0) { mainProgressBar.Value = progress; }
            if (buttons == 3) { setPanel(6); }
            linkLabel1.Visible = (progress == -1);
            advConfigButton.Visible = (progress == -1);
        }

        private delegate void displayVersionsHandler(string currentVersion, string availableVersion);
        private void displayVersions(string currentVersion, string availableVersion)
        {
            log("displayVersions, current: " + currentVersion + ", available: " + availableVersion);
            if (this.InvokeRequired) { this.Invoke(new displayVersionsHandler(displayVersions), currentVersion, availableVersion); return; }
            currentVersionLabel.Text = currentVersion;
            availableVersionLabel.Text = availableVersion;
            setPanel(7);
            button4.Enabled = (currentVersion != availableVersion);
            if (currentVersion != availableVersion) {
                label8.Text = "MeshCentral update is available, press next to update.";
            } else {
                label8.Text = "MeshCentral is up-to-date, no action needed.";
            }
            log("displayVersions: " + label8.Text);
        }

        private delegate void setLinkHandler(string link);
        private void setLink(string link)
        {
            if (this.InvokeRequired) { this.Invoke(new setLinkHandler(setLink), link); return; }
            log("setLink: " + link);
            if (link != null) {
                linkLabel1.Text = link;
                linkLabel1.Visible = true;
            } else {
                linkLabel1.Visible = false;
            }
        }

        // Global install variables
        string nodeUrl = null;
        string nodeFile = null;
        string nodeHash = null;

        public Version GetCurrentNodeVersion()
        {
            // Look in the registry
            try
            {
                using (var localKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                {
                    var verStr = localKey?.OpenSubKey(@"Software\Node.js", false)?.GetValue("Version") as string;
                    if (!string.IsNullOrEmpty(verStr)) { return new Version(verStr); }
                }
            }
            catch (Exception) { }

            // Try to run "Node -v"
            Process xprocess = new Process();
            xprocess.StartInfo.CreateNoWindow = true;
            xprocess.StartInfo.UseShellExecute = false;
            xprocess.StartInfo.RedirectStandardError = true;
            xprocess.StartInfo.RedirectStandardOutput = true;
            xprocess.StartInfo.RedirectStandardInput = true;
            xprocess.StartInfo.FileName = "node";
            xprocess.StartInfo.Arguments = "-v";
            log("Launching " + xprocess.StartInfo.FileName + " " + xprocess.StartInfo.Arguments);
            var xstartSuccess = false;
            try { xstartSuccess = xprocess.Start(); } catch (Exception) { }
            if (xstartSuccess == false) { displayMessage("Unable to run node.exe -v (1)", 2); workerThread = null; return null; }
            allOutput = "";
            xprocess.BeginOutputReadLine();
            xprocess.BeginErrorReadLine();
            xprocess.OutputDataReceived += Process_OutputDataReceived;
            xprocess.ErrorDataReceived += Process_ErrorDataReceived;
            if (xprocess.WaitForExit(15000) == false) { try { xprocess.Kill(); } catch (Exception) { } displayMessage("Unable to run node.exe -v (2)", 2); workerThread = null; return null; }
            if (allOutput.Length == 0) { displayMessage("Unable to run node.exe -v (3)", 2); workerThread = null; return null; }
            string[] allOutputSplit = allOutput.Replace("\r\n", "\r").Split('\r');
            if ((allOutputSplit.Length == 2) && (allOutputSplit[1] == "") && (allOutputSplit[0][0] == 'v')) { try { return new Version(allOutputSplit[0].Substring(1)); } catch (Exception) { } }
            return null;
        }

        public string GetCurrentNodeInstallPath()
        {
            try
            {
                using (var localKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                {
                    return localKey?.OpenSubKey(@"Software\Node.js", false)?.GetValue("InstallPath") as string;
                }
            }
            catch (Exception) { return null; }
        }

        public string GetCurrentNodeVersion2()
        {
            string ver = null;
            string uninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
            using (RegistryKey rk = Registry.LocalMachine.OpenSubKey(uninstallKey))
            {
                foreach (string skName in rk.GetSubKeyNames())
                {
                    using (RegistryKey sk = rk.OpenSubKey(skName))
                    {
                        try
                        {
                            string name = (string)sk.GetValue("DisplayName");
                            if (name == "Node.js")
                            {
                                string publisher = (string)sk.GetValue("Publisher");
                                if (publisher == "Node.js Foundation")
                                {
                                    ver = (string)sk.GetValue("DisplayVersion");
                                }
                            }
                        }
                        catch (Exception) { }
                    }
                }
            }
            return ver;
        }

        private void performInstallation()
        {
            try
            {
                // Check if NodeJS is installed
                displayMessage("Checking NodeJS installation...");
                currentNodeVersion = GetCurrentNodeVersion();
                if (currentNodeVersion != null) { log("currentNodeVersion: " + currentNodeVersion); } else { log("currentNodeVersion: none"); }

                // Check if current version is to old
                if ((currentNodeVersion != null) && (currentNodeVersion < new Version("16.0.0"))) { displayMessage("Installed NodeJS is too old.", 2, "Uninstall the currently installed NodeJS and try again."); return; }

                // Check if we need to install NodeJS
                if (currentNodeVersion != null) {
                    log("Skipping NodeJS installation.");
                    performInstallation3(); // Skip NodeJS installation
                } else {
                    // NodeJS is not installed, check what OS we are on
                    if (Environment.Is64BitOperatingSystem) {
                        // 64bit OS
                        log("Setting NodeJS 64bit.");
                        nodeUrl = nodeUrl64;
                        nodeFile = nodeFile64;
                        nodeHash = nodeHash64;
                    } else {
                        // 32bit OS
                        log("Setting NodeJS 32bit.");
                        nodeUrl = nodeUrl32;
                        nodeFile = nodeFile32;
                        nodeHash = nodeHash32;
                    }

                    // Figure out the full NodeJS MSI path
                    string nodeJsFullPath = Path.Combine(windowsTempPath, nodeFile);

                    // Check if we already have the NodeJS MSI downloaded
                    if ((File.Exists(nodeJsFullPath) == true) && (CalculateSHA384(nodeJsFullPath) == nodeHash)) {
                        // We have it alreadys, use that.
                        log("We already have the NodeJS install file, use that.");
                        performInstallation2();
                    } else {
                        // We don't have it, download it.
                        log("Downloading NodeJS from: " + nodeUrl);
                        WebClient Client = new WebClient();
                        Client.DownloadProgressChanged += Client_DownloadProgressChanged;
                        Client.DownloadFileCompleted += Client_DownloadFileCompleted;
                        Client.DownloadFileAsync(new Uri(nodeUrl), nodeJsFullPath);
                    }
                }
            }
            catch (Exception e) { displayMessage("Exception Error", 2, e.ToString()); }
            workerThread = null;
        }

        private void Client_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            string nodeJsFullPath = Path.Combine(windowsTempPath, nodeFile);
            if (e.Error != null)
            {
                log("NodeJS download error: " + e.ToString());
                try { if (File.Exists(nodeJsFullPath)) { File.Delete(nodeJsFullPath); } } catch (Exception) { }
                displayMessage("Download Error, please install NodeJS manually.", 2, e.Error.Message);
                if (e.Error != null) {
                    if (e.Error.Message != null) { log("Exception Message: " + e.Error.Message); }
                    if (e.Error.Source != null) { log("Exception Source: " + e.Error.Source); }
                    if (e.Error.StackTrace != null) { log("Exception StackTrace: " + e.Error.StackTrace); }
                    if (e.Error.InnerException != null)
                    {
                        if (e.Error.InnerException.Message != null) { log("InnerException Message: " + e.Error.InnerException.Message); }
                        if (e.Error.InnerException.Source != null) { log("InnerException Source: " + e.Error.InnerException.Source); }
                        if (e.Error.InnerException.StackTrace != null) { log("InnerException StackTrace: " + e.Error.InnerException.StackTrace); }
                    }
                }
                workerThread = null;
            }
            else
            {
                // Download of NodeJS compelted, check the hash
                displayMessage("Checking NodeJS hash...", 0, "", 0);
                string filehash = CalculateSHA384(nodeJsFullPath);
                log("NodeJS file hash: " + filehash);
                log("NodeJS expected hash: " + nodeHash);
                if (filehash == nodeHash)
                {
                    // File is good, use it
                    log("NodeJS file is good.");
                    performInstallation2();
                }
                else
                {
                    // Hash failed
                    displayMessage("NodeJS download hash failed.", 2);
                    workerThread = null;
                }
            }
        }

        private void Client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            // Called when we are making progress downloading the NodeJS MSI installer
            displayMessage("Downloading NodeJS...", 0, "", e.ProgressPercentage);
        }

        private void performInstallation2()
        {
            try
            {
                displayMessage("Installing NodeJS...", 0);

                string nodeJsFullPath = Path.Combine(windowsTempPath, nodeFile);

                // Perform the installation
                Process process = new Process();
                process.StartInfo.FileName = "cmd.exe";
                process.StartInfo.Arguments = "/c start /wait " + nodeJsFullPath + " /quiet"; // This does not work if there is a space in the path.
                process.StartInfo.Verb = "runas";
                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                log("Launching: " + process.StartInfo.FileName + " " + process.StartInfo.Arguments);
                var startSuccess = false;
                try { startSuccess = process.Start(); } catch (Exception) { }
                if (startSuccess == false) { displayMessage("Unable to install NodeJS.", 2); workerThread = null; return; }
                while (process.HasExited == false) { System.Threading.Thread.Sleep(100); }

                // Check that the installation worked
                currentNodeVersion = GetCurrentNodeVersion();
                if (currentNodeVersion != null) {
                    displayMessage("Completing NodeJS installation...", 0);
                    Thread.Sleep(10000);

                    displayMessage("NodeJS installed.", 0);
                    try { if (File.Exists(nodeJsFullPath)) { File.Delete(nodeJsFullPath); } } catch (Exception) { }

                    // Add node to the path
                    string nodeInstallPath = GetCurrentNodeInstallPath();
                    if (nodeInstallPath != null)
                    {
                        bool foundNodePath = false;
                        string[] paths = System.Environment.GetEnvironmentVariable("PATH").Split(';');
                        for (int i = 0; i < paths.Length; i++) { if ((paths[i].ToLower() == nodeInstallPath.ToLower())) { foundNodePath = true; } }
                        if (foundNodePath == false)
                        {
                            System.Environment.SetEnvironmentVariable("PATH", System.Environment.GetEnvironmentVariable("PATH") + ";" + nodeInstallPath);
                        }
                    }

                    performInstallation3();
                } else {
                    displayMessage("Unable to install NodeJS.", 2);
                    workerThread = null;
                }
            }
            catch (Exception e) { displayMessage("Exception Error", 2, e.ToString()); }
        }

        private void performInstallation3()
        {
            displayMessage("Checking MeshCentral installation...", 0);
            DirectoryInfo dir = new DirectoryInfo(ServerInstallPath);
            if (Directory.Exists(dir.FullName) == false) {
                DirectorySecurity xrestrictedPermissions = null;
                try
                {
                    // Setup special folder restricted permissions
                    xrestrictedPermissions = new DirectorySecurity(ServerInstallPath, AccessControlSections.All);
                    xrestrictedPermissions.SetAccessRuleProtection(true, false);
                    xrestrictedPermissions.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().Name, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.NoPropagateInherit, AccessControlType.Allow));
                    xrestrictedPermissions.AddAccessRule(new FileSystemAccessRule("SYSTEM", FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.NoPropagateInherit, AccessControlType.Allow));
                }
                catch (Exception) { }

                try
                {
                    Directory.CreateDirectory(dir.FullName, xrestrictedPermissions);
                }
                catch (Exception)
                {
                    Directory.CreateDirectory(dir.FullName);
                }
            }

            // Install firewall rules
            displayMessage("Firewall Setup...", 0, "Adding firewall rules.");
            if (ServerMode == 1) { FirewallSetup.SetupFirewall("80,443,4433", null); } // WAN mode
            else { FirewallSetup.SetupFirewall("80,443,4433", "16990"); } // LAN or Hybrid mode

            // Configure the HTTP proxy if needed
            Uri proxyUri = Win32Api.GetProxy(new Uri(nodeUrl64));
            if (proxyUri != null)
            {
                // Setup the NPM HTTP proxy
                displayMessage("Configuring NPM...", 0, "Setting up HTTP proxy.");
                Process xprocess = new Process();
                xprocess.StartInfo.CreateNoWindow = true;
                xprocess.StartInfo.UseShellExecute = false;
                xprocess.StartInfo.RedirectStandardError = true;
                xprocess.StartInfo.RedirectStandardOutput = true;
                xprocess.StartInfo.RedirectStandardInput = true;
                xprocess.StartInfo.FileName = @"C:\Program Files\nodejs\npm.cmd";
                xprocess.StartInfo.Arguments = "config set proxy " + proxyUri.ToString();
                xprocess.StartInfo.WorkingDirectory = @"C:\Program Files\nodejs";
                log("Launching: " + xprocess.StartInfo.FileName + " " + xprocess.StartInfo.Arguments);
                var xstartSuccess = false;
                try { xstartSuccess = xprocess.Start(); } catch (Exception) { }
                if (xstartSuccess == false) { displayMessage("Unable to set NPM HTTP proxy (#1).", 2); workerThread = null; return; }
                allOutput = "";
                xprocess.BeginOutputReadLine();
                xprocess.BeginErrorReadLine();
                xprocess.OutputDataReceived += Process_OutputDataReceived;
                xprocess.ErrorDataReceived += Process_ErrorDataReceived;
                if (xprocess.WaitForExit(15000) == false) { try { xprocess.Kill(); } catch (Exception) { } displayMessage("Unable to set NPM HTTPS proxy (#2).", 2); workerThread = null; return; }

                // Setup the NPM HTTPS proxy
                displayMessage("Configuring NPM...", 0, "Setting up HTTPS proxy.");
                xprocess = new Process();
                xprocess.StartInfo.CreateNoWindow = true;
                xprocess.StartInfo.UseShellExecute = false;
                xprocess.StartInfo.RedirectStandardError = true;
                xprocess.StartInfo.RedirectStandardOutput = true;
                xprocess.StartInfo.RedirectStandardInput = true;
                xprocess.StartInfo.FileName = @"C:\Program Files\nodejs\npm.cmd";
                xprocess.StartInfo.Arguments = "config set https-proxy " + proxyUri.ToString();
                xprocess.StartInfo.WorkingDirectory = @"C:\Program Files\nodejs";
                xstartSuccess = false;
                log("Launching: " + xprocess.StartInfo.FileName + " " + xprocess.StartInfo.Arguments);
                try { xstartSuccess = xprocess.Start(); } catch (Exception) { }
                if (xstartSuccess == false) { displayMessage("Unable to set NPM HTTPS proxy (#1).", 2); workerThread = null; return; }
                allOutput = "";
                xprocess.BeginOutputReadLine();
                xprocess.BeginErrorReadLine();
                xprocess.OutputDataReceived += Process_OutputDataReceived;
                xprocess.ErrorDataReceived += Process_ErrorDataReceived;
                if (xprocess.WaitForExit(15000) == false) { try { xprocess.Kill(); } catch (Exception) { } displayMessage("Unable to set NPM HTTPS proxy (#2).", 2); workerThread = null; return; }
            }

            Process process = null;
            bool startSuccess = false;

            // Install MeshCentral
            displayMessage("Installing MeshCentral...", 0, "This may take several minutes.");
            process = new Process();
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.FileName = @"C:\Program Files\nodejs\npm.cmd";
            process.StartInfo.Arguments = "install --no-package-lock meshcentral";
            process.StartInfo.WorkingDirectory = dir.FullName;
            startSuccess = false;
            log("Launching: " + process.StartInfo.FileName + " " + process.StartInfo.Arguments);
            try { startSuccess = process.Start(); } catch (Exception) { }
            if (startSuccess == false) { displayMessage("Unable to install MeshCentral.", 2); workerThread = null; return; }
            allOutput = "";
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.OutputDataReceived += Process_OutputDataReceived;
            process.ErrorDataReceived += Process_ErrorDataReceived;
            if (process.WaitForExit(300000) == false) { try { process.Kill(); } catch (Exception) { } displayMessage("Unable to install MeshCentral, install process did not exit.", 2); workerThread = null; return; }

            // Check that node_modules folder was created
            DirectoryInfo modulesdir = new DirectoryInfo(Path.Combine(ServerInstallPath, "node_modules"));
            if (Directory.Exists(modulesdir.FullName) == false) { displayMessage("Can't find node_modules folder, installation failed.", 2); workerThread = null; return; }

            // Install all extra modules
            string optionalModules = g_optionalModules;
            if (currentNodeVersion < new Version("8.0.0")) { optionalModules += " util.promisify"; }
            if (InstallModule(dir, optionalModules, false) == false) return;

            DirectorySecurity restrictedPermissions = null;
            try
            {
                // Setup special folder restricted permissions
                restrictedPermissions = new DirectorySecurity(ServerInstallPath, AccessControlSections.All);
                restrictedPermissions.SetAccessRuleProtection(true, false);
                restrictedPermissions.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().Name, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.NoPropagateInherit, AccessControlType.Allow));
                restrictedPermissions.AddAccessRule(new FileSystemAccessRule("SYSTEM", FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.NoPropagateInherit, AccessControlType.Allow));
            }
            catch (Exception) { }

            DirectoryInfo datadir = new DirectoryInfo(Path.Combine(ServerInstallPath, "meshcentral-data"));
            DirectoryInfo filesdir = new DirectoryInfo(Path.Combine(ServerInstallPath, "meshcentral-files"));
            DirectoryInfo recordingsdir = new DirectoryInfo(Path.Combine(ServerInstallPath, "meshcentral-recordings"));
            DirectoryInfo backupsdir = new DirectoryInfo(Path.Combine(ServerInstallPath, "meshcentral-backups"));

            try
            {
                // Setup folders with restricted permissions
                if (Directory.Exists(datadir.FullName) == false) { Directory.CreateDirectory(datadir.FullName, restrictedPermissions); }
                if (Directory.Exists(filesdir.FullName) == false) { Directory.CreateDirectory(filesdir.FullName, restrictedPermissions); }
                if (Directory.Exists(recordingsdir.FullName) == false) { Directory.CreateDirectory(recordingsdir.FullName, restrictedPermissions); }
                if (Directory.Exists(backupsdir.FullName) == false) { Directory.CreateDirectory(backupsdir.FullName, restrictedPermissions); }
            } catch (Exception) {
                // Fallback, setup data folder with normal permissions
                if (Directory.Exists(datadir.FullName) == false) { Directory.CreateDirectory(datadir.FullName); }
                if (Directory.Exists(filesdir.FullName) == false) { Directory.CreateDirectory(filesdir.FullName); }
                if (Directory.Exists(recordingsdir.FullName) == false) { Directory.CreateDirectory(recordingsdir.FullName); }
                if (Directory.Exists(backupsdir.FullName) == false) { Directory.CreateDirectory(backupsdir.FullName); }
            }

            // Write the configuration file
            FileInfo configFileInfo = new FileInfo(Path.Combine(ServerInstallPath, "meshcentral-data", "config.json"));
            if (File.Exists(configFileInfo.FullName) == true) {
                int i = 0;
                while (File.Exists(configFileInfo.FullName + ".old" + i) == true) { i++; }
                File.Move(configFileInfo.FullName, configFileInfo.FullName + ".old" + i);
            }

            // Create the configuration file
            string config = "{\r\n  \"settings\": {\r\n";
            if (ServerMode > 0) { config += "    \"cert\": \"" + ServerName + "\",\r\n"; }
            if (ServerMode == 0) { config += "    \"lanonly\": true,\r\n"; }
            if (ServerMode == 1) { config += "    \"wanonly\": true,\r\n"; }
            if (ServerMultiUser == false) { config += "    \"nousers\": true,\r\n"; }
            //config += "    \"selfupdate\": " + (ServerSelfUpdate ? "true" : "false") + "\r\n";
            config += "    \"_minify\": true\r\n";
            config += "  }\r\n}\r\n";
            File.WriteAllText(configFileInfo.FullName, config);

            // Write the registry key
            RegistryKey myKey = Registry.LocalMachine.CreateSubKey("SOFTWARE\\Open Source\\MeshCentral2");
            if (myKey != null) {
                myKey.SetValue("InstallPath", ServerInstallPath, RegistryValueKind.String);
                myKey.SetValue("ServerName", ServerName, RegistryValueKind.String);
                myKey.SetValue("ServerMode", ServerMode, RegistryValueKind.DWord);
                myKey.SetValue("ServerMultiUser", ServerMultiUser ? 1 : 0, RegistryValueKind.DWord);
                //myKey.SetValue("ServerSelfUpdate", ServerSelfUpdate ? 1 : 0, RegistryValueKind.DWord);
                myKey.Close();
            }

            displayMessage("Installing MeshCentral Service...", 0, "This may take several minutes.");

            if (File.Exists(Path.Combine(ServerInstallPath, "node_modules\\meshcentral\\winservice.js")) == true)
            {
                // Copy winservice.js in a seperate folder
                try
                {
                    Directory.CreateDirectory(Path.Combine(ServerInstallPath, "winservice"), restrictedPermissions);
                } catch (Exception) {
                    Directory.CreateDirectory(Path.Combine(ServerInstallPath, "winservice"));
                }
                File.Copy(Path.Combine(ServerInstallPath, "node_modules\\meshcentral\\winservice.js"), Path.Combine(ServerInstallPath, "winservice\\winservice.js"), true);

                // Install and start the service
                process = new Process();
                process.StartInfo.FileName = "C:\\Program Files\\nodejs\\node.exe";
                process.StartInfo.Arguments = "\"" + Path.Combine(ServerInstallPath, "winservice\\winservice.js") + "\" --install";
                process.StartInfo.WorkingDirectory = Path.Combine(ServerInstallPath, "node_modules");
                process.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
            }
            else
            {
                // Install and start the service
                process = new Process();
                process.StartInfo.FileName = "C:\\Program Files\\nodejs\\node.exe";
                process.StartInfo.Arguments = "\"" + Path.Combine(ServerInstallPath, "node_modules\\meshcentral\\meshcentral.js") + "\" --install";
                process.StartInfo.WorkingDirectory = Path.Combine(ServerInstallPath, "node_modules");
                process.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
            }

            // Launch the startup process
            startSuccess = false;
            log("Launching: " + process.StartInfo.FileName + " " + process.StartInfo.Arguments);
            try { startSuccess = process.Start(); } catch (Exception e) { displayMessage("Exception Error", 2, e.ToString()); }
            if (startSuccess == false) { displayMessage("Can't install MeshCentral Service (#1).", 2); workerThread = null; return; }
            allOutput = "";
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.OutputDataReceived += Process_OutputDataReceived;
            process.ErrorDataReceived += Process_ErrorDataReceived;
            if (process.WaitForExit(60000) == false) { try { process.Kill(); } catch (Exception) { } displayMessage("Can't install MeshCentral Service (#2).", 2); workerThread = null; return; }

            // Check for errors, stop if there are any
            if (allOutput.IndexOf("ERROR:") >= 0) { displayMessage("Can't install MeshCentral Service (#3).", 2, allOutput); return; }

            // Waiting 30 seconds before continuing to fix an winsw install bug
            log("Waiting 30 seconds before continuing to fix an winsw install bug.");
            displayMessage("Waiting 30 seconds", 2, "This fixes a bug with the WinSW executable");
            System.Threading.Thread.Sleep(30000);

            // We need to start the service again just incase it didnt actually start the first time around
            displayMessage("Starting MeshCentral Service...", 0, "This may take several minutes.");

            if (File.Exists(Path.Combine(ServerInstallPath, "node_modules\\meshcentral\\winservice.js")) == true)
            {
                // Copy winservice.js in a seperate folder
                try
                {
                    Directory.CreateDirectory(Path.Combine(ServerInstallPath, "winservice"), restrictedPermissions);
                }
                catch (Exception)
                {
                    Directory.CreateDirectory(Path.Combine(ServerInstallPath, "winservice"));
                }
                File.Copy(Path.Combine(ServerInstallPath, "node_modules\\meshcentral\\winservice.js"), Path.Combine(ServerInstallPath, "winservice\\winservice.js"), true);

                // start the service
                process = new Process();
                process.StartInfo.FileName = "C:\\Program Files\\nodejs\\node.exe";
                process.StartInfo.Arguments = "\"" + Path.Combine(ServerInstallPath, "winservice\\winservice.js") + "\" --start";
                process.StartInfo.WorkingDirectory = Path.Combine(ServerInstallPath, "node_modules");
                process.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
            }
            else
            {
                // Install and start the service
                process = new Process();
                process.StartInfo.FileName = "C:\\Program Files\\nodejs\\node.exe";
                process.StartInfo.Arguments = "\"" + Path.Combine(ServerInstallPath, "node_modules\\meshcentral\\meshcentral.js") + "\" --start";
                process.StartInfo.WorkingDirectory = Path.Combine(ServerInstallPath, "node_modules");
                process.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
            }

            // Launch the startup process
            startSuccess = false;
            log("Launching: " + process.StartInfo.FileName + " " + process.StartInfo.Arguments);
            try { startSuccess = process.Start(); } catch (Exception e) { displayMessage("Exception Error", 2, e.ToString()); }
            if (startSuccess == false) { displayMessage("Can't start MeshCentral Service (#1).", 2); workerThread = null; return; }
            allOutput = "";
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.OutputDataReceived += Process_OutputDataReceived;
            process.ErrorDataReceived += Process_ErrorDataReceived;
            if (process.WaitForExit(60000) == false) { try { process.Kill(); } catch (Exception) { } displayMessage("Can't start MeshCentral Service (#2).", 2); workerThread = null; return; }

            // Check for errors, stop if there are any
            if (allOutput.IndexOf("ERROR:") >= 0) { displayMessage("Can't start MeshCentral Service (#3).", 2, allOutput); return; }

            // Start looking at the server state
            ServerState s = new ServerState();
            s.state = ServerStateEnum.Unknown;
            while ((s.state != ServerStateEnum.Running) && (s.state != ServerStateEnum.Stopped))
            {
                s = readServerStateEx(ServerInstallPath);
                if (s.state == ServerStateEnum.Certificate) { displayMessage("Generating certificates...", 0); }
                if (s.state == ServerStateEnum.Starting) { displayMessage("Server starting up...", 0); }
                if ((s.state != ServerStateEnum.Running) && (s.state != ServerStateEnum.Stopped)) { System.Threading.Thread.Sleep(300); }
            }

            setLink(s.url);
            if (s.nousers == false)
            {
                displayMessage("Installation Completed.", 3, "The first account to be created on the server will be the server administrator.\r\nInitial certificate will not be trusted by browsers.", -1);
            } else {
                displayMessage("Installation Completed.", 3, "", -1);
            }
        }

        private bool InstallModule(DirectoryInfo dir, string modulename, bool update)
        {
            Process process = null;
            bool startSuccess = false;

            // Install node-windows
            if (update)
            {
                if (modulename.Split(' ').Length > 1)
                {
                    displayMessage("Updating optional modules...", 0, "This may take several minutes.");
                }
                else
                {
                    displayMessage("Updating " + modulename + "...", 0, "This may take several minutes.");
                }
            }
            else
            {
                if (modulename.Split(' ').Length > 1)
                {
                    displayMessage("Installing optional modules...", 0, "This may take several minutes.");
                }
                else
                {
                    displayMessage("Installing " + modulename + "...", 0, "This may take several minutes.");
                }
            }
            process = new Process();
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.FileName = @"C:\Program Files\nodejs\npm.cmd";
            process.StartInfo.Arguments = "install --no-package-lock --no-optional --save " + modulename;
            process.StartInfo.WorkingDirectory = dir.FullName;
            startSuccess = false;
            log("Launching: " + process.StartInfo.FileName + " " + process.StartInfo.Arguments);
            try { startSuccess = process.Start(); } catch (Exception) { }
            if (startSuccess == false) { displayMessage("Unable to install " + modulename + " (#1).", 2); workerThread = null; return false; }
            allOutput = "";
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.OutputDataReceived += Process_OutputDataReceived;
            process.ErrorDataReceived += Process_ErrorDataReceived;
            if (process.WaitForExit(120000) == false) { try { process.Kill(); } catch (Exception) { } displayMessage("Unable to install " + modulename + " (#2).", 2); workerThread = null; return false; }
            return true;
        }

        private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            log("StdErr: " + e.Data);
            if ((e.Data != null) && (e.Data != "")) {
                allOutput += (e.Data + "\r\n");
                //displayMessage(null, 0, allOutput);
            }
        }

        string allOutput = "";
        private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            log("StdOut: " + e.Data);
            if ((e.Data != null) && (e.Data != "")) {
                allOutput += (e.Data + "\r\n");
                //displayMessage(null, 0, allOutput);
            }
        }

        private void Process_OutputDataReceivedEx(object sender, DataReceivedEventArgs e)
        {
            log("StdOutEx: " + e.Data);
            if ((e.Data != null) && (e.Data != "")) { onlineVersion = e.Data; }
        }

        public static string CalculateSHA384(string filename)
        {
            try
            {
                using (var sha384 = SHA384.Create())
                {
                    using (var stream = File.OpenRead(filename))
                    {
                        var hash = sha384.ComputeHash(stream);
                        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    }
                }
            }
            catch (Exception) { return ""; }
        }

        private void nextButton1_Click(object sender, EventArgs e) { setPanel(2); }
        private void backButton2_Click(object sender, EventArgs e) { if (ServerAlreadyInstalled == false) { setPanel(1); } else { setPanel(4); } }

        private void outputFolderButton_Click(object sender, EventArgs e)
        {
            outputFolderBrowserDialog.SelectedPath = installPathTextBox.Text;
            if (outputFolderBrowserDialog.ShowDialog(this) == DialogResult.OK) { installPathTextBox.Text = outputFolderBrowserDialog.SelectedPath; }
        }

        private void nextButton2_Click(object sender, EventArgs e)
        {
            nextButton3.Enabled = false;
            backButton3.Enabled = false;
            setPanel(3);
            if (workerThread != null) { return; }

            // Set install settings
            ServerInstallPath = installPathTextBox.Text;
            ServerMode = serverModeComboBox.SelectedIndex;
            //ServerSelfUpdate = autoUpdateCheckBox.Checked;
            ServerMultiUser = multiUserCheckBox.Checked;
            ServerName = serverNameTextBox.Text;

            // Start worker thread
            workerThread = new Thread(new ThreadStart(performInstallation));
            workerThread.Start();
        }

        private void nextButton5_Click(object sender, EventArgs e) { Application.Exit(); }

        private void backButton3_Click(object sender, EventArgs e) {
            // Install failed, reset the installer
            displayMessage("");
            SetStartPanel();
        }

        private void serverModeComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            updateSettings();
        }

        private void licenseLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://www.apache.org/licenses/LICENSE-2.0");
        }

        private void helpLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("http://info.meshcentral.com/downloads/MeshCentral2/MeshCentral2UserGuide.pdf");
        }

        private void updateSettings()
        {
            serverNameTextBox.Enabled = (serverModeComboBox.SelectedIndex > 0);
            string[] nameArray = serverNameTextBox.Text.Split('.');
            bool ok = ((serverModeComboBox.SelectedIndex == 0) || ((serverNameTextBox.Text.Length > 0) && (nameArray.Length > 1) && (nameArray[0].Length > 0) && (nameArray[1].Length > 0)));
            if ((serverNameTextBox.Text.IndexOf(":") >= 0) || (serverNameTextBox.Text.IndexOf("/") >= 0) || (serverNameTextBox.Text.IndexOf("@") >= 0)) { ok = false; }
            if (ok == false) { serverNameTextBox.BackColor = System.Drawing.Color.MistyRose; } else { serverNameTextBox.BackColor = windowColor; }
            nextButton2.Enabled = ok;
            serverNameWarnLabel.Visible = (serverModeComboBox.SelectedIndex != 0);
        }

        private void serverNameTextBox_TextChanged(object sender, EventArgs e)
        {
            updateSettings();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (radioButton1.Checked) { setPanel(2); } // Reinstall
            else if (radioButton2.Checked) {
                // Check for update
                nextButton3.Enabled = false;
                backButton3.Enabled = false;
                setPanel(3);
                if (workerThread != null) { return; }

                // Start worker thread
                workerThread = new Thread(new ThreadStart(performUpdateCheck));
                workerThread.Start();
            }
            else if (radioButton3.Checked) {
                // Uninstall
                nextButton3.Enabled = false;
                backButton3.Enabled = false;
                setPanel(3);
                if (workerThread != null) { return; }

                // Start worker thread
                workerThread = new Thread(new ThreadStart(performUnInstallation));
                workerThread.Start();
            }
            else if (radioButton4.Checked)
            {
                // Scan the network
                setPanel(8);
            }
            else if (radioButton5.Checked)
            {
                // Edit configuration, get the location of the config.json file
                string existingPath = null;
                RegistryKey myKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Open Source\\MeshCentral2", true);
                if (myKey != null)
                {
                    try { existingPath = (string)myKey.GetValue("InstallPath", null); } catch (Exception) { }
                    myKey.Close();
                }
                if ((existingPath != null) && Directory.Exists(existingPath) && Directory.Exists(Path.Combine(existingPath, "meshcentral-data")) && File.Exists(Path.Combine(existingPath, "meshcentral-data", "config.json")))
                {
                    loadConfiguration(Path.Combine(existingPath, "meshcentral-data", "config.json"));
                    setPanel(9);
                    //new ConfigEditorForm(Path.Combine(existingPath, "meshcentral-data", "config.json")).ShowDialog(this);
                }
                else
                {
                    MessageBox.Show(this, "Unable to open config.json file.", "Configuration Editor", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void uninstallConfirmCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            updateRadioButtons();
        }

        private void updateRadioButtons()
        {
            pictureBox12.Visible = uninstallConfirmCheckBox.Visible = radioButton3.Checked;
            if (radioButton3.Checked == false) { uninstallConfirmCheckBox.Checked = false; }
            button2.Enabled = ((radioButton3.Checked == false) || (uninstallConfirmCheckBox.Checked == true));
        }

        private void performUpdateCheck()
        {
            try
            {
                // Check if NodeJS is installed
                displayMessage("Checking for updates...");

                // Get current version
                string currentVersion = null;
                string packagePath = Path.Combine(ServerInstallPath, "node_modules\\meshcentral\\package.json");
                string config = null;
                try { config = File.ReadAllText(packagePath); } catch (Exception) { }
                if (config != null) {
                    int i = config.IndexOf("  \"version\": \"");
                    if (i >= 0) { config = config.Substring(i + 14); i = config.IndexOf("\""); if (i >= 0) { currentVersion = config.Substring(0, i); } }
                } else { currentVersion = "Unknown"; }

                // Get online version
                Process process = new Process();
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardInput = true;
                process.StartInfo.FileName = @"C:\Program Files\nodejs\npm.cmd";
                process.StartInfo.Arguments = "view meshcentral dist-tags.latest";
                process.StartInfo.WorkingDirectory = @"C:\Program Files\nodejs";
                var startSuccess = false;
                log("Launching: " + process.StartInfo.FileName + " " + process.StartInfo.Arguments);
                try { startSuccess = process.Start(); } catch (Exception) { }
                if (startSuccess == false) { displayMessage("Unable to get available version (#1).", 2); workerThread = null; return; }
                allOutput = "";
                process.BeginOutputReadLine();
                process.OutputDataReceived += Process_OutputDataReceivedEx;
                if (process.WaitForExit(10000) == false) { try { process.Kill(); } catch (Exception) { } displayMessage("Unable to get available version (#2).", 2); workerThread = null; return; }

                displayVersions(currentVersion, onlineVersion);
                workerThread = null;
            }
            catch (Exception e) { displayMessage("Exception Error", 2, e.ToString()); }
        }

        private void performUnInstallation()
        {
            Process process;

            try
            {
                // Check if NodeJS is installed
                displayMessage("Performing uninstall...");

                // Shutdown the MeshCentral service
                try
                {
                    ServiceController[] services = ServiceController.GetServices();
                    foreach (ServiceController service in services)
                    {
                        if (service.ServiceName == "meshcentral.exe")
                        {
                            displayMessage("Stopping service...", 0, "");
                            service.Stop();
                            service.WaitForStatus(ServiceControllerStatus.Stopped, new TimeSpan(0, 0, 30));
                        }
                    }
                }
                catch (Exception) { }

                // Uninstall the MeshCentral service
                try
                {
                    displayMessage("Uninstalling service...", 0, "");
                    ServiceInstaller ServiceInstallerObj = new ServiceInstaller();
                    System.Configuration.Install.InstallContext Context = new System.Configuration.Install.InstallContext(Path.Combine(ServerInstallPath, "uninstall.log"), null);
                    ServiceInstallerObj.Context = Context;
                    ServiceInstallerObj.ServiceName = "meshcentral.exe";
                    ServiceInstallerObj.Uninstall(null);
                }
                catch (Exception) { }

                // Remove firewall rules
                displayMessage("Firewall Setup...", 0, "Removing firewall rules.");
                FirewallSetup.RemoveRules();

                // Uninstall the MeshCentral service using new system
                int cycle = 3;
                while (--cycle > 0)
                {
                    if (Directory.Exists(Path.Combine(ServerInstallPath, "winservice")) && File.Exists(Path.Combine(ServerInstallPath, "winservice\\winservice.js")))
                    {
                        displayMessage("Uninstalling service...", 0, "winservice.js --uninstall");
                        process = new Process();
                        process.StartInfo.FileName = "C:\\Program Files\\nodejs\\node.exe";
                        process.StartInfo.Arguments = "\"" + Path.Combine(ServerInstallPath, "winservice\\winservice.js") + "\" --uninstall";
                        process.StartInfo.WorkingDirectory = ServerInstallPath;
                        process.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.CreateNoWindow = true;
                        process.StartInfo.RedirectStandardOutput = true;
                        process.StartInfo.RedirectStandardError = true;
                        log("Launching: " + process.StartInfo.FileName + " " + process.StartInfo.Arguments);
                        process.Start();
                        if (process.WaitForExit(10000) == false) { try { process.Kill(); } catch (Exception) { } displayMessage("Unable to shutdown MeshCentral (#2).", 2); workerThread = null; return; }
                        string a1 = process.StandardOutput.ReadToEnd();
                        string a2 = process.StandardError.ReadToEnd();
                        if (a2 == "") { cycle = 0; } // Try to uninstall the service until a few times if it does not work.
                    }
                }

                // Uninstall the MeshCentral service using old system
                cycle = 3;
                while (--cycle > 0)
                {
                    if (Directory.Exists(Path.Combine(ServerInstallPath, "node_modules")) && Directory.Exists(Path.Combine(ServerInstallPath, "node_modules\\meshcentral")) && File.Exists(Path.Combine(ServerInstallPath, "node_modules\\meshcentral\\meshcentral.js")))
                    {
                        displayMessage("Uninstalling service...", 0, "meshcentral.js --uninstall");
                        process = new Process();
                        process.StartInfo.FileName = "C:\\Program Files\\nodejs\\node.exe";
                        process.StartInfo.Arguments = "\"" + Path.Combine(ServerInstallPath, "node_modules\\meshcentral\\meshcentral.js") + "\" --uninstall";
                        process.StartInfo.WorkingDirectory = Path.Combine(ServerInstallPath, "node_modules");
                        process.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.CreateNoWindow = true;
                        process.StartInfo.RedirectStandardOutput = true;
                        process.StartInfo.RedirectStandardError = true;
                        log("Launching: " + process.StartInfo.FileName + " " + process.StartInfo.Arguments);
                        process.Start();
                        if (process.WaitForExit(10000) == false) { try { process.Kill(); } catch (Exception) { } displayMessage("Unable to shutdown MeshCentral (#2).", 2); workerThread = null; return; }
                        string a1 = process.StandardOutput.ReadToEnd();
                        string a2 = process.StandardError.ReadToEnd();
                        if (a2 == "") { cycle = 0; } // Try to uninstall the service until a few times if it does not work.
                    }
                }

                // Delete all files
                try { Directory.Delete(ServerInstallPath, true); } catch (Exception) { }
                System.Threading.Thread.Sleep(1000);
                try { Directory.Delete(ServerInstallPath, true); } catch (Exception) { }
                System.Threading.Thread.Sleep(4000);
                try { Directory.Delete(ServerInstallPath, true); } catch (Exception) { }

                // Remove parent folder if empty
                int entryCount = 0;
                string parentPath = new DirectoryInfo(ServerInstallPath).Parent.FullName;
                foreach (string filename in Directory.EnumerateFiles(parentPath)) { entryCount++; }
                foreach (string filename in Directory.EnumerateDirectories(parentPath)) { entryCount++; }
                if (entryCount == 0) { try { Directory.Delete(parentPath, false); } catch (Exception) { } }

                // Delete registery key
                Registry.LocalMachine.DeleteSubKey("SOFTWARE\\Open Source\\MeshCentral2");

                // We are done
                displayMessage("Uninstall completed", 3, "If not needed, uninstall NodeJS seperatly.");
            }
            catch (Exception e) { displayMessage("Exception Error", 2, e.ToString()); }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            setPanel(4);
        }

        private void performUpdate()
        {
            try
            {
                // Check if NodeJS is installed
                displayMessage("Checking NodeJS installation...");
                currentNodeVersion = GetCurrentNodeVersion();
                if (currentNodeVersion == null) { displayMessage("NodeJS installation not detected, please re-install.", 2); workerThread = null; return; }

                Process process;
                DirectoryInfo dir = new DirectoryInfo(ServerInstallPath);
                if (Directory.Exists(dir.FullName) == false) {
                    DirectorySecurity xrestrictedPermissions = null;
                    try
                    {
                        // Setup special folder restricted permissions
                        xrestrictedPermissions = new DirectorySecurity(ServerInstallPath, AccessControlSections.All);
                        xrestrictedPermissions.SetAccessRuleProtection(true, false);
                        xrestrictedPermissions.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().Name, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.NoPropagateInherit, AccessControlType.Allow));
                        xrestrictedPermissions.AddAccessRule(new FileSystemAccessRule("SYSTEM", FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.NoPropagateInherit, AccessControlType.Allow));
                    }
                    catch (Exception) { }

                    try
                    {
                        Directory.CreateDirectory(dir.FullName, xrestrictedPermissions);
                    }
                    catch (Exception)
                    {
                        Directory.CreateDirectory(dir.FullName);
                    }
                }

                // Configure the HTTP proxy if needed
                Uri x = new Uri(nodeUrl64);
                WebProxy proxy = WebProxy.GetDefaultProxy();
                Uri proxyUri = proxy.GetProxy(x);
                if (proxyUri.ToString() != x.ToString())
                {
                    // Setup the NPM HTTP proxy
                    displayMessage("Configuring NPM...", 0, "Setting up HTTP proxy.");
                    Process xprocess = new Process();
                    xprocess.StartInfo.CreateNoWindow = true;
                    xprocess.StartInfo.UseShellExecute = false;
                    xprocess.StartInfo.RedirectStandardError = true;
                    xprocess.StartInfo.RedirectStandardOutput = true;
                    xprocess.StartInfo.RedirectStandardInput = true;
                    xprocess.StartInfo.FileName = @"C:\Program Files\nodejs\npm.cmd";
                    xprocess.StartInfo.Arguments = "config set proxy " + proxyUri.ToString();
                    xprocess.StartInfo.WorkingDirectory = @"C:\Program Files\nodejs";
                    var xstartSuccess = false;
                    log("Launching: " + xprocess.StartInfo.FileName + " " + xprocess.StartInfo.Arguments);
                    try { xstartSuccess = xprocess.Start(); } catch (Exception) { }
                    if (xstartSuccess == false) { displayMessage("Unable to set NPM HTTP proxy (#1).", 2); workerThread = null; return; }
                    allOutput = "";
                    xprocess.BeginOutputReadLine();
                    xprocess.BeginErrorReadLine();
                    xprocess.OutputDataReceived += Process_OutputDataReceived;
                    xprocess.ErrorDataReceived += Process_ErrorDataReceived;
                    if (xprocess.WaitForExit(15000) == false) { try { xprocess.Kill(); } catch (Exception) { } displayMessage("Unable to set NPM HTTPS proxy (#2).", 2); workerThread = null; return; }

                    // Setup the NPM HTTPS proxy
                    displayMessage("Configuring NPM...", 0, "Setting up HTTPS proxy.");
                    xprocess = new Process();
                    xprocess.StartInfo.CreateNoWindow = true;
                    xprocess.StartInfo.UseShellExecute = false;
                    xprocess.StartInfo.RedirectStandardError = true;
                    xprocess.StartInfo.RedirectStandardOutput = true;
                    xprocess.StartInfo.RedirectStandardInput = true;
                    xprocess.StartInfo.FileName = @"C:\Program Files\nodejs\npm.cmd";
                    xprocess.StartInfo.Arguments = "config set https-proxy " + proxyUri.ToString();
                    xprocess.StartInfo.WorkingDirectory = @"C:\Program Files\nodejs";
                    xstartSuccess = false;
                    log("Launching: " + xprocess.StartInfo.FileName + " " + xprocess.StartInfo.Arguments);
                    try { xstartSuccess = xprocess.Start(); } catch (Exception) { }
                    if (xstartSuccess == false) { displayMessage("Unable to set NPM HTTPS proxy (#1).", 2); workerThread = null; return; }
                    allOutput = "";
                    xprocess.BeginOutputReadLine();
                    xprocess.BeginErrorReadLine();
                    xprocess.OutputDataReceived += Process_OutputDataReceived;
                    xprocess.ErrorDataReceived += Process_ErrorDataReceived;
                    if (xprocess.WaitForExit(15000) == false) { try { xprocess.Kill(); } catch (Exception) { } displayMessage("Unable to set NPM HTTPS proxy (#2).", 2); workerThread = null; return; }
                }

                // Shutdown the MeshCentral service
                try
                {
                    ServiceController[] services = ServiceController.GetServices();
                    foreach (ServiceController service in services)
                    {
                        if (service.ServiceName == "meshcentral.exe")
                        {
                            displayMessage("Stopping service...", 0, "");
                            service.Stop();
                            service.WaitForStatus(ServiceControllerStatus.Stopped, new TimeSpan(0, 0, 30));
                        }
                    }
                }
                catch (Exception) { }

                // Uninstall the MeshCentral service
                try
                {
                    displayMessage("Uninstalling service...", 0, "");
                    ServiceInstaller ServiceInstallerObj = new ServiceInstaller();
                    System.Configuration.Install.InstallContext Context = new System.Configuration.Install.InstallContext(Path.Combine(ServerInstallPath, "uninstall.log"), null);
                    ServiceInstallerObj.Context = Context;
                    ServiceInstallerObj.ServiceName = "meshcentral.exe";
                    ServiceInstallerObj.Uninstall(null);
                }
                catch (Exception) { }

                // Uninstall the MeshCentral service using new system
                int cycle = 3;
                while (--cycle > 0)
                {
                    if (Directory.Exists(Path.Combine(ServerInstallPath, "winservice")) && File.Exists(Path.Combine(ServerInstallPath, "winservice\\winservice.js")))
                    {
                        displayMessage("Uninstalling service...", 0, "winservice.js --uninstall");
                        process = new Process();
                        process.StartInfo.FileName = "C:\\Program Files\\nodejs\\node.exe";
                        process.StartInfo.Arguments = "\"" + Path.Combine(ServerInstallPath, "winservice\\winservice.js") + "\" --uninstall";
                        process.StartInfo.WorkingDirectory = ServerInstallPath;
                        process.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.CreateNoWindow = true;
                        process.StartInfo.RedirectStandardOutput = true;
                        process.StartInfo.RedirectStandardError = true;
                        log("Launching: " + process.StartInfo.FileName + " " + process.StartInfo.Arguments);
                        process.Start();
                        if (process.WaitForExit(10000) == false) { try { process.Kill(); } catch (Exception) { } displayMessage("Unable to shutdown MeshCentral (#2).", 2); workerThread = null; return; }
                        string a1 = process.StandardOutput.ReadToEnd();
                        string a2 = process.StandardError.ReadToEnd();
                        if (a2 == "") { cycle = 0; } // Try to uninstall the service until a few times if it does not work.
                    }
                }

                // Uninstall the MeshCentral service using old system
                cycle = 3;
                while (--cycle > 0)
                {
                    if (Directory.Exists(Path.Combine(ServerInstallPath, "node_modules")) && Directory.Exists(Path.Combine(ServerInstallPath, "node_modules\\meshcentral")) && File.Exists(Path.Combine(ServerInstallPath, "node_modules\\meshcentral\\meshcentral.js")))
                    {
                        displayMessage("Uninstalling service...", 0, "meshcentral.js --uninstall");
                        process = new Process();
                        process.StartInfo.FileName = "C:\\Program Files\\nodejs\\node.exe";
                        process.StartInfo.Arguments = "\"" + Path.Combine(ServerInstallPath, "node_modules\\meshcentral\\meshcentral.js") + "\" --uninstall";
                        process.StartInfo.WorkingDirectory = Path.Combine(ServerInstallPath, "node_modules");
                        process.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.CreateNoWindow = true;
                        process.StartInfo.RedirectStandardOutput = true;
                        process.StartInfo.RedirectStandardError = true;
                        log("Launching: " + process.StartInfo.FileName + " " + process.StartInfo.Arguments);
                        process.Start();
                        if (process.WaitForExit(10000) == false) { try { process.Kill(); } catch (Exception) { } displayMessage("Unable to shutdown MeshCentral (#2).", 2); workerThread = null; return; }
                        string a1 = process.StandardOutput.ReadToEnd();
                        string a2 = process.StandardError.ReadToEnd();
                        if (a2 == "") { cycle = 0; } // Try to uninstall the service until a few times if it does not work.
                    }
                }

                // Remove the "node_modules_bak" folder
                try
                {
                    if (Directory.Exists(Path.Combine(ServerInstallPath, "node_modules_bak"))) {
                        displayMessage("Removing node_modules_bak...", 0);
                        Directory.Delete(Path.Combine(ServerInstallPath, "node_modules_bak"), true);
                    }
                }
                catch (Exception ex) {
                    log("Folder delete error: " + ex.ToString());
                    displayMessage("Folder delete error", 2, "ERROR: Unable to delete \"node_modules_bak\".\r\nSomething must be locking this folder.\r\n\r\n" + ex.ToString()); workerThread = null;
                    return;
                }

                // Rename node_modules to node_modules_bak
                try
                {
                    if (Directory.Exists(Path.Combine(ServerInstallPath, "node_modules")))
                    {
                        if (IntPtr.Size == 4)
                        {
                            displayMessage("Moving node_modules to node_modules_bak...", 0);
                            InteropSHFileOperation32 fileoperation = new InteropSHFileOperation32();
                            fileoperation.lpszProgressTitle = "Creating node_modules backup";
                            fileoperation.pFrom = Path.Combine(ServerInstallPath, "node_modules");
                            fileoperation.pTo = Path.Combine(ServerInstallPath, "node_modules_bak");
                            fileoperation.wFunc = InteropSHFileOperation32.FO_Func.FO_RENAME;
                            if (fileoperation.Execute() == false) { displayMessage("Folder backup error", 2, "ERROR: Unable to rename \"node_modules\" to \"node_modules_bak\".\r\nSomething must be locking this folder.\r\n\r\n"); workerThread = null; return; }
                        }
                        else
                        {
                            displayMessage("Moving node_modules to node_modules_bak...", 0);
                            InteropSHFileOperation64 fileoperation = new InteropSHFileOperation64();
                            fileoperation.lpszProgressTitle = "Creating node_modules backup";
                            fileoperation.pFrom = Path.Combine(ServerInstallPath, "node_modules");
                            fileoperation.pTo = Path.Combine(ServerInstallPath, "node_modules_bak");
                            fileoperation.wFunc = InteropSHFileOperation64.FO_Func.FO_RENAME;
                            if (fileoperation.Execute() == false) { displayMessage("Folder backup error", 2, "ERROR: Unable to rename \"node_modules\" to \"node_modules_bak\".\r\nSomething must be locking this folder.\r\n\r\n"); workerThread = null; return; }
                        }
                    }
                }
                catch (Exception ex) {
                    log("Folder backup error: " + ex.ToString());
                    displayMessage("Folder backup error", 2, "ERROR: Unable to rename \"node_modules\" to \"node_modules_bak\".\r\nSomething must be locking this folder.\r\n\r\n" + ex.ToString());
                    workerThread = null;
                    return;
                }

                // Delete the serverstate.txt
                try { File.Delete(Path.Combine(ServerInstallPath, "meshcentral-data", "serverstate.txt")); } catch (Exception ex) { log("Delete the serverstate.txt: " + ex.ToString()); }

                // Update MeshCentral
                displayMessage("Performing update...", 0, "This may take several minutes.");
                process = new Process();
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardInput = true;
                process.StartInfo.FileName = @"C:\Program Files\nodejs\npm.cmd";
                process.StartInfo.Arguments = "install --no-package-lock meshcentral";
                process.StartInfo.WorkingDirectory = dir.FullName;
                bool startSuccess = false;
                log("Launching: " + process.StartInfo.FileName + " " + process.StartInfo.Arguments);
                try { startSuccess = process.Start(); } catch (Exception ex) { log("process.Start(): " + ex.ToString()); }
                if (startSuccess == false) { displayMessage("Unable to update MeshCentral (#1).", 2); workerThread = null; return; }
                allOutput = "";
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.OutputDataReceived += Process_OutputDataReceived;
                process.ErrorDataReceived += Process_ErrorDataReceived;
                if (process.WaitForExit(60000) == false) { try { process.Kill(); } catch (Exception ex) { log("process.Kill(): " + ex.ToString()); } displayMessage("Unable to update MeshCentral (#2).", 2); workerThread = null; return; }

                // Install all extra modules
                string optionalModules = g_optionalModules;
                if (currentNodeVersion < new Version("8.0.0")) { optionalModules += " util.promisify"; }
                if (InstallModule(dir, optionalModules, true) == false) return;

                // Setup special folder restricted permissions
                DirectorySecurity restrictedPermissions = null;
                try
                {
                    restrictedPermissions = new DirectorySecurity(ServerInstallPath, AccessControlSections.All);
                    restrictedPermissions.SetAccessRuleProtection(true, false);
                    restrictedPermissions.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().Name, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.NoPropagateInherit, AccessControlType.Allow));
                    restrictedPermissions.AddAccessRule(new FileSystemAccessRule("SYSTEM", FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.NoPropagateInherit, AccessControlType.Allow));
                }
                catch (Exception ex) { log("restrictedPermissions: " + ex.ToString()); }

                if (File.Exists(Path.Combine(ServerInstallPath, "node_modules\\meshcentral\\winservice.js")) == true)
                {
                    // Copy winservice.js in a seperate folder
                    displayMessage("Performing update...", 0, "Creating winservice.js");
                    try
                    {
                        Directory.CreateDirectory(Path.Combine(ServerInstallPath, "winservice"), restrictedPermissions);
                    }
                    catch (Exception ex) {
                        log("CreateDirectory with restrictedPermissions: " + Path.Combine(ServerInstallPath, "winservice") + ": " + ex.ToString());
                        Directory.CreateDirectory(Path.Combine(ServerInstallPath, "winservice"));
                    }
                    File.Copy(Path.Combine(ServerInstallPath, "node_modules\\meshcentral\\winservice.js"), Path.Combine(ServerInstallPath, "winservice\\winservice.js"), true);

                    // Install and start the service
                    displayMessage("Installing service...", 0);
                    process = new Process();
                    process.StartInfo.FileName = "C:\\Program Files\\nodejs\\node.exe";
                    process.StartInfo.Arguments = "\"" + Path.Combine(ServerInstallPath, "winservice\\winservice.js") + "\" --install";
                    process.StartInfo.WorkingDirectory = Path.Combine(ServerInstallPath, "node_modules");
                    process.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                }
                else
                {
                    // Install and start the service
                    displayMessage("Installing service...", 0, "Using old method...");
                    process = new Process();
                    process.StartInfo.FileName = "C:\\Program Files\\nodejs\\node.exe";
                    process.StartInfo.Arguments = "\"" + Path.Combine(ServerInstallPath, "node_modules\\meshcentral\\meshcentral.js") + "\" --install";
                    process.StartInfo.WorkingDirectory = Path.Combine(ServerInstallPath, "node_modules");
                    process.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                }

                // Launch the startup process
                log("Launching: " + process.StartInfo.FileName + " " + process.StartInfo.Arguments);
                process.Start();
                if (process.WaitForExit(20000) == false) { try { process.Kill(); } catch (Exception ex) { log("process.Kill(): " +  ex.ToString()); } displayMessage("Unable to start MeshCentral service (#1).", 2); workerThread = null; return; }
                string b1 = process.StandardOutput.ReadToEnd();
                string b2 = process.StandardError.ReadToEnd();

                // Start looking at the server state
                ServerState s = new ServerState();
                s.state = ServerStateEnum.Unknown;
                while ((s.state != ServerStateEnum.Running) && (s.state != ServerStateEnum.Stopped)) {
                    s = readServerStateEx(installPathTextBox.Text);
                    if (s.state == ServerStateEnum.Certificate) { displayMessage("Generating certificates...", 0); }
                    if (s.state == ServerStateEnum.Starting) { displayMessage("Server starting up...", 0); }
                    if ((s.state != ServerStateEnum.Running) && (s.state != ServerStateEnum.Stopped)) { System.Threading.Thread.Sleep(300); }
                }

                // Remove the "node_modules_bak" folder
                try
                {
                    if (Directory.Exists(Path.Combine(ServerInstallPath, "node_modules_bak"))) {
                        displayMessage("Removing node_modules_bak...", 0);
                        Directory.Delete(Path.Combine(ServerInstallPath, "node_modules_bak"), true);
                    }
                }
                catch (Exception ex) {
                    log("Folder delete error: " + ex.ToString());
                    displayMessage("Folder delete error", 2, "ERROR: Unable to delete \"node_modules_bak\".\r\nSomething must be locking this folder.\r\n\r\n" + ex.ToString());
                    workerThread = null;
                    return;
                }

                setLink(s.url);
                displayMessage("Update Completed.", 3, "", -1);
            }
            catch (Exception e) { displayMessage("Exception Error", 2, e.ToString()); }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            // Update
            nextButton3.Enabled = false;
            backButton3.Enabled = false;
            setPanel(3);
            if (workerThread != null) { return; }

            // Start worker thread
            workerThread = new Thread(new ThreadStart(performUpdate));
            workerThread.Start();
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(linkLabel1.Text);
        }

        public enum ServerStateEnum { Unknown, Starting, Stopped, Running, Certificate }

        public class ServerState
        {
            public ServerStateEnum state;
            public string url;
            public bool nousers;
        }

        private ServerState readServerStateEx(string ServerInstallPath)
        {
            ServerState r = new ServerState();
            r.state = ServerStateEnum.Unknown;
            r.url = null;
            r.nousers = false;
            Dictionary<string, string> state = readServerState(ServerInstallPath);
            if (state == null) return r;
            if (state.ContainsKey("state"))
            {
                string x = state["state"];
                if (x == "starting") { r.state = ServerStateEnum.Starting; }
                else if (x == "running") { r.state = ServerStateEnum.Running; }
                else if (x == "stopped") { r.state = ServerStateEnum.Stopped; }
                else if (x == "generatingcertificates") { r.state = ServerStateEnum.Certificate; }
            }
            if (state.ContainsKey("nousers")) { r.nousers = true; }
            string servername = "localhost";
            if (state.ContainsKey("servername")) { servername = state["servername"]; }
            if (r.state == ServerStateEnum.Running)
            {
                if (state.ContainsKey("https-port")) {
                    string port = state["https-port"];
                    if (port == "443") { r.url = "https://" + servername + "/"; } else { r.url = "https://" + servername + ":" + port + "/"; }
                } else if (state.ContainsKey("http-port")) {
                    string port = state["http-port"];
                    if (port == "80") { r.url = "http://" + servername + "/"; } else { r.url = "http://" + servername + ":" + port + "/"; }
                }
            }
            return r;
        }

        private Dictionary<string, string> readServerState(string ServerInstallPath)
        {
            try
            {
                Dictionary<string, string> r = new Dictionary<string, string>();
                string statepath = Path.Combine(ServerInstallPath, "meshcentral-data", "serverstate.txt");
                string statedata = File.ReadAllText(statepath);
                string[] values = statedata.Replace("\r\n", "\r").Split('\r');
                foreach (string line in values) {
                    if (line.Length > 0) {
                        int i = line.IndexOf("=");
                        if (i > 0) { r.Add(line.Substring(0, i), line.Substring(i + 1)); }
                    }
                }
                log("Read server state: " + r);
                return r;
            }
            catch (Exception) { return null; }
        }

        private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(linkLabel2.Text);
        }

        //
        // MeshCentral Discovery
        //

        private void Scanner_OnNotify(MeshDiscovery sender, System.Net.IPEndPoint source, System.Net.IPEndPoint local, string agentCertHash, string url, string name, string info)
        {
            if (InvokeRequired) { Invoke(new MeshDiscovery.NotifyHandler(Scanner_OnNotify), sender, source, local, agentCertHash, url, name, info); return; }
            AddServer(agentCertHash, name, info, url);
        }

        private void AddServer(string key, string name, string info, string url)
        {
            ServerUserControl match = null;
            foreach (Control c in discoveryPanel.Controls) { if ((c.GetType() == typeof(ServerUserControl)) && (((ServerUserControl)c).key == key)) { match = (ServerUserControl)c; } }
            if (match == null)
            {
                noServerFoundLabel.Visible = false;
                UserControl cc = new ServerUserControl(this, key, name, info, url);
                discoveryPanel.Controls.Add(cc);
                cc.Dock = DockStyle.Top;

                /*
                if (name == null) { name = "MeshCentral"; }
                if (info == null) { info = url; }
                mainNotifyIcon.ShowBalloonTip(2000, "MeshCentral", url, ToolTipIcon.None);
                */
            }
        }

        private void RemoveServer(string key)
        {
            ServerUserControl match = null;
            foreach (Control c in discoveryPanel.Controls) { if ((c.GetType() == typeof(ServerUserControl)) && (((ServerUserControl)c).key == key)) { match = (ServerUserControl)c; } }
            if (match != null) { discoveryPanel.Controls.Remove(match); }
            if (discoveryPanel.Controls.Count == 1) { noServerFoundLabel.Visible = true; }
        }

        private void RemoveAllServers()
        {
            ArrayList keys = new ArrayList();
            foreach (Control c in discoveryPanel.Controls) { if (c.GetType() == typeof(ServerUserControl)) { ServerUserControl ctrl = (ServerUserControl)c; keys.Add(ctrl.key); } }
            foreach (string key in keys) { RemoveServer(key); }
        }

        private void RemoveOldServers(DateTime time)
        {
            ArrayList keys = new ArrayList();
            foreach (Control c in discoveryPanel.Controls)
            {
                if (c.GetType() == typeof(ServerUserControl))
                {
                    ServerUserControl ctrl = (ServerUserControl)c;
                    if (time.Subtract(ctrl.lastUpdate).Ticks > 0) { keys.Add(ctrl.key); }
                }
            }
            foreach (string key in keys) { RemoveServer(key); }
        }

        public void serverClick(ServerUserControl child)
        {
            System.Diagnostics.Process.Start(child.url);
        }
        private void refreshButton_Click(object sender, EventArgs e)
        {
            refreshTime = DateTime.Now;
            scanner.MulticastPing();
            refreshTimer.Enabled = true;
        }
        private void refreshTimer_Tick(object sender, EventArgs e)
        {
            RemoveOldServers(refreshTime);
            refreshTimer.Enabled = false;
        }

        private void backButton7_Click(object sender, EventArgs e)
        {
            SetStartPanel();
        }

        private void closeButton7_Click(object sender, EventArgs e) { Application.Exit(); }

        private void scanButton1_Click(object sender, EventArgs e)
        {
            setPanel(8);
        }

        private void button5_Click(object sender, EventArgs e)
        {
            // Apply new configuration
            string existingPath = null;
            RegistryKey myKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Open Source\\MeshCentral2", true);
            if (myKey != null)
            {
                try { existingPath = (string)myKey.GetValue("InstallPath", null); } catch (Exception ex) { log("MyKey.GetValue(): " + ex.ToString()); }
                myKey.Close();
            }
            if ((existingPath != null) && Directory.Exists(existingPath) && Directory.Exists(Path.Combine(existingPath, "meshcentral-data")) && File.Exists(Path.Combine(existingPath, "meshcentral-data", "config.json")))
            {
                saveConfiguration(Path.Combine(existingPath, "meshcentral-data", "config.json"));

                // Start worker thread to restart the service
                displayMessage("Restarting MeshCentral Service...", 0);
                setPanel(3);
                workerThread = new Thread(new ThreadStart(restartService));
                workerThread.Start();
            }
            else
            {
                MessageBox.Show(this, "Unable to open config.json file.", "Configuration Editor", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button6_Click(object sender, EventArgs e)
        {
            SetStartPanel();
        }

        private void loadConfiguration(string configFilePath)
        {
            Dictionary<string, object> parentConfig = new Dictionary<string, object>();
            Dictionary<string, object> settingsSection = new Dictionary<string, object>();
            Dictionary<string, object> domainsSection = new Dictionary<string, object>();
            Dictionary<string, object> configfilesSection = new Dictionary<string, object>();
            Dictionary<string, object> letsencryptSection = new Dictionary<string, object>();
            Dictionary<string, object> peersSection = new Dictionary<string, object>();
            Dictionary<string, object> smtpSection = new Dictionary<string, object>();
            Dictionary<string, object> discoverySection = new Dictionary<string, object>();
            Dictionary<string, object> domainSection = new Dictionary<string, object>();
            Dictionary<string, object> defaultDomainSection = new Dictionary<string, object>();

            // Read the configuration file and sections
            parentConfig = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(configFilePath));
            settingsSection = readSection(parentConfig, "settings");
            discoverySection = readSection(settingsSection, "localdiscovery");
            configfilesSection = readSection(parentConfig, "configfiles");
            letsencryptSection = readSection(parentConfig, "letsencrypt");
            peersSection = readSection(parentConfig, "peers");
            smtpSection = readSection(parentConfig, "smtp");
            domainsSection = readSection(parentConfig, "domains");
            defaultDomainSection = readSection(domainsSection, "");

            // Read the settings section
            string mongoDb = readString(settingsSection, "mongodb", null);
            string mongoDbCol = readString(settingsSection, "mongodbcol", null);

            string serverName = readString(settingsSection, "cert", null);
            bool noUsers = readBool(settingsSection, "nousers", false);
            bool lanOnly = readBool(settingsSection, "lanonly", false);
            bool wanOnly = readBool(settingsSection, "wanonly", false);
            bool selfupdate = readBool(settingsSection, "selfupdate", false);
            bool minify = readBool(settingsSection, "minify", false);
            bool clickOnce = readBool(settingsSection, "clickonce", false);
            bool allowLoginToken = readBool(settingsSection, "allowlogintoken", false);
            bool allowFraming = readBool(settingsSection, "allowframing", false);
            bool webRtc = readBool(settingsSection, "webrtc", false);

            int port = readInt(settingsSection, "port", 443);
            int redirPort = readInt(settingsSection, "redirport", 80);

            int sessionTime = readInt(settingsSection, "sessiontime", 0);
            string sessionKey = readString(settingsSection, "sessionkey", null);

            // Local discovery section
            string discoveryName = readString(discoverySection, "name", "");
            string discoveryDesc = readString(discoverySection, "info", "");

            // Default domain
            string title1 = readString(defaultDomainSection, "title", "");
            string title2 = readString(defaultDomainSection, "title2", null);
            bool newAccounts = readBool(defaultDomainSection, "newaccounts", true);
            string newAccountPass = readString(defaultDomainSection, "newaccountspass", "");
            int userQuota = readInt(settingsSection, "userquota", 0); // DEFAULT??
            int meshQuota = readInt(settingsSection, "meshquota", 0); // DEFAULT??
            string footer = readString(defaultDomainSection, "footer", "");

            // Email section
            string smtpServerName = readString(smtpSection, "host", null);
            int smtpPort = readInt(smtpSection, "port", 25);
            string smtpFrom = readString(smtpSection, "from", null);
            bool smtpTls = readBool(smtpSection, "tls", false);
            string smtpUser = readString(smtpSection, "user", null);
            string smtpPass = readString(smtpSection, "pass", null);

            // Set the values on the user interface
            serverNameTextBox2.Text = serverName;
            multiUserCheckBox2.Checked = !noUsers;
            autoUpdateCheckBox2.Checked = selfupdate;
            minifyCheckBox.Checked = minify;

            serverModeComboBox2.SelectedIndex = 2;
            if (lanOnly) { serverModeComboBox2.SelectedIndex = 0; }
            if (wanOnly) { serverModeComboBox2.SelectedIndex = 1; }

            clickOnceCheckBox.Checked = clickOnce;
            webrtcCheckBox.Checked = webRtc;
            allowLoginTokenCheckBox.Checked = allowLoginToken;
            allowSiteFramingCheckBox.Checked = allowFraming;

            discoveryNameTextBox.Text = discoveryName;
            discoveryDescTextBox.Text = discoveryDesc;

            titleTextBox1.Text = title1;
            subtitleCheckBox.Checked = (title2 != null);
            if (title2 != null) { titleTextBox2.Text = title2; }
            footerTextBox.Text = footer;
            newAccountCheckBox.Checked = newAccounts;
            newAccountPassTextBox.Text = newAccountPass;

            emailServerCheckBox.Checked = (smtpSection.Count > 0);
            smtpHostTextBox.Text = smtpServerName;
            smtpPortNumericUpDown.Value = smtpPort;
            smtpTlsCheckBox.Checked = smtpTls;
            smtpFromTextBox.Text = smtpFrom;
            smtpUserTextBox.Text = smtpUser;
            smtpPassTextBox.Text = smtpPass;

            updateConfigPanel();
        }

        private void saveConfiguration(string configFilePath)
        {
            Dictionary<string, object> parentConfig = new Dictionary<string, object>();
            Dictionary<string, object> settingsSection = new Dictionary<string, object>();
            Dictionary<string, object> domainsSection = new Dictionary<string, object>();
            Dictionary<string, object> configfilesSection = new Dictionary<string, object>();
            Dictionary<string, object> letsencryptSection = new Dictionary<string, object>();
            Dictionary<string, object> peersSection = new Dictionary<string, object>();
            Dictionary<string, object> smtpSection = new Dictionary<string, object>();
            Dictionary<string, object> discoverySection = new Dictionary<string, object>();
            Dictionary<string, object> domainSection = new Dictionary<string, object>();
            Dictionary<string, object> defaultDomainSection = new Dictionary<string, object>();

            // Read the configuration file and sections
            parentConfig = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(configFilePath));
            settingsSection = readSection(parentConfig, "settings");
            domainsSection = readSection(parentConfig, "domains");
            configfilesSection = readSection(parentConfig, "configfiles");
            letsencryptSection = readSection(parentConfig, "letsencrypt");
            peersSection = readSection(parentConfig, "peers");
            smtpSection = readSection(parentConfig, "smtp");
            discoverySection = readSection(settingsSection, "localdiscovery");
            domainsSection = readSection(parentConfig, "domains");
            defaultDomainSection = readSection(domainsSection, "");

            // General tab
            bool lanOnly = false;
            bool wanOnly = false;
            if (serverModeComboBox2.SelectedIndex == 0) { lanOnly = true; }
            if (serverModeComboBox2.SelectedIndex == 1) { wanOnly = true; }
            if (lanOnly) { settingsSection["lanonly"] = true; } else { if (settingsSection.ContainsKey("lanonly")) { settingsSection.Remove("lanonly"); } }
            if (wanOnly) { settingsSection["wanonly"] = true; } else { if (settingsSection.ContainsKey("wanonly")) { settingsSection.Remove("wanonly"); } }
            if (serverModeComboBox2.SelectedIndex > 0) { settingsSection["cert"] = serverNameTextBox2.Text; } else { if (settingsSection.ContainsKey("cert")) { settingsSection.Remove("cert"); } }
            if (!multiUserCheckBox2.Checked) { settingsSection["nousers"] = true; } else { if (settingsSection.ContainsKey("nousers")) { settingsSection.Remove("nousers"); } }
            if (autoUpdateCheckBox2.Checked) { settingsSection["selfupdate"] = true; } else { if (settingsSection.ContainsKey("selfupdate")) { settingsSection.Remove("selfupdate"); } }
            if (minifyCheckBox.Checked) { settingsSection["minify"] = true; } else { if (settingsSection.ContainsKey("minify")) { settingsSection.Remove("minify"); } }
            if (clickOnceCheckBox.Checked) { settingsSection["clickonce"] = true; } else { if (settingsSection.ContainsKey("clickonce")) { settingsSection.Remove("clickonce"); } }
            if (webrtcCheckBox.Checked) { settingsSection["webrtc"] = true; } else { if (settingsSection.ContainsKey("webrtc")) { settingsSection.Remove("webrtc"); } }
            if (allowLoginTokenCheckBox.Checked) { settingsSection["allowlogintoken"] = true; } else { if (settingsSection.ContainsKey("allowlogintoken")) { settingsSection.Remove("allowlogintoken"); } }
            if (allowSiteFramingCheckBox.Checked) { settingsSection["allowframing"] = true; } else { if (settingsSection.ContainsKey("allowframing")) { settingsSection.Remove("allowframing"); } }

            // Discovery tab
            if (discoveryNameTextBox.Text.Length > 0) { discoverySection["name"] = discoveryNameTextBox.Text; } else { if (discoverySection.ContainsKey("name")) { discoverySection.Remove("name"); } }
            if (discoveryDescTextBox.Text.Length > 0) { discoverySection["info"] = discoveryDescTextBox.Text; } else { if (discoverySection.ContainsKey("info")) { discoverySection.Remove("info"); } }
            if (discoverySection.Count > 0) { settingsSection["localdiscovery"] = discoverySection; } else { if (settingsSection.ContainsKey("localdiscovery")) { settingsSection.Remove("localdiscovery"); } }

            // Website tab
            if (titleTextBox1.Text.Length > 0) { defaultDomainSection["title"] = titleTextBox1.Text; } else { if (defaultDomainSection.ContainsKey("title")) { defaultDomainSection.Remove("title"); } }
            if (subtitleCheckBox.Checked) { defaultDomainSection["title2"] = titleTextBox2.Text; } else { if (defaultDomainSection.ContainsKey("title2")) { defaultDomainSection.Remove("title2"); } }
            if (footerTextBox.Text.Length > 0) { defaultDomainSection["footer"] = footerTextBox.Text; } else { if (defaultDomainSection.ContainsKey("footer")) { defaultDomainSection.Remove("footer"); } }
            if (!newAccountCheckBox.Checked) { defaultDomainSection["newaccounts"] = false; } else { if (defaultDomainSection.ContainsKey("newaccounts")) { defaultDomainSection.Remove("newaccounts"); } }
            if (newAccountPassTextBox.Text.Length > 0) { defaultDomainSection["newaccountspass"] = newAccountPassTextBox.Text; } else { if (defaultDomainSection.ContainsKey("newaccountspass")) { defaultDomainSection.Remove("newaccountspass"); } }
            if (defaultDomainSection.Count > 0) { domainsSection[""] = defaultDomainSection; } else { if (domainsSection.ContainsKey("")) { domainsSection.Remove(""); } }

            parentConfig["settings"] = settingsSection;
            parentConfig["domains"] = domainsSection;

            // Email tab
            if (emailServerCheckBox.Checked)
            {
                // Setup SMTP
                smtpSection = new Dictionary<string, object>();
                smtpSection.Add("host", smtpHostTextBox.Text);
                smtpSection.Add("port", smtpPortNumericUpDown.Value);
                smtpSection.Add("from", smtpFromTextBox.Text);
                smtpSection.Add("user", smtpUserTextBox.Text);
                smtpSection.Add("pass", smtpPassTextBox.Text);
                smtpSection.Add("tls", smtpTlsCheckBox.Checked);
                parentConfig["smtp"] = smtpSection;
            }
            else
            {
                // Clear SMTP
                if (parentConfig.ContainsKey("smtp")) { parentConfig.Remove("smtp"); }
            }

            // Write the new config.json
            try
            {
                // Let fix the JSON to make it more readable, save the non-fixed version if anything goes wrong.
                string output = new JavaScriptSerializer().Serialize(parentConfig);
                string output2 = null;
                try { output2 = clearJson(output); } catch (Exception ex) { log("clearJson(): " + ex.ToString()); }
                if (output2 == null)
                {
                    File.WriteAllText(configFilePath, output);
                }
                else
                {
                    Dictionary<string, object> test = null;
                    try { test = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(output2); } catch (Exception ex) { log("JavaScriptDeSerializer: " + ex.ToString()); }
                    if (test == null) { File.WriteAllText(configFilePath, output); } else { File.WriteAllText(configFilePath, output2); }
                }
            }
            catch (Exception ex)
            {
                log("Configuration Editor: " + ex.ToString());
                MessageBox.Show(this, ex.ToString(), "Configuration Editor", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        private string clearJson(string json)
        {
            bool escape = false;
            bool quotes = false;
            int identCount = 0;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (escape == true) { escape = false; sb.Append(c); }
                else if ((c == '\"') && (quotes == false)) { quotes = true; sb.Append(c); }
                else if ((c == '\"') && (quotes == true)) { quotes = false; sb.Append(c); }
                else if ((c == '\\')) { escape = true; sb.Append(c); }
                else if (quotes == true) { sb.Append(c); }
                else if (c == '}') { identCount--; sb.Append("\r\n" + identSpaces(identCount) + "}"); }
                else if (c == '{') { identCount++; sb.Append("{\r\n" + identSpaces(identCount)); }
                else if (c == ':') { sb.Append(": "); }
                else if (c == ',') { sb.Append(", \r\n" + identSpaces(identCount)); }
                else { sb.Append(c); }
            }
            return sb.ToString();
        }

        private string identSpaces(int count)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < count; i++) { sb.Append("   "); }
            return sb.ToString();
        }

        private void restartService()
        {
            int waitCount = 0;
            log("restartService()");

            // Get the current service state
            MeshCentralServiceController.Refresh();
            ServiceControllerStatus st = 0;
            try { st = MeshCentralServiceController.Status; } catch (Exception) { }
            if (st == 0)
            {
                displayMessage("Unable to restart MeshCentral service, not installed", 2);
                workerThread = null;
                return;
            }

            // Stop the service
            MeshCentralServiceController.Stop();
            try { st = MeshCentralServiceController.Status; } catch (Exception) { st = 0; }
            while ((st != ServiceControllerStatus.Stopped) && (st != 0) && (waitCount < 10))
            {
                waitCount++;
                Thread.Sleep(3000);
                MeshCentralServiceController.Refresh();
                try { st = MeshCentralServiceController.Status; } catch (Exception) { st = 0; }
            }
            if (st == 0)
            {
                displayMessage("Unable to restart MeshCentral service, not installed", 2);
                workerThread = null;
                return;
            }

            // Start the service
            waitCount = 0;
            MeshCentralServiceController.Start();
            try { st = MeshCentralServiceController.Status; } catch (Exception) { st = 0; }
            while ((st != ServiceControllerStatus.Running) && (st != 0) && (waitCount < 10))
            {
                Thread.Sleep(3000);
                MeshCentralServiceController.Refresh();
                try { st = MeshCentralServiceController.Status; } catch (Exception) { st = 0; }
            }
            if (st == 0)
            {
                displayMessage("Unable to restart MeshCentral service, not installed", 2);
                workerThread = null;
                return;
            }

            // Display outcome
            if (st == ServiceControllerStatus.Running)
            {
                displayMessage("MeshCentral service restarted.", 2);
            }
            else
            {
                displayMessage("Unable to restart MeshCentral service.", 2);
            }
            workerThread = null;
        }

        // This version does not work
        private void restartServiceOld()
        {
            log("restartServiceOld()");

            // Restart the MeshCentral service
            Process process = new Process();
            process.StartInfo.FileName = "C:\\Program Files\\nodejs\\node.exe";
            process.StartInfo.Arguments = "\"" + Path.Combine(ServerInstallPath, "node_modules\\meshcentral\\meshcentral.js") + "\" --restart";
            process.StartInfo.WorkingDirectory = Path.Combine(ServerInstallPath, "node_modules");
            process.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            // Launch the startup process
            bool startSuccess = false;
            log("Launching " + process.StartInfo.FileName + " " + process.StartInfo.Arguments);
            try { startSuccess = process.Start(); } catch (Exception) { }
            if (startSuccess == false) { displayMessage("Can't restart MeshCentral Service (#1).", 2); workerThread = null; return; }
            allOutput = "";
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.OutputDataReceived += Process_OutputDataReceived;
            process.ErrorDataReceived += Process_ErrorDataReceived;
            if (process.WaitForExit(60000) == false) { try { process.Kill(); } catch (Exception) { } displayMessage("Can't restart MeshCentral Service (#2).", 2); workerThread = null; return; }

            displayMessage("MeshCentral Service Restarted.", 2);
            workerThread = null;
        }

        private void updateConfigPanel()
        {
            smtpPassTextBox.Enabled = smtpUserTextBox.Enabled = smtpFromTextBox.Enabled = smtpTlsCheckBox.Enabled = smtpHostTextBox.Enabled = smtpPortNumericUpDown.Enabled = emailServerCheckBox.Checked;
            serverNameTextBox2.Enabled = (serverModeComboBox2.SelectedIndex > 0);

            // General tab
            bool ok = ((serverModeComboBox2.SelectedIndex == 0) || (checkServerName(serverNameTextBox2.Text)));
            if (ok == false) { serverNameTextBox2.BackColor = System.Drawing.Color.MistyRose; } else { serverNameTextBox2.BackColor = windowColor; }

            // Website Tab
            newAccountPassTextBox.Enabled = newAccountCheckBox.Checked;
            titleTextBox2.Enabled = subtitleCheckBox.Checked;

            // Email tab
            if (emailServerCheckBox.Checked && !checkServerName(smtpHostTextBox.Text)) { smtpHostTextBox.BackColor = System.Drawing.Color.MistyRose; ok = false; } else { smtpHostTextBox.BackColor = windowColor; }
            if (emailServerCheckBox.Checked && smtpPortNumericUpDown.Value == 0) { smtpPortNumericUpDown.BackColor = System.Drawing.Color.MistyRose; ok = false; } else { smtpPortNumericUpDown.BackColor = windowColor; }
            if (emailServerCheckBox.Checked && !checkEmail(smtpFromTextBox.Text)) { smtpFromTextBox.BackColor = System.Drawing.Color.MistyRose; ok = false; } else { smtpFromTextBox.BackColor = windowColor; }
            if (emailServerCheckBox.Checked && (smtpUserTextBox.Text.Length == 0)) { smtpUserTextBox.BackColor = System.Drawing.Color.MistyRose; ok = false; } else { smtpUserTextBox.BackColor = windowColor; }
            if (emailServerCheckBox.Checked && (smtpPassTextBox.Text.Length == 0)) { smtpPassTextBox.BackColor = System.Drawing.Color.MistyRose; ok = false; } else { smtpPassTextBox.BackColor = windowColor; }

            button5.Enabled = ok;
        }

        private bool checkServerName(string name)
        {
            if (name.Length == 0) return false;
            string[] nameArray = name.Split('.');
            return (nameArray.Length > 1) && (nameArray[0].Length > 0) && (nameArray[1].Length > 0);
        }
        private bool checkEmail(string name)
        {
            if (name.Length == 0) return false;
            string[] nameArray = name.Split('@');
            return (nameArray.Length > 1) && (nameArray[0].Length > 0) && (checkServerName(nameArray[1]));
        }

        private bool readBool(Dictionary<string, object> dic, string key, bool defaultValue) { if (dic.ContainsKey(key) && dic[key].GetType().Equals(typeof(bool))) { return (bool)dic[key]; } if (dic.ContainsKey(key) && dic[key].GetType().Equals(typeof(int))) { return ((int)dic[key] != 0); } return defaultValue; }
        private string readString(Dictionary<string, object> dic, string key, string defaultValue) { if (dic.ContainsKey(key) && dic[key].GetType().Equals(typeof(string))) { return (string)dic[key]; } return defaultValue; }
        private int readInt(Dictionary<string, object> dic, string key, int defaultValue) { if (dic.ContainsKey(key) && dic[key].GetType().Equals(typeof(int))) { return (int)dic[key]; } return defaultValue; }
        private Dictionary<string, object> readSection(Dictionary<string, object> dic, string key) {
            if (dic.ContainsKey(key) && dic[key].GetType().Equals(typeof(Dictionary<string, object>))) {
                Dictionary<string, object> d1 = (Dictionary<string, object>)dic[key];
                Dictionary<string, object> d2 = new Dictionary<string, object>();
                foreach (string k in d1.Keys) { d2[k.ToLower()] = d1[k]; }
                return d2;
            }
            return new Dictionary<string, object>();
        }

        private void emailServerCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            updateConfigPanel();
        }

        private void serverModeComboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            updateConfigPanel();
        }

        private void button7_Click(object sender, EventArgs e)
        {
            // Edit configuration, get the location of the config.json file
            string existingPath = null;
            RegistryKey myKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Open Source\\MeshCentral2", true);
            if (myKey != null)
            {
                try { existingPath = (string)myKey.GetValue("InstallPath", null); } catch (Exception) { }
                myKey.Close();
            }
            if ((existingPath != null) && Directory.Exists(existingPath) && Directory.Exists(Path.Combine(existingPath, "meshcentral-data")) && File.Exists(Path.Combine(existingPath, "meshcentral-data", "config.json")))
            {
                loadConfiguration(Path.Combine(existingPath, "meshcentral-data", "config.json"));
                setPanel(9);
            }
            else
            {
                MessageBox.Show(this, "Unable to open config.json file.", "Configuration Editor", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void smtpHostTextBox_TextChanged(object sender, EventArgs e)
        {
            updateConfigPanel();
        }

        private void smtpPortNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            updateConfigPanel();
        }

        private void smtpFromTextBox_TextChanged(object sender, EventArgs e)
        {
            updateConfigPanel();
        }

        private void smtpPortNumericUpDown_KeyUp(object sender, KeyEventArgs e)
        {
            updateConfigPanel();
        }

        private void serverNameTextBox2_TextChanged(object sender, EventArgs e)
        {
            updateConfigPanel();
        }

        private void newAccountCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            updateConfigPanel();
        }

        private void siteSettingsHelpPictureBox_Click(object sender, EventArgs e)
        {
            new HelpWebSiteForm().ShowDialog(this);
        }

        private void newAccountHelpPictureBox_Click(object sender, EventArgs e)
        {
            new HelpNewAccountForm().ShowDialog(this);
        }

        private void smtpUserTextBox_TextChanged(object sender, EventArgs e)
        {
            updateConfigPanel();
        }

        private void smtpPassTextBox_TextChanged(object sender, EventArgs e)
        {
            updateConfigPanel();
        }

        private void emailHelpPictureBox_Click(object sender, EventArgs e)
        {
            new HelpEmailForm().ShowDialog(this);
        }

        private void discoveryHelpPictureBox_Click(object sender, EventArgs e)
        {
            new HelpDiscoveryForm().ShowDialog(this);
        }

        private void serverFeaturesHelpPictureBox_Click(object sender, EventArgs e)
        {
            new HelpFeaturesForm().ShowDialog(this);
        }

        private void installationHelpPictureBox_Click(object sender, EventArgs e)
        {
            new HelpServerModeForm().ShowDialog(this);
        }

        private void pictureBox15_Click(object sender, EventArgs e)
        {
            new HelpServerModeForm().ShowDialog(this);
        }

    }
}
