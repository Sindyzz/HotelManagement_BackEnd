﻿namespace HotelManagement.Model
{
    public class Account
    {
        public int MaTaiKhoan { get; set; }
        public string TenTaiKhoan { get; set; }
        public string MatKhau { get; set; }
        public string TenHienThi { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public int MaVaiTro { get; set; }
        public string TenVaiTro { get; set; }
    }

    public class AddAccount
    {
        public string TenTaiKhoan { get; set; }
        public string MatKhau { get; set; }
        public string TenHienThi { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public int MaVaiTro { get; set; }
    }
}