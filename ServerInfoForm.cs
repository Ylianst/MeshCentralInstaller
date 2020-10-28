using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
