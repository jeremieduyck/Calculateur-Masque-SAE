using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;

namespace Calculateur_Masque_Sous_Reseau
{
    public partial class MainWindow : Window
    {
        // Mon premier test avec Git pour la SAE
        private bool _cidrManuel = false;

        public MainWindow()
        {
            InitializeComponent();
            txtIP.Text = "192.168.1.1";
            txtCIDR.Text = "24";
            txtIP.TextChanged += TxtIP_TextChanged;
            txtCIDR.TextChanged += TxtCIDR_TextChanged;
            SuggereCIDR("192.168.1.1");
        }

        private void TxtCIDR_TextChanged(object sender, TextChangedEventArgs e)
        {
            string ipText = txtIP.Text.Trim();
            if (!string.IsNullOrWhiteSpace(ipText) && IPAddress.TryParse(ipText, out IPAddress? ip) && ip != null)
            {
                string cidrAuto = GetCIDRParDefaut(ip.GetAddressBytes()[0]).ToString();
                _cidrManuel = txtCIDR.Text.Trim() != cidrAuto;
            }
        }

        private void TxtIP_TextChanged(object sender, TextChangedEventArgs e)
        {
            string ipText = txtIP.Text.Trim();
            if (!_cidrManuel)
                SuggereCIDR(ipText);
        }

