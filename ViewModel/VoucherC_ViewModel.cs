namespace VoucherCapture.ViewModel
{
    public class VoucherC_ViewModel
    {
        public int IdRequestSignature {  get; set; }

        public int IdVoucher { get; set; }

        public string Cc { get; set; }

        public string PickedUpBy { get; set; }

        public Boolean PickedUp {  get; set; }

        public string RequestedBy { get; set; }

        public string RequestedDate { get; set; }

        public string AuthorizedBy { get; set; }

        public string AuthorizationDate { get; set; }

        public string RequestStatus { get; set; }

        public Boolean Imported { get; set; }

        public string Storage {  get; set; }

        public string Concept { get; set; }

        public string Comment {  get; set; }

        public int Canceled { get; set; }

        public string VoucherNumber { get; set; }

        public int RawMaterial { get; set; }

        public int idVoucherDetail { get; set; }
    }
}
