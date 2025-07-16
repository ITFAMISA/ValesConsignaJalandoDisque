namespace VoucherCapture.Models
{
    public class Voucher_Model
    {
        public string VoucherNumber { get; set; }

        public string CostCenter { get; set; }

        public string RequestedDate { get; set; }

        public string RequestedBy { get; set; }

        public int Imported { get; set; }

        public int Authorized { get; set; }

        public int PickedUp { get; set; }

        public string PickedUpBy { get; set; }

        public string AuthorizedDate { get; set; }

        public int Canceled { get; set; }

        public int RawMaterial { get; set; }

    }
}