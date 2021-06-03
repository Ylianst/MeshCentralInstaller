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
using System.Collections.Generic;
using System.Windows.Forms;
using System.Web.Script.Serialization;

namespace MeshCentralInstaller
{
    public partial class ConfigEditorForm : Form
    {
        public string configFilePath;
        public Dictionary<string, object> parentConfig = new Dictionary<string, object>();
        public Dictionary<string, object> settingsSection = new Dictionary<string, object>();
        public Dictionary<string, object> domainsSection = new Dictionary<string, object>();
        public Dictionary<string, object> configfilesSection = new Dictionary<string, object>();
        public Dictionary<string, object> letsencryptSection = new Dictionary<string, object>();
        public Dictionary<string, object> peersSection = new Dictionary<string, object>();
        public Dictionary<string, object> smtpSection = new Dictionary<string, object>();

        public ConfigEditorForm(string configFilePath)
        {
            this.configFilePath = configFilePath;
            InitializeComponent();
            Translate.TranslateControl(this);
        }

        private void ConfigEditorForm_Load(object sender, EventArgs e)
        {
            // Read the configuration file and sections
            parentConfig = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(configFilePath));
            settingsSection = readSection(parentConfig, "settings");
            domainsSection = readSection(parentConfig, "domains");
            configfilesSection = readSection(parentConfig, "configfiles");
            letsencryptSection = readSection(parentConfig, "letsencrypt");
            peersSection = readSection(parentConfig, "peers");
            smtpSection = readSection(parentConfig, "smtp");            

            // Read the settings section
            string mongoDb = readString(settingsSection, "mongodb", null);
            string mongoDbCol = readString(settingsSection, "mongodbcol", null);

            bool lanOnly = readBool(settingsSection, "lanonly", false);
            bool wanOnly = readBool(settingsSection, "wanonly", false);
            bool selfupdate = readBool(settingsSection, "selfupdate", false);
            bool minify = readBool(settingsSection, "minify", false);
            bool clickOnce = readBool(settingsSection, "clickonce", false);
            bool allowLoginToken = readBool(settingsSection, "Allowlogintoken", false);
            bool allowFraming = readBool(settingsSection, "allowframing", false);
            bool webRtc = readBool(settingsSection, "webrtc", false);

            int port = readInt(settingsSection, "port", 443);
            int redirPort = readInt(settingsSection, "redirport", 80);

            int sessionTime = readInt(settingsSection, "sessiontime", 0);
            string sessionKey = readString(settingsSection, "sessionkey", null);


        }

        private bool readBool(Dictionary<string, object> dic, string key, bool defaultValue) {
            if (dic.ContainsKey(key) && dic[key].GetType().Equals(typeof(bool))) { return (bool)dic[key]; }
            if (dic.ContainsKey(key) && dic[key].GetType().Equals(typeof(int))) { return ((int)dic[key] != 0); }
            return defaultValue;
        }
        private string readString(Dictionary<string, object> dic, string key, string defaultValue) { if (dic.ContainsKey(key) && dic[key].GetType().Equals(typeof(string))) { return (string)dic[key]; } return defaultValue; }
        private int readInt(Dictionary<string, object> dic, string key, int defaultValue) { if (dic.ContainsKey(key) && dic[key].GetType().Equals(typeof(int))) { return (int)dic[key]; } return defaultValue; }

        private Dictionary<string, object> readSection(Dictionary<string, object> dic, string key) {
            if (dic.ContainsKey(key) && dic[key].GetType().Equals(typeof(Dictionary<string, object>))) { return (Dictionary<string, object>)dic[key]; }
            return new Dictionary<string, object>();
        }
    }
}
