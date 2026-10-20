using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace AtletikaiRajtszamok
{
    public partial class MainWindow : Window
    {
        private const string Fejlec = "Rajtszam;Nev;Versenyszam;Nem;Eletkor;OlimpiaiCsucs;VBCsucs;EBCsucs";

        public MainWindow()
        {
            InitializeComponent();
        }

        // ---------- II/1 Életkor visszajelzése ----------
        private void Eletkor_Valtozott(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // A Slider eseménye az ablak építése közben is lefuthat, amikor a TextBlock még nem létezik.
            if (txtEletkor == null)
                return;

            txtEletkor.Text = (int)sldEletkor.Value + " év";
        }

        // ---------- II/2-3 Regisztráció ----------
        private void Regisztral_Click(object sender, RoutedEventArgs e)
        {
            string nev = txtNev.Text.Trim();

            if (nev.Length == 0)
            {
                Figyelmeztet("A versenyző nevének megadása kötelező.");
                return;
            }
            if (nev.Contains(';') || nev.Contains('|'))
            {
                Figyelmeztet("A névben pontosvessző és függőleges vonal nem használható.");
                return;
            }

            ComboBoxItem kijelolt = cmbVersenyszam.SelectedItem as ComboBoxItem;
            if (kijelolt == null)
            {
                Figyelmeztet("Válasszon versenyszámot.");
                return;
            }
            string versenyszam = kijelolt.Content.ToString();

            if (rbFerfi.IsChecked != true && rbNo.IsChecked != true)
            {
                Figyelmeztet("Adja meg a versenyző nemét.");
                return;
            }
            string nem = rbFerfi.IsChecked == true ? "F" : "N";

            HashSet<int> foglalt = FoglaltRajtszamok();

            string rajtszamSzoveg = txtRajtszam.Text.Trim();
            int rajtszam;
            if (rajtszamSzoveg.Length > 0)
            {
                if (!int.TryParse(rajtszamSzoveg, NumberStyles.None, CultureInfo.InvariantCulture, out rajtszam)
                    || rajtszam < 1 || rajtszam > 9999)
                {
                    Figyelmeztet("A rajtszám 1 és 9999 közötti egész szám lehet.");
                    return;
                }
                if (foglalt.Contains(rajtszam))
                {
                    Figyelmeztet("A kért rajtszám már foglalt. Adjon meg másikat, vagy hagyja üresen a mezőt.");
                    return;
                }
            }
            else
            {
                rajtszam = 0;
                for (int i = 1; i <= 9999; i++)
                {
                    if (!foglalt.Contains(i))
                    {
                        rajtszam = i;
                        break;
                    }
                }
                if (rajtszam == 0)
                {
                    Figyelmeztet("Nincs több szabad rajtszám.");
                    return;
                }
            }

            int eletkor = (int)sldEletkor.Value;
            string sor = string.Join(" | ", new string[]
            {
                rajtszam.ToString(CultureInfo.InvariantCulture),
                nev,
                versenyszam,
                nem,
                eletkor.ToString(CultureInfo.InvariantCulture),
                chkOlimpiai.IsChecked == true ? "1" : "0",
                chkVB.IsChecked == true ? "1" : "0",
                chkEB.IsChecked == true ? "1" : "0"
            });

            lstVersenyzok.Items.Add(sor);
            LetszamFrissit();

            MessageBox.Show("A regisztráció sikeres. Kiosztott rajtszám: " + rajtszam,
                "Regisztráció", MessageBoxButton.OK, MessageBoxImage.Information);

            txtNev.Clear();
            txtRajtszam.Clear();
        }

        // ---------- II/4 Fájlba mentés ----------
        private void Mentes_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog
            {
                FileName = "atletika.csv",
                Filter = "CSV-fájl (*.csv)|*.csv"
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                List<string> sorok = new List<string> { Fejlec };
                foreach (object elem in lstVersenyzok.Items)
                {
                    string[] mezok = elem.ToString().Split('|').Select(m => m.Trim()).ToArray();
                    sorok.Add(string.Join(";", mezok));
                }

                File.WriteAllLines(dialog.FileName, sorok, new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                MessageBox.Show("A mentés nem sikerült: " + ex.Message,
                    "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ---------- II/5 Fájlból betöltés ----------
        private void Betoltes_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "CSV-fájl (*.csv)|*.csv"
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                string[] sorok = File.ReadAllLines(dialog.FileName, Encoding.UTF8);

                List<string> ujElemek = new List<string>();
                HashSet<string> latottRajtszamok = new HashSet<string>();

                // az első sor a fejléc, azt kihagyjuk
                for (int i = 1; i < sorok.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(sorok[i]))
                        continue;

                    string[] mezok = sorok[i].Split(';').Select(m => m.Trim()).ToArray();

                    if (!latottRajtszamok.Add(mezok[0]))
                    {
                        MessageBox.Show("A fájlban ismétlődő rajtszám található: " + mezok[0]
                            + ". A betöltés megszakadt, a korábbi lista megmaradt.",
                            "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    ujElemek.Add(string.Join(" | ", mezok));
                }

                // csak sikeres beolvasás után cseréljük a régi listát
                lstVersenyzok.Items.Clear();
                foreach (string elem in ujElemek)
                    lstVersenyzok.Items.Add(elem);

                LetszamFrissit();
            }
            catch (Exception ex)
            {
                MessageBox.Show("A betöltés nem sikerült: " + ex.Message,
                    "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ---------- Segédmetódusok ----------
        private HashSet<int> FoglaltRajtszamok()
        {
            HashSet<int> foglalt = new HashSet<int>();
            foreach (object elem in lstVersenyzok.Items)
            {
                string elso = elem.ToString().Split('|')[0].Trim();
                int szam;
                if (int.TryParse(elso, NumberStyles.None, CultureInfo.InvariantCulture, out szam))
                    foglalt.Add(szam);
            }
            return foglalt;
        }

        private void LetszamFrissit()
        {
            txtLetszam.Text = "Regisztrált versenyzők: " + lstVersenyzok.Items.Count;
        }

        private static void Figyelmeztet(string uzenet)
        {
            MessageBox.Show(uzenet, "Figyelmeztetés", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
