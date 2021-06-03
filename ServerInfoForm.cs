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
using System.Windows.Forms;

namespace MeshCentralInstaller
{
    public partial class ServerInfoForm : Form
    {
        private string name;
        private string url;
        private string desc;
        private string hash;

        public ServerInfoForm(string name, string url, string desc, string hash)
        {
            this.name = name;
            this.url = url;
            this.desc = desc;
            this.hash = hash;
            InitializeComponent();
            Translate.TranslateControl(this);
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void ServerInfoForm_Load(object sender, EventArgs e)
        {
            nameLabel.Text = name;
            urlLabel.Text = url;
            descLabel.Text = desc;
            hashLabel.Text = hash;
        }

        private void urlLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(url);
        }
    }
}
