using VoucherCapture.ViewModel;

namespace VoucherCapture.Models
{
    public class VoucherComplete_Model
    {
        public VoucherC_ViewModel Header { get; set; }

        public List<List<VoucherDetail_ViewModel>> Body { get; set; }
    }
}
