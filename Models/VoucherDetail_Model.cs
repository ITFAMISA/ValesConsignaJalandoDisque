namespace VoucherCapture.Models
{
    public class VoucherDetail_Model
    {
        public int IdVoucherDetail {  get; set; }

        public int IdVoucher { get; set; }

        public int IdSupply { get; set; }

        public string UnitType {  get; set; }

        public decimal QtyRequested { get; set; }

        public decimal QtyAuthorized {  get; set; }

        public int IdStorage { get; set; }

        public int IdSupplySurplus { get; set; }
        public bool IsFromConsignment { get; set; }

    }
}
