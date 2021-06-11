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

using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;

namespace MeshCentralInstaller
{
    public class Translate
    {
        // *** TRANSLATION TABLE START ***
        static private Dictionary<string, Dictionary<string, string>> translationTable = new Dictionary<string, Dictionary<string, string>>() {
        {
            "Install",
            new Dictionary<string, string>() {
                {"de","Installieren"},
                {"hi","इंस्टॉल"},
                {"fr","Installer"},
                {"zh-cht","安裝"},
                {"zh-chs","安装"},
                {"fi","Asenna"},
                {"tr","Yüklemek"},
                {"cs","Instalace"},
                {"ja","インストール"},
                {"es","Instalar"},
                {"pt","Instalar"},
                {"nl","Installeren"},
                {"ko","설치"},
                {"ru","Установка"}
            }
        },
        {
            "Title",
            new Dictionary<string, string>() {
                {"de","Titel"},
                {"hi","शीर्षक"},
                {"fr","Titre"},
                {"zh-cht","標題"},
                {"zh-chs","标题"},
                {"fi","Otsikko"},
                {"tr","Başlık"},
                {"cs","Titul"},
                {"ja","題名"},
                {"es","Título"},
                {"pt","Título"},
                {"nl","Titel"},
                {"ko","표제"},
                {"ru","заглавие"}
            }
        },
        {
            "Confirm Delete",
            new Dictionary<string, string>() {
                {"nl","Verwijderen bevestigen"},
                {"ko","削除を確認"},
                {"fr","Confirmation de la suppression"},
                {"zh-chs","确认删除"},
                {"es","Confirmar eliminación"},
                {"hi","हटाने की पुष्टि करें"},
                {"de","Löschen bestätigen"}
            }
        },
        {
            "Email",
            new Dictionary<string, string>() {
                {"de","E-Mail"},
                {"hi","ईमेल"},
                {"zh-cht","電郵"},
                {"zh-chs","电邮"},
                {"fi","Sähköposti"},
                {"tr","E-posta"},
                {"cs","E-mail"},
                {"ja","Eメール"},
                {"es","Correo electrónico"},
                {"nl","E-mail"},
                {"ko","이메일"}
            }
        },
        {
            "Welcome",
            new Dictionary<string, string>() {
                {"de","Willkommen"},
                {"hi","स्वागत हे"},
                {"fr","Bienvenue"},
                {"zh-cht","歡迎"},
                {"zh-chs","欢迎"},
                {"fi","Tervetuloa"},
                {"tr","Hoşgeldiniz"},
                {"cs","Vítejte"},
                {"ja","ようこそ"},
                {"es","Bienvenido"},
                {"pt","Bem vindo"},
                {"nl","Welkom"},
                {"ko","환영합니다!"},
                {"ru","Добро пожаловать"}
            }
        },
        {
            "Description",
            new Dictionary<string, string>() {
                {"de","Beschreibung"},
                {"hi","विवरण"},
                {"zh-cht","描述"},
                {"zh-chs","描述"},
                {"fi","Kuvaus"},
                {"tr","Açıklama"},
                {"cs","Popis"},
                {"ja","説明"},
                {"es","Descripción"},
                {"pt","Descrição"},
                {"nl","Omschrijving"},
                {"ko","설명"},
                {"ru","Описание"}
            }
        },
        {
            "Scan Network",
            new Dictionary<string, string>() {
                {"de","Netzwerk durchsuchen"},
                {"hi","स्कैन नेटवर्क"},
                {"fr","Scan réseau"},
                {"zh-cht","掃描網絡"},
                {"zh-chs","扫描网络"},
                {"fi","Skannaa verkko"},
                {"tr","Ağı Tara"},
                {"cs","Skenovat síť"},
                {"ja","スキャンネットワーク"},
                {"es","Escanear Red"},
                {"pt","Escaneamento via rede"},
                {"nl","Scan Netwerk"},
                {"ko","네트워크 검색"},
                {"ru","Сканировать сеть"}
            }
        },
        {
            "Cancel",
            new Dictionary<string, string>() {
                {"de","Abbrechen"},
                {"hi","रद्द करना"},
                {"fr","Annuler"},
                {"zh-cht","取消"},
                {"zh-chs","取消"},
                {"fi","Peruuta"},
                {"tr","İptal etmek"},
                {"cs","Storno"},
                {"ja","キャンセル"},
                {"es","Cancelar"},
                {"pt","Cancelar"},
                {"nl","Annuleren"},
                {"ko","취소"},
                {"ru","Отмена"}
            }
        },
        {
            "Back",
            new Dictionary<string, string>() {
                {"de","Zurück"},
                {"hi","वापस"},
                {"fr","Retour"},
                {"zh-cht","返回"},
                {"zh-chs","返回"},
                {"fi","Takaisin"},
                {"tr","Geri"},
                {"cs","Zpět"},
                {"ja","バック"},
                {"es","Atrás"},
                {"pt","Voltar"},
                {"nl","Terug"},
                {"ko","뒤로"},
                {"ru","Назад"}
            }
        },
        {
            "Scan",
            new Dictionary<string, string>() {
                {"de","Scannen"},
                {"hi","स्कैन"},
                {"fr","Analyse"},
                {"zh-cht","掃瞄"},
                {"zh-chs","扫瞄"},
                {"fi","Skannaa"},
                {"tr","Tarama"},
                {"cs","Skenovat"},
                {"ja","スキャン"},
                {"es","Escanear"},
                {"ko","검색"},
                {"ru","Сканировать"}
            }
        },
        {
            "Close",
            new Dictionary<string, string>() {
                {"de","Schließen"},
                {"hi","बंद करे"},
                {"fr","Fermer"},
                {"zh-cht","關"},
                {"zh-chs","关"},
                {"fi","Sulje"},
                {"tr","Kapat"},
                {"cs","Zavřít"},
                {"ja","閉じる"},
                {"es","Cerrar"},
                {"pt","Fechar"},
                {"nl","Sluiten"},
                {"ko","닫기"},
                {"ru","Закрыть"}
            }
        },
        {
            "General",
            new Dictionary<string, string>() {
                {"de","Allgemein"},
                {"hi","सामान्य"},
                {"fr","Général"},
                {"zh-cht","一般"},
                {"zh-chs","一般"},
                {"fi","Yleinen"},
                {"tr","Genel"},
                {"cs","Obecné"},
                {"ja","全般"},
                {"pt","Geral"},
                {"nl","Algemeen"},
                {"ko","일반"},
                {"ru","Сводка"}
            }
        },
        {
            "OK",
            new Dictionary<string, string>() {
                {"hi","ठीक"},
                {"fr","ОК"},
                {"tr","tamam"},
                {"pt","Ok"},
                {"ko","확인"},
                {"ru","ОК"}
            }
        },
        {
            "Next",
            new Dictionary<string, string>() {
                {"nl","Volgende"},
                {"ko","次"},
                {"fr","Suivant"},
                {"zh-chs","下一个"},
                {"es","próximo"},
                {"hi","अगला"},
                {"de","Nächster"}
            }
        },
        {
            "Name",
            new Dictionary<string, string>() {
                {"hi","नाम"},
                {"fr","Nom"},
                {"zh-cht","名稱"},
                {"zh-chs","名称"},
                {"fi","Nimi"},
                {"tr","İsim"},
                {"cs","Jméno/název"},
                {"ja","名"},
                {"es","Nombre"},
                {"pt","Nome"},
                {"nl","Naam"},
                {"ko","이름"},
                {"ru","Имя"}
            }
        },
        {
            "Open Source, Apache 2.0 License",
            new Dictionary<string, string>() {
                {"nl","Open Source, Apache 2.0 Licentie"},
                {"ko","オープンソース、Apache 2.0 ライセンス"},
                {"fr","Open Source, licence Apache 2.0"},
                {"zh-chs","开源，Apache 2.0 许可"},
                {"es","Código abierto, licencia Apache 2.0"},
                {"hi","ओपन सोर्स, अपाचे 2.0 लाइसेंस"},
                {"de","Open Source, Apache 2.0-Lizenz"}
            }
        },
        {
            "Uninstall",
            new Dictionary<string, string>() {
                {"de","Deinstallation"},
                {"hi","स्थापना रद्द करें"},
                {"fr","Désinstaller"},
                {"zh-cht","卸載"},
                {"zh-chs","卸载"},
                {"fi","Asennuksen poistaminen"},
                {"tr","Kaldır"},
                {"cs","Odinstalace"},
                {"ja","アンインストール"},
                {"es","Desinstalar"},
                {"pt","Desinstalar"},
                {"nl","Deïnstallatie"},
                {"ko","설치 제거"},
                {"ru","Удаление"}
            }
        },
        {
            "...",
            new Dictionary<string, string>() {
                {"cs","…"}
            }
        },
        {
            "Refresh",
            new Dictionary<string, string>() {
                {"de","Aktualisieren"},
                {"hi","ताज़ा करना"},
                {"fr","Rafraîchir"},
                {"zh-cht","刷新"},
                {"zh-chs","刷新"},
                {"fi","Päivitä"},
                {"tr","Yenile"},
                {"cs","Načíst znovu"},
                {"ja","リフレッシュ"},
                {"es","Actualizar"},
                {"pt","Atualizar"},
                {"nl","Verversen"},
                {"ko","새로 고침"},
                {"ru","Обновить"}
            }
        }
        };
        // *** TRANSLATION TABLE END ***

