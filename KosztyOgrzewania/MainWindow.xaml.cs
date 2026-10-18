using Microsoft.Data.Sqlite;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace KosztyOgrzewania
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private SqliteConnection connection = new SqliteConnection("Data Source=Dane.db");
        private class Surowiec
        {
            public int Id { get; set; }
            public string Nazwa { get; set; }
            public double Cena { get; set; }
            public double WartoscEnergetyczna { get; set; }
            public string Jednostka { get; set; }
            public double StrataEnergetyczna { get; set; }
        }

        List<Surowiec> surowce = new List<Surowiec>();

        private void createDatabase()
        {
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
            CREATE TABLE IF NOT EXISTS Surowce (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Nazwa TEXT NOT NULL,
                Cena REAL NOT NULL,
                WartoscEnergetyczna REAL NOT NULL,
                Jednostka TEXT NOT NULL,
                StrataEnergetyczna REAL DEFAULT 0.0
            )";
            command.ExecuteNonQuery();

            using var commandLicznik = connection.CreateCommand();
            commandLicznik.CommandText = "SELECT COUNT(*) FROM Surowce";
            long liczbaRekordow = (long)commandLicznik.ExecuteScalar();

            if (liczbaRekordow > 0)
                return; // tabela już ma dane, nic nie wstawiamy

            using var command2 = connection.CreateCommand();
            command2.CommandText = @"
        INSERT INTO Surowce (Nazwa, Cena, WartoscEnergetyczna, Jednostka, StrataEnergetyczna) VALUES
            ('Węgiel kamienny',     1800.0, 6700.0,  't',       30),
            ('Ekogroszek',          1500.0, 7000.0,  't',       25),  
            ('Pellet',              2350.0, 4900.0,  't',       10),
            ('Drewno',              1250.0, 4000.0,  't',       25),  
            ('Olej opałowy',        4650.0, 11900.0, 't',       10),  
            ('Gaz ziemny',          4850.0, 10500.0, 'tys. m³', -10),
            ('Elektryczność',       1050.0, 1000.0,  'MWh',     0)
            ";
            command2.ExecuteNonQuery();
        }
        private void pobierzSurowce()
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Surowce";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                surowce.Add(new Surowiec
                {
                    Id = reader.GetInt32(0),
                    Nazwa = reader.GetString(1),
                    Cena = reader.GetDouble(2),
                    WartoscEnergetyczna = reader.GetDouble(3),
                    Jednostka = reader.GetString(4),
                    StrataEnergetyczna = reader.GetDouble(5)
                });
            }
        }
        private void wypelnjiComboBoxy()
        {
            UzytySurowiecComboBox.ItemsSource = surowce;
            UzytySurowiecComboBox.DisplayMemberPath = "Nazwa";
            UzytySurowiecComboBox.SelectedValuePath = "Id";

            SurowiecPorownawczyComboBox.ItemsSource = surowce;
            SurowiecPorownawczyComboBox.DisplayMemberPath = "Nazwa";
            SurowiecPorownawczyComboBox.SelectedValuePath = "Id";
        }

        private void Window_Closed(object sender, EventArgs e)  // Funkcja automatycznie zamyka połączenie z bazą danych po zamknięciu okna aplikacji.
        {
            connection.Close();
        }
        public MainWindow()
        {
            InitializeComponent();
            createDatabase();
            pobierzSurowce();
            wypelnjiComboBoxy();
            
            
        }
        private void UzytySurowiecComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UzytySurowiecComboBox.SelectedItem is Surowiec wybranySurowiec)
            {
                JednostkaTextBlock.Text = wybranySurowiec.Jednostka;
                PoziomStratSlider.Value = wybranySurowiec.StrataEnergetyczna; 
            }

        }

        private void WyczyscWyniki()
        {
            WynikEnergiaTextBlock.Text = "";
            WynikEnergiaZeStratamiTextBlock.Text = "";
            WynikIloscSurowcaPorownawczegoTextBlock.Text = "";
            WynikKosztSurowcaPorownawczegoTextBlock.Text = "";
        }
        private void Przelicz()
        {
            // Dane z kontrolek
            if (UzytySurowiecComboBox.SelectedItem is not Surowiec uzyty ||
                SurowiecPorownawczyComboBox.SelectedItem is not Surowiec alternatywa)
            {
                WyczyscWyniki();
                return;
            }

            string tekstIlosci = IloscZuzytegoSurowcaTextBox.Text.Replace('.', ',');
            if (!double.TryParse(tekstIlosci, out double ilosc) || ilosc <= 0)
            {
                WyczyscWyniki();
                return;
            }

            double stratyProcent = PoziomStratSlider.Value;

            // Energia z użytego surowca
            double energiaBrutto = ilosc * uzyty.WartoscEnergetyczna;
            double energiaUzyteczna = energiaBrutto * (1 - stratyProcent / 100.0);

            // Ile użytecznej energii daje jednostka surowca porównawczego
            double uzytecznaZJednostki =
                alternatywa.WartoscEnergetyczna * (1 - alternatywa.StrataEnergetyczna / 100.0);

            if (uzytecznaZJednostki <= 0)
            {
                WyczyscWyniki();
                WynikIloscSurowcaPorownawczegoTextBlock.Text =
                    "Surowiec porównawczy nie daje użytecznej energii.";
                return;
            }

            double iloscAlternatywy = energiaUzyteczna / uzytecznaZJednostki;
            double koszt = iloscAlternatywy * alternatywa.Cena;

            // Wyniki
            WynikEnergiaTextBlock.Text =
                $"Energia ze spalenia: {energiaBrutto:N0} kWh";
            WynikEnergiaZeStratamiTextBlock.Text =
                $"Energia po stratach ({stratyProcent:0}%): {energiaUzyteczna:N0} kWh";
            WynikIloscSurowcaPorownawczegoTextBlock.Text =
                $"Potrzeba ({alternatywa.Nazwa}): {iloscAlternatywy:N2} {alternatywa.Jednostka}";
            WynikKosztSurowcaPorownawczegoTextBlock.Text =
                $"Koszt: {koszt:N2} zł";
        }
        private void SurowiecPorownawczyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Przelicz();
        }

        private void ObliczButton_Click(object sender, RoutedEventArgs e)
        {
            Przelicz();
        }


    }
}