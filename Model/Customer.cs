namespace HotelManagement.Model
{
    public class Customer
    {
        public string MaKhachHang { get; set; }
        public string HoTenKhachHang { get; set; }
        public string Email { get; set; }
        public string DienThoai { get; set; }
        public int TongDiem { get; set; }
        public string MaCT { get; set; } // Mã Chương Trình (Point Program)
        public string TenCT { get; set; } // Tên Chương Trình
    }

    public class AddCustomer
    {
        public string HoTenKhachHang { get; set; }
        public string Email { get; set; }
        public string DienThoai { get; set; }
        public string MaCT { get; set; } // Mã Chương Trình (Point Program)
    }
    public class UpdateCustomerNameRequest
    {
        public string HoTenKhachHang { get; set; }
    }
}