        static public string T(string english)
        {
            string lang = Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName;
            if (lang == "en") return english;
            if (translationTable.ContainsKey(english))
            {
                Dictionary<string, string> translations = translationTable[english];
                if (translations.ContainsKey(lang)) return translations[lang];
            }
            return english;
        }

        static public void TranslateControl(Control control)
        {
            control.Text = T(control.Text);
            foreach (Control c in control.Controls) { TranslateControl(c); }
        }

        static public void TranslateContextMenu(ContextMenuStrip menu)
        {
            menu.Text = T(menu.Text);
            foreach (object i in menu.Items) { if (i.GetType() == typeof(ToolStripMenuItem)) { TranslateToolStripMenuItem((ToolStripMenuItem)i); } }
        }

        static public void TranslateToolStripMenuItem(ToolStripMenuItem menu)
        {
            menu.Text = T(menu.Text);
            foreach (object i in menu.DropDownItems)
            {
                if (i.GetType() == typeof(ToolStripMenuItem))
                {
                    TranslateToolStripMenuItem((ToolStripMenuItem)i);
                }
            }
        }

        static public void TranslateListView(ListView listview)
        {
            listview.Text = T(listview.Text);
            foreach (object c in listview.Columns)
            {
                if (c.GetType() == typeof(ColumnHeader))
                {
                    ((ColumnHeader)c).Text = T(((ColumnHeader)c).Text);
                }
            }
        }


    }
}
