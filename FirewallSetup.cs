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
using NetFwTypeLib;

namespace MeshCentralInstaller
{
    public class FirewallSetup
    {
        private const string CLSID_FIREWALL_MANAGER = "{304CE942-6E39-40D8-943A-B913C40C9CD4}";
        private const string PROGID_AUTHORIZED_APPLICATION = "HNetCfg.FwAuthorizedApplication"; 

        private static NetFwTypeLib.INetFwMgr GetFirewallManager()
        {
            try
            {
                Type objectType = Type.GetTypeFromCLSID(new Guid(CLSID_FIREWALL_MANAGER));
                return Activator.CreateInstance(objectType) as NetFwTypeLib.INetFwMgr;
            }
            catch (Exception) { return null; }
        }

        public static bool DetectFirewall()
        {
            INetFwMgr manager = GetFirewallManager();
            if (manager == null) return false;
            return manager.LocalPolicy.CurrentProfile.FirewallEnabled;
        }

        public static int AddRules(string tcpports, string udpports)
        {
            INetFwRule firewallRule1 = null;
            INetFwRule firewallRule2 = null;

            if ((tcpports != null) && (tcpports != ""))
            {
                try { firewallRule1 = (INetFwRule)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FWRule")); } catch (System.Runtime.InteropServices.COMException) { return 1; }
                firewallRule1.Action = NET_FW_ACTION_.NET_FW_ACTION_ALLOW;
                firewallRule1.Description = "Allow inbound TCP ports for MeshCentral (HTTP, HTTPS, MPS)";
                firewallRule1.Direction = NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_IN;
                firewallRule1.Enabled = true;
                firewallRule1.InterfaceTypes = "All";
                firewallRule1.Protocol = 6; // TCP
                firewallRule1.LocalPorts = tcpports; // "80,443,4433"; // HTTP, HTTPS, MPS
                firewallRule1.Name = "MeshCentral Server TCP ports";
            }

            if ((udpports != null) && (udpports != ""))
            {
                try { firewallRule2 = (INetFwRule)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FWRule")); } catch (System.Runtime.InteropServices.COMException) { return 1; }
                firewallRule2.Action = NET_FW_ACTION_.NET_FW_ACTION_ALLOW;
                firewallRule2.Description = "Allow inbound UDP ports for MeshCentral";
                firewallRule2.Direction = NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_IN;
                firewallRule2.Enabled = true;
                firewallRule2.InterfaceTypes = "All";
                firewallRule2.Protocol = 17; // UDP
                firewallRule2.LocalPorts = udpports; //  "8081"; // Agent/server network discovery
                firewallRule2.Name = "MeshCentral Server UDP ports";
            }
            
            INetFwPolicy2 firewallPolicy = (INetFwPolicy2)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2"));
            try
            {
                if (firewallRule1 != null) { firewallPolicy.Rules.Add(firewallRule1); }
                if (firewallRule2 != null) { firewallPolicy.Rules.Add(firewallRule2); }
            }
            catch (System.UnauthorizedAccessException) { return 2; }

            return 0;
        }

        public static int RemoveRules()
        {
            INetFwPolicy2 firewallPolicy = null;
            try { firewallPolicy = (INetFwPolicy2)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2")); } catch (System.Exception) { return 1; }
            if (firewallPolicy == null) { return 1; }
            try { firewallPolicy.Rules.Remove("MeshCentral Server TCP ports"); } catch (System.Exception) { }
            try { firewallPolicy.Rules.Remove("MeshCentral Server TCP ports"); } catch (System.Exception) { }
            try { firewallPolicy.Rules.Remove("MeshCentral Server TCP ports"); } catch (System.Exception) { }
            try { firewallPolicy.Rules.Remove("MeshCentral Server UDP ports"); } catch (System.Exception) { }
            try { firewallPolicy.Rules.Remove("MeshCentral Server UDP ports"); } catch (System.Exception) { }
            try { firewallPolicy.Rules.Remove("MeshCentral Server UDP ports"); } catch (System.Exception) { }
            firewallPolicy = null;
            return 0;
        }

        public static int SetupFirewall(string tcpports, string udpports)
        {
            RemoveRules();
            System.Threading.Thread.Sleep(2000);
            return AddRules(tcpports, udpports);
        }

        public static int ClearFirewall()
        {
            return RemoveRules();
        }

    }
}
