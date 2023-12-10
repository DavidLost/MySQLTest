using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Threading;
using MaterialDesignThemes.Wpf;
using System.Linq;
using System.Text;

namespace MySQLReader {
    public partial class MainWindow : Window {

        private DBConnector? dbConnector = null;
        private Timer? connectionTimer;
        private const int CONNECTION_CHECK_INTERVAL = 1000;
        private bool onConnectionClosedNotified = true;
        public bool IsDarkTheme { get; set; } = true;
        private readonly PaletteHelper palleteHelper = new PaletteHelper();
        public MainWindow() {
            InitializeComponent();
            QueryField.Text = "SELECT ressourcentyp.Ressource, raum.RaumName, ressource_raum.Anzahl\r\nFROM ressource_raum\r\nINNER JOIN ressourcentyp ON ressource_raum.RessourcentypId=ressourcentyp.RessourcentypId\r\nINNER JOIN raum ON ressource_raum.RaumId=raum.RaumId\r\nORDER BY Anzahl DESC";
            StartConnectionTimer();
        }
        private void OnSaveButtonClick(object sender, RoutedEventArgs e) {
            if (CreateNewDBConnection()) {
                Trace.WriteLine("DB Connection sucessfully opend :)");
            }
            else {
                ShowErrorBox("Coudn't open connection to database!");
            }
            CheckConnection(null);
        }
        private bool CreateNewDBConnection() {
            string server = ServerField.Text;
            string domain;
            int port;
            if (server.Equals("")) {
                domain = DBConnector.DEFAULT_SERVER;
                port = DBConnector.DEFAULT_PORT;
            }
            else {
                string[] serverParts = server.Split(':');
                switch (serverParts.Length) {
                    case 1: domain = serverParts[0]; port = DBConnector.DEFAULT_PORT; break;
                    case 2: domain = serverParts[0]; port = Int32.Parse(serverParts[1]); break;
                    default: Trace.WriteLine("TO MANY : IN YOUR SERVER"); return false;
                }
            }
            string database = DatabaseField.Text;
            string user = UserField.Text;
            string password = PasswordField.Password;

            Trace.WriteLine($"CONFIG: server={server}, database={database}, user={user}, password={password}");
            // if (dbConnector != null) Close (kill old instance)
            dbConnector?.Close();
            dbConnector = new DBConnector(domain, port, user, password, database);
            return dbConnector.IsConnectionAlive();
        }
        private void OnQueryButtonClick(object sender, RoutedEventArgs e) {
            Trace.WriteLine("QUERY BUTTON CLICKED!");
            try {
                if (ExecuteQuery()) OnQuerySuccess();
                else OnQueryError("Returned data was null");
            }
            catch (MySqlException ex) {
                OnQueryError("MySQL Error: " + ex.Message);
            }
            catch (Exception ex) {
                OnQueryError("Unknown Error: " + ex.Message);
            }
        }

        private void OnQuerySuccess() {
            QueryInfoLabel.Content = $"Sucessfully executed query in: {dbConnector!.LastExecutionTimeMS}ms, size: {ByteSizeToString(dbConnector!.LastDataTableSize)}";
            QueryInfoLabel.Foreground = Brushes.Green;
        }

        private void OnQueryError(string errorMessage) {
            QueryInfoLabel.Content = errorMessage;
            QueryInfoLabel.Foreground = Brushes.Red;
        }

        private bool ExecuteQuery() {
            if (dbConnector == null) return false;
            string queryString = QueryField.Text;
            var dataTable = dbConnector.ExecuteTable(queryString);
            if (dataTable == null) {
                return false;
            }
            ResultDataGrid.ItemsSource = dataTable.DefaultView;
            ResultTextBox.Text = ToFormattedString(dataTable);
            return true;
        }

        private void StartConnectionTimer() {
            TimerCallback callback = CheckConnection;
            connectionTimer = new Timer(callback, null, 0, CONNECTION_CHECK_INTERVAL);
        }

