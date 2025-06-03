using Microsoft.Data.Sqlite;
using PolyclinicInventory.Models;
using System.IO;
using System.Text;

namespace PolyclinicInventory.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString = @"Data Source=C:\Inventory\inventory.db"; // Локальный путь

        public DatabaseService()
        {
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_connectionString.Replace("Data Source=", ""))!);
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                // Проверяем существующую схему
                var checkCommand = connection.CreateCommand();
                checkCommand.CommandText = "PRAGMA table_info(Computers)";
                var reader = checkCommand.ExecuteReader();
                var columns = new List<string>();
                while (reader.Read())
                {
                    columns.Add(reader.GetString(1)); // Имя столбца
                }
                reader.Close();

                // Полная схема таблицы
                var requiredColumns = new[]
  {
    "Id", "Name", "WindowsVersion", "ActivationStatus", "LicenseExpiry",
    "IPAddress", "MACAddress", "Processor", "Monitor", "OfficeStatus",
    "Mouse", "Keyboard", "LastChecked", "Printer", "Memory"
};

                // Если таблица отсутствует или неполная, создаём новую
                if (columns.Count == 0 || !requiredColumns.All(col => columns.Contains(col)))
                {
                    var dropCommand = connection.CreateCommand();
                    dropCommand.CommandText = "DROP TABLE IF EXISTS Computers";
                    dropCommand.ExecuteNonQuery();

                    var createCommand = connection.CreateCommand();
                    createCommand.CommandText = @"
    CREATE TABLE Computers (
        Id INTEGER PRIMARY KEY AUTOINCREMENT,
        Name TEXT,
        WindowsVersion TEXT,
        ActivationStatus TEXT,
        LicenseExpiry TEXT,
        IPAddress TEXT,
        MACAddress TEXT,
        Processor TEXT,
        Monitor TEXT,
        OfficeStatus TEXT,
        Mouse TEXT,
        Keyboard TEXT,
        LastChecked TEXT,
        Printer TEXT,
        Memory TEXT
    )";
                    createCommand.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText(@"C:\Inventory\local_log.txt", $"{DateTime.Now}: Database initialization error: {ex.Message}\n", Encoding.UTF8);
                throw;
            }
        }

        public void SaveComputer(Computer computer)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
    INSERT OR REPLACE INTO Computers (Name, WindowsVersion, ActivationStatus, LicenseExpiry, IPAddress, MACAddress, Processor, Monitor, OfficeStatus, Mouse, Keyboard, LastChecked, Printer, Memory)
    VALUES ($name, $version, $status, $expiry, $ip, $mac, $processor, $monitor, $office, $mouse, $keyboard, $checked, $printer, $memory)";
                command.Parameters.AddWithValue("$memory", computer.Memory ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("$name", computer.Name ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("$version", computer.WindowsVersion ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("$status", computer.ActivationStatus ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("$expiry", computer.LicenseExpiry ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("$ip", computer.IPAddress ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("$mac", computer.MACAddress ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("$processor", computer.Processor ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("$monitor", computer.Monitor ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("$office", computer.OfficeStatus ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("$mouse", computer.Mouse ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("$keyboard", computer.Keyboard ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("$checked", computer.LastChecked ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("$printer", computer.Printer ?? (object)DBNull.Value);
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                File.AppendAllText(@"C:\Inventory\local_log.txt", $"{DateTime.Now}: Save computer error: {ex.Message}\n", Encoding.UTF8);
            }
        }

        public List<Computer> GetComputers()
        {
            var computers = new List<Computer>();
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = "SELECT * FROM Computers";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    computers.Add(new Computer
                    {
                        Id = reader.GetInt32(0),
                        Name = reader.IsDBNull(1) ? null : reader.GetString(1),
                        WindowsVersion = reader.IsDBNull(2) ? null : reader.GetString(2),
                        ActivationStatus = reader.IsDBNull(3) ? null : reader.GetString(3),
                        LicenseExpiry = reader.IsDBNull(4) ? null : reader.GetString(4),
                        IPAddress = reader.IsDBNull(5) ? null : reader.GetString(5),
                        MACAddress = reader.IsDBNull(6) ? null : reader.GetString(6),
                        Processor = reader.IsDBNull(7) ? null : reader.GetString(7),
                        Monitor = reader.IsDBNull(8) ? null : reader.GetString(8),
                        OfficeStatus = reader.IsDBNull(9) ? null : reader.GetString(9),
                        Mouse = reader.IsDBNull(10) ? null : reader.GetString(10),
                        Keyboard = reader.IsDBNull(11) ? null : reader.GetString(11),
                        LastChecked = reader.IsDBNull(12) ? null : reader.GetString(12),
                        Printer = reader.IsDBNull(13) ? null : reader.GetString(13),
                        Memory = reader.IsDBNull(14) ? null : reader.GetString(14)
                    });
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText(@"C:\Inventory\local_log.txt", $"{DateTime.Now}: Get computers error: {ex.Message}\n", Encoding.UTF8);
            }
            return computers;
        }
    }
}