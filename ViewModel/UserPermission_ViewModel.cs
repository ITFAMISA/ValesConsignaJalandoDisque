using VoucherCapture.Models;

namespace VoucherCapture.ViewModel
{
    public class UserPermission_ViewModel
    {
        public string EmpNumber { get; set; }

        public string Role { get; set; }

        public string Department { get; set; }

        public List<Permission_Model> Permissions { get; set; }

        public string User {  get; set; }

        public string Email {  get; set; }

        public int matPrimOtros { get; set; }

        public List<int> SelectedPermissions { get; set; } = new();
               

    }
}