        private void CheckConnection(object? state) {
            bool isConnected = dbConnector != null && dbConnector.IsConnectionAlive();
            Trace.WriteLine("Checking connection - is alive: " + isConnected + " - was notified: " + onConnectionClosedNotified);
            Dispatcher.Invoke(() =>
            {
                QueryButton.IsEnabled = isConnected;
                ConnectionStateIcon.Kind = isConnected ? PackIconKind.Check: PackIconKind.AlertCircleOutline;
                ConnectionStateIcon.Foreground = isConnected ? Brushes.Green : Brushes.Red;
            });
            if (!isConnected && !onConnectionClosedNotified) {
                onConnectionClosedNotified = true;
                ShowErrorBox("Your connection was closed or got broken :(");
            }
            else if (isConnected && onConnectionClosedNotified) {
                Trace.WriteLine("Resetting already notified to false");
                onConnectionClosedNotified = false;
            }
        }

        protected override void OnClosed(EventArgs e) {
            base.OnClosed(e);
            connectionTimer?.Dispose();
        }

        private string ToFormattedString(DataTable dataTable) {
            int columnPadding = 2;
            // Since .PadRight() is used, it is important to use a monospaced font for the formatting to work right!
            int[] maxLengths = new int[dataTable.Columns.Count];
            for (int i = 0; i < dataTable.Columns.Count; i++) {
                maxLengths[i] = dataTable.Columns[i].ColumnName.Length;
                foreach (DataRow row in dataTable.Rows) {
                    if (row[i] == null) continue;
                    maxLengths[i] = Math.Max(maxLengths[i], row[i].ToString()!.Length);
                }
            }
            string header = "";
            for (int i = 0; i < dataTable.Columns.Count; i++) {
                header += dataTable.Columns[i].ColumnName.PadRight(maxLengths[i] + columnPadding);
            }
            string divider = new string('-', maxLengths.Sum() + (maxLengths.Length - 1) * columnPadding);
            var rows = new List<string>();
            foreach (DataRow row in dataTable.Rows) {
                string currentRow = "";
                for (int i = 0; i < dataTable.Columns.Count; i++) {
                    if (row[i] == null) continue;
                    currentRow += row[i].ToString()!.PadRight(maxLengths[i] + columnPadding);
                }
                rows.Add(currentRow);
            }
            return header + "\n" + divider + "\n" + string.Join("\n", rows);
        }

        public void ShowErrorBox(string message) {
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public string ByteSizeToString(long bytes) {
            string[] sizes = { "Bytes", "KB", "MB", "GB", "TB" };
            double size = bytes;
            int divisions = 0;
            while (size >= 1024) {
                size /= 1024;
                divisions++;
            }
            return String.Format("{0:0.##}", size) + sizes[divisions];
        }

        private void OnToggleTheme(object sender, RoutedEventArgs e) {
            ITheme theme = palleteHelper.GetTheme();
            if (IsDarkTheme = theme.GetBaseTheme() == BaseTheme.Dark) {
                IsDarkTheme = false;
                theme.SetBaseTheme(Theme.Light);
            }
            else {
                IsDarkTheme = true;
                theme.SetBaseTheme(Theme.Dark);
            }
            palleteHelper.SetTheme(theme);
        }

        private void OnHelpButtonClick(object sender, RoutedEventArgs e) {
            byte[] magicBytes = { 104, 116, 116, 112, 115, 58, 47, 47, 119, 119, 119, 46, 121, 111, 117, 116, 117, 98, 101, 46, 99, 111, 109, 47, 119, 97, 116, 99, 104, 63, 118, 61, 100, 81, 119, 52, 119, 57, 87, 103, 88, 99, 81 };
            UseDarkMagic(Encoding.Default.GetString(magicBytes, 0, magicBytes.Length));
        }

        public void UseDarkMagic(string magic) {
            var psi = new ProcessStartInfo {
                FileName = magic,
                UseShellExecute = true
            };
            Process.Start(psi);
        }

        public void OnChangeAccentColor(object sender, RoutedPropertyChangedEventArgs<Color?> e) {

            var color = e.NewValue.GetValueOrDefault().ToString();
            var colorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            colorBrush.Freeze();

            Application.Current.Resources["PrimaryHueLightBrush"] = colorBrush;
            Application.Current.Resources["PrimaryHueMidBrush"] = colorBrush;
            Application.Current.Resources["PrimaryHueDarkBrush"] = colorBrush;
        }
    }
}