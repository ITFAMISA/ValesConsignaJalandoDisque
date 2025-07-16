namespace VoucherCapture.ViewModel
{
    public class VoucherDetail_ViewModel
    {
        public int IdVoucherDetail {  get; set; }

        public int IdVoucher {  get; set; }

        public string Supply { get; set; }

        public float QtyRequested { get; set; }

        public float QtyAuthorized { get; set; }

        public string UnitType { get; set; }

        public string MicrosipKey { get; set; }

        public int IdSupply { get; set; }

        public int IdRequestStatus { get; set; }

        public List<Storage_ViewModel> Storages { get; set; }       
    }
}
