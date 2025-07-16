namespace VoucherCapture.Models
{
    public class CostCenter_Model
    {
        public int IdCostCenter {  get; set; }

        public string Description { get; set; }

        public int IdCostCenterStatus { get; set; }

        public string ExpiredDate { get; set; }

        public int IdCustomer { get; set; }
    }
}
