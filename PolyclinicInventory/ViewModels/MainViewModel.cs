using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using PolyclinicInventory.Models;
using PolyclinicInventory.Services;
using CsvHelper;
using System.IO;
using System.Globalization;
using System.Linq;

namespace PolyclinicInventory.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly DataService _dataService = new DataService();
        private readonly DatabaseService _dbService = new DatabaseService();
        private ObservableCollection<Computer> _computers = new ObservableCollection<Computer>();
        private string _filterText = string.Empty;

        public ObservableCollection<Computer> Computers
        {
            get => _computers;
            set
            {
                _computers = value;
                OnPropertyChanged(nameof(Computers));
            }
        }

        public string FilterText
        {
            get => _filterText;
            set
            {
                _filterText = value;
                OnPropertyChanged(nameof(FilterText));
                ApplyFilter();
            }
        }

        public ICommand RefreshCommand { get; }
        public ICommand ExportCommand { get; }

        public MainViewModel()
        {
            Computers = new ObservableCollection<Computer>(_dbService.GetComputers());
            RefreshCommand = new RelayCommand(Refresh);
            ExportCommand = new RelayCommand(Export);
        }

        private void Refresh()
        {
            var computer = _dataService.CollectData();
            _dbService.SaveComputer(computer);
            Computers = new ObservableCollection<Computer>(_dbService.GetComputers());
            ApplyFilter();
        }

        private void Export()
        {
            try
            {
                using (var writer = new StreamWriter(@"C:\Inventory\export.csv"))
                using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                {
                    csv.WriteRecords(Computers);
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText(@"C:\Inventory\log.txt", $"{DateTime.Now}: Export error: {ex.Message}\n");
            }
        }

        private void ApplyFilter()
        {
            var allComputers = _dbService.GetComputers();
            if (string.IsNullOrWhiteSpace(_filterText))
            {
                Computers = new ObservableCollection<Computer>(allComputers);
            }
            else
            {
                var filtered = allComputers
     .Where(c => (c.Name?.Contains(_filterText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                 (c.IPAddress?.Contains(_filterText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                 (c.Printer?.Contains(_filterText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                 (c.Memory?.Contains(_filterText, StringComparison.OrdinalIgnoreCase) ?? false))
     .ToList();
                Computers = new ObservableCollection<Computer>(filtered);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute)
        {
            _execute = execute;
            _canExecute = () => true;
        }

        public bool CanExecute(object? parameter) => _canExecute();

        public void Execute(object? parameter) => _execute();

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}