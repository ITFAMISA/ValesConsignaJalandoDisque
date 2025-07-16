namespace VoucherCapture.Models
{
    public class Supply_Model
    {
        public int IdSupply { get; set; }

        public string MicrosipKey { get; set; }

        public string Description { get; set; }

        public string UnitType { get; set; }

        public int Inspection {  get; set; }

        public float QtyTotal {  get; set; }

        public int RawMaterial {  get; set; }
    }
}
