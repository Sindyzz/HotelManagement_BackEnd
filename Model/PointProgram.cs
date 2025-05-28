namespace HotelManagement.Model
{
    public class PointProgram
    {
        public int MaCT { get; set; }
        public string TenCT { get; set; }
        public int DiemToiThieu { get; set; }
        public decimal MucGiamGia { get; set; }
        public decimal TyLeTichDiem { get; set; } =0.0m;
    }
}