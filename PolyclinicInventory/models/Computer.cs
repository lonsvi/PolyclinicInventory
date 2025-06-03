namespace PolyclinicInventory.Models
{
    public class Computer
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? WindowsVersion { get; set; }
        public string? ActivationStatus { get; set; }
        public string? LicenseExpiry { get; set; }
        public string? IPAddress { get; set; }
        public string? MACAddress { get; set; }
        public string? Processor { get; set; }
        public string? Monitor { get; set; }
        public string? OfficeStatus { get; set; }
        public string? Mouse { get; set; }
        public string? Keyboard { get; set; }
        public string? Printer { get; set; }
        public string? Memory { get; set; } // Новое поле
        public string? LastChecked { get; set; }
    }
}