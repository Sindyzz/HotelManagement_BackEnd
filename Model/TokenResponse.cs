﻿namespace HotelManagement.Model
{
    public class TokenResponse
    {
        public int MaTaiKhoan { get; set; }
        public string TenTaiKhoan { get; set; }
        public string TenHienThi { get; set; }
        public string Email { get; set; }
        public int MaVaiTro { get; set; }
        public string TenVaiTro { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public DateTime NgayHetHan { get; set; }
    }
}