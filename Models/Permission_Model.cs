namespace VoucherCapture.Models
{
    public class Permission_Model
    {
        public int IdPermission { get; set; }

        public string Name { get; set; }

        public int Status { get; set; }

        public List<int> SelectedPermissions { get; set; } = new();

        public Dictionary<int, string> OperacionalOptions { get; set; } = new();

        public int? OpcionSeleccionada { get; set; }

    }
}
