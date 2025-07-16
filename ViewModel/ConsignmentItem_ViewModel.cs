namespace VoucherCapture.ViewModel
{
    public class ConsignmentItem_ViewModel
    {
        public int IdSupply { get; set; }
        public string MicrosipKey { get; set; }
        public string Description { get; set; }
        public string UnitType { get; set; }
        public decimal CurrentStock { get; set; }
        public decimal MinimumStock { get; set; }
        public decimal MaximumStock { get; set; }
        public bool IsConsignment { get; set; }
        public string LastUpdate { get; set; }
        public string SupplierName { get; set; }
        public string Status
        {
            get
            {
                if (CurrentStock <= MinimumStock) return "Bajo";
                if (CurrentStock >= MaximumStock) return "Alto";
                return "Normal";
            }
        }
    }
}