        private void SuggereCIDR(string ipText)
        {
            if (!IPAddress.TryParse(ipText, out IPAddress? ipAddress) || ipAddress is null ||
                ipAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                return;

            byte premier = ipAddress.GetAddressBytes()[0];
            int cidrAuto = GetCIDRParDefaut(premier);

            txtCIDR.TextChanged -= TxtCIDR_TextChanged;
            txtCIDR.Text = cidrAuto.ToString();
            txtCIDR.TextChanged += TxtCIDR_TextChanged;
            _cidrManuel = false;
        }

        private int GetCIDRParDefaut(byte premier)
        {
            if (premier <= 126) return 8;
            if (premier <= 191) return 16;
            if (premier <= 223) return 24;
            return 24;
        }

        private void BtnCalculer_Click(object sender, RoutedEventArgs e)
        {
            lblErreur.Text = "";
            ResetResultats();

            string ipText = txtIP.Text.Trim();

            if (string.IsNullOrWhiteSpace(ipText))
            {
                lblErreur.Text = "Veuillez saisir une adresse IPv4.";
                return;
            }

            string[] parts = ipText.Split('.');
            if (parts.Length != 4)
            {
                lblErreur.Text = "Adresse IPv4 invalide : 4 octets attendus.";
                return;
            }

            foreach (string part in parts)
            {
                if (!int.TryParse(part, out int octet) || octet < 0 || octet > 255)
                {
                    lblErreur.Text = $"Octet invalide « {part} » : valeur entre 0 et 255.";
                    return;
                }
                if (part.Length > 1 && part[0] == '0')
                {
                    lblErreur.Text = $"Octet invalide « {part} » : pas de zero en tete.";
                    return;
                }
            }

            if (!IPAddress.TryParse(ipText, out IPAddress? ipAddress) || ipAddress is null ||
                ipAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            {
                lblErreur.Text = "Adresse IPv4 invalide (format attendu : X.X.X.X).";
                return;
            }

            string cidrText = txtCIDR.Text.Trim();

            if (string.IsNullOrWhiteSpace(cidrText))
            {
                lblErreur.Text = "Veuillez saisir un masque CIDR (0-32).";
                return;
            }

            if (!int.TryParse(cidrText, out int cidr) || cidr < 0 || cidr > 32)
            {
                lblErreur.Text = "Masque CIDR invalide : valeur entiere entre 0 et 32.";
                return;
            }

            byte[] bytes = ipAddress.GetAddressBytes();
            string classe = GetClass(bytes[0]);

            string erreurClasse = ValiderCIDRSelonClasse(classe, cidr);
            if (erreurClasse != string.Empty)
            {
                lblErreur.Text = erreurClasse;
                return;
            }

            string avertissement = GetAvertissementRFC5735(bytes);
            if (avertissement != string.Empty)
                lblErreur.Text = "Avertissement : " + avertissement;

            uint ip = (uint)bytes[0] << 24 | (uint)bytes[1] << 16 |
                         (uint)bytes[2] << 8 | bytes[3];
            uint mask = cidr == 0 ? 0u : 0xFFFFFFFFu << (32 - cidr);
            uint net = ip & mask;
            uint bcast = net | ~mask;

            lblClasseVal.Text = classe;
            lblNetVal.Text = ToDot(net);
            lblBroadcastVal.Text = ToDot(bcast);

            if (cidr <= 30)
            {
                lblFirstIPVal.Text = ToDot(net + 1);
                lblLastIPVal.Text = ToDot(bcast - 1);
                lblNbMachinesVal.Text = ((1U << (32 - cidr)) - 2).ToString("N0");
            }
            else if (cidr == 31)
            {
                lblFirstIPVal.Text = ToDot(net);
                lblLastIPVal.Text = ToDot(bcast);
                lblNbMachinesVal.Text = "2 (lien P2P, RFC 3021)";
            }
            else
            {
                lblFirstIPVal.Text = ToDot(net);
                lblLastIPVal.Text = ToDot(bcast);
                lblNbMachinesVal.Text = "1 (hote unique /32)";
            }

            lblNbIPsVal.Text = ((ulong)1 << (32 - cidr)).ToString("N0");
            lblIpBinaireVal.Text = ToBin(ip);
            lblMasqueBinaireVal.Text = ToBin(mask);
            lblMasqueOctetsVal.Text = ToDot(mask);
            lblIpHexaVal.Text = "0x" + ip.ToString("X8");
        }

        private void ResetResultats()
        {
            lblClasseVal.Text = lblNetVal.Text = lblBroadcastVal.Text =
            lblFirstIPVal.Text = lblLastIPVal.Text = lblNbIPsVal.Text =
            lblNbMachinesVal.Text = lblIpBinaireVal.Text = lblIpHexaVal.Text =
            lblMasqueBinaireVal.Text = lblMasqueOctetsVal.Text = "-";
        }

        private string GetClass(byte premier)
        {
            if (premier == 0) return "A (reseau 0.x.x.x reserve)";
            if (premier <= 126) return "A";
            if (premier == 127) return "Loopback (127.x.x.x)";
            if (premier <= 191) return "B";
            if (premier <= 223) return "C";
            if (premier <= 239) return "D (Multicast)";
            return "E (Experimental)";
        }

        private string ValiderCIDRSelonClasse(string classe, int cidr)
        {
            char lettre = classe.Length > 0 ? char.ToUpper(classe[0]) : '?';

            switch (lettre)
            {
                case 'A':
                    if (cidr < 8) return $"CIDR /{cidr} invalide pour une classe A : minimum /8.";
                    if (cidr > 30) return $"CIDR /{cidr} invalide : maximum /30 pour avoir au moins 2 hotes.";
                    break;
                case 'B':
                    if (cidr < 16) return $"CIDR /{cidr} invalide pour une classe B : minimum /16.";
                    if (cidr > 30) return $"CIDR /{cidr} invalide : maximum /30 pour avoir au moins 2 hotes.";
                    break;
                case 'C':
                    if (cidr < 24) return $"CIDR /{cidr} invalide pour une classe C : minimum /24.";
                    if (cidr > 30) return $"CIDR /{cidr} invalide : maximum /30 pour avoir au moins 2 hotes.";
                    break;
                case 'L':
                    return "Adresse de loopback (127.x.x.x) : non routee, calcul non pertinent.";
                case 'D':
                    return "Adresse multicast (classe D) : pas de masque sous-reseau applicable.";
                case 'E':
                    return "Adresse experimentale (classe E) : usage reserve, non routable.";
            }

            return string.Empty;
        }

        private string GetAvertissementRFC5735(byte[] b)
        {
            uint ip = (uint)b[0] << 24 | (uint)b[1] << 16 | (uint)b[2] << 8 | b[3];

            if (b[0] == 10)
                return "Adresse privee RFC 1918 (10.0.0.0/8) - non routable sur Internet.";
            if (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                return "Adresse privee RFC 1918 (172.16.0.0/12) - non routable sur Internet.";
            if (b[0] == 192 && b[1] == 168)
                return "Adresse privee RFC 1918 (192.168.0.0/16) - non routable sur Internet.";
            if (b[0] == 169 && b[1] == 254)
                return "Adresse APIPA / lien-local (169.254.0.0/16) - RFC 3927.";
            if (b[0] == 192 && b[1] == 0 && b[2] == 2)
                return "Adresse de documentation TEST-NET-1 (192.0.2.0/24) - RFC 5737.";
            if (b[0] == 198 && b[1] == 51 && b[2] == 100)
                return "Adresse de documentation TEST-NET-2 (198.51.100.0/24) - RFC 5737.";
            if (b[0] == 203 && b[1] == 0 && b[2] == 113)
                return "Adresse de documentation TEST-NET-3 (203.0.113.0/24) - RFC 5737.";
            if (b[0] == 100 && (b[1] & 0xC0) == 64)
                return "Adresse CGN (100.64.0.0/10) - RFC 6598, usage operateur.";
            if (b[0] == 192 && b[1] == 0 && b[2] == 0)
                return "Adresse IETF Protocol Assignments (192.0.0.0/24) - RFC 5736.";
            if (b[0] == 198 && (b[1] == 18 || b[1] == 19))
                return "Adresse de test de performance (198.18.0.0/15) - RFC 2544.";
            if (b[0] >= 240)
                return "Adresse reservee classe E (240.0.0.0/4) - RFC 1112.";
            if (ip == 0xFFFFFFFF)
                return "Broadcast limite (255.255.255.255) - non routable.";
            if (b[0] == 0)
                return "Reseau courant (0.0.0.0/8) - RFC 1122, usage limite.";

            return string.Empty;
        }

        private string ToDot(uint val) =>
            $"{(val >> 24) & 0xFF}.{(val >> 16) & 0xFF}.{(val >> 8) & 0xFF}.{val & 0xFF}";

        private string ToBin(uint val) =>
            $"{OctetBin(val, 24)}.{OctetBin(val, 16)}.{OctetBin(val, 8)}.{OctetBin(val, 0)}";

        private string OctetBin(uint v, int shift) =>
            Convert.ToString((v >> shift) & 0xFF, 2).PadLeft(8, '0');
    }
}