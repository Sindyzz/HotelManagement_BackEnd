﻿﻿using HotelManagement.DataReader;
using HotelManagement.Model;
using System.Globalization;

namespace HotelManagement.Services
{
    public interface ICustomerRepository
    {
        Task<(ApiResponse<IEnumerable<Customer>> Items, int TotalCount)> GetAllAsync(
            int pageNumber, int pageSize, string? searchTerm = null, string? sortBy = "MaKhachHang", string? sortOrder = "ASC");
        Task<ApiResponse<Customer>> GetByIdAsync(string id);
        Task<ApiResponse<Customer>> CreateAsync(AddCustomer addCustomer);

        Task<ApiResponse<bool>> UpdateAsync(Customer customer);
        Task<ApiResponse<bool>> UpdateCustomerNameAsync(int maTaiKhoan, string hoTenKhachHang);
        Task<ApiResponse<bool>> UpdateCustomerNameByCustomerIdAsync(string maKhachHang, string hoTenKhachHang);

        Task<ApiResponse<bool>> DeleteAsync(string id);
        Task<ApiResponse<bool>> IsEmailExistsAsync(string email);
        Task<ApiResponse<bool>> IsPhoneExistsAsync(string phone);
        Task<ApiResponse<bool>> AddPointsAsync(string customerId, int points);
        Task<ApiResponse<bool>> AccumulatePointsAsync(string maKhachHang, decimal thanhTien);
        Task<ApiResponse<decimal>> UsePointsAsync(string maKhachHang, int soDiemSuDung);
        Task<ApiResponse<PointProgram>> GetPointProgramByIdAsync(string maCT);
        Task<ApiResponse<CustomerPointsInfo>> GetCustomerPointsInfoAsync(string maKhachHang);

    }

    public class CustomerRepository : ICustomerRepository
    {
        private readonly DatabaseDapper _db;

        public CustomerRepository(DatabaseDapper db)
        {
            _db = db;
        }

        public async Task<(ApiResponse<IEnumerable<Customer>> Items, int TotalCount)> GetAllAsync(
            int pageNumber, int pageSize, string? searchTerm = null, string? sortBy = "MaKhachHang", string? sortOrder = "ASC")
        {
            try
            {
                using var reader = await _db.QueryMultipleAsync("sp_Customer_GetAll",
                    new
                    {
                        PageNumber = pageNumber,
                        PageSize = pageSize,
                        SearchTerm = searchTerm,
                        SortBy = sortBy,
                        SortOrder = sortOrder
                    });

                var items = (await reader.ReadAsync<Customer>()).ToList();
                var totalCount = await reader.ReadSingleAsync<int>();

                return (ApiResponse<IEnumerable<Customer>>.SuccessResponse(items, "Lấy danh sách khách hàng thành công"), totalCount);
            }
            catch (Exception ex)
            {
                return (ApiResponse<IEnumerable<Customer>>.ErrorResponse($"Lỗi khi lấy danh sách khách hàng: {ex.Message}"), 0);
            }
        }
        public async Task<ApiResponse<Customer>> CreateAsync(AddCustomer addCustomer)
        {
            try
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(addCustomer.HoTenKhachHang))
                    return ApiResponse<Customer>.ErrorResponse("Họ tên khách hàng không được để trống");

                if (string.IsNullOrWhiteSpace(addCustomer.Email))
                    return ApiResponse<Customer>.ErrorResponse("Email không được để trống");

                if (string.IsNullOrWhiteSpace(addCustomer.DienThoai))
                    return ApiResponse<Customer>.ErrorResponse("Số điện thoại không được để trống");

                // Kiểm tra email đã tồn tại
                var emailExists = await IsEmailExistsAsync(addCustomer.Email);
                if (emailExists.Success && emailExists.Data)
                    return ApiResponse<Customer>.ErrorResponse("Email đã tồn tại trong hệ thống");

                // Kiểm tra số điện thoại đã tồn tại
                var phoneExists = await IsPhoneExistsAsync(addCustomer.DienThoai);
                if (phoneExists.Success && phoneExists.Data)
                    return ApiResponse<Customer>.ErrorResponse("Số điện thoại đã tồn tại trong hệ thống");

                // Kiểm tra chương trình điểm nếu có
                if (!string.IsNullOrWhiteSpace(addCustomer.MaCT))
                {
                    var pointProgram = await GetPointProgramByIdAsync(addCustomer.MaCT);
                    if (!pointProgram.Success)
                        return ApiResponse<Customer>.ErrorResponse("Chương trình điểm không tồn tại");
                }

                // Thực hiện insert vào database (MaKhachHang sẽ tự động tăng)
                var parameters = new
                {
                    HoTenKhachHang = addCustomer.HoTenKhachHang,
                    Email = addCustomer.Email,
                    DienThoai = addCustomer.DienThoai,
                    TongDiem = 0, // Mặc định 0 điểm
                    MaCT = addCustomer.MaCT ?? ""
                };

                // Sử dụng stored procedure trả về MaKhachHang vừa tạo
                var newCustomerId = await _db.QueryFirstOrDefaultStoredProcedureAsync<string>("sp_Customer_Create", parameters);
                if (string.IsNullOrEmpty(newCustomerId))
                    return ApiResponse<Customer>.ErrorResponse("Tạo khách hàng thất bại");

                // Lấy thông tin khách hàng vừa tạo để trả về
                var createdCustomer = await GetByIdAsync(newCustomerId);
                if (!createdCustomer.Success)
                    return ApiResponse<Customer>.ErrorResponse("Tạo khách hàng thành công nhưng không thể lấy thông tin");

                return ApiResponse<Customer>.SuccessResponse(createdCustomer.Data, "Tạo khách hàng thành công");
            }
            catch (Exception ex)
            {
                return ApiResponse<Customer>.ErrorResponse($"Lỗi khi tạo khách hàng: {ex.Message}");
            }
        }
        public async Task<ApiResponse<Customer>> GetByIdAsync(string id)
        {
            try
            {
                var customer = await _db.QueryFirstOrDefaultStoredProcedureAsync<Customer>("sp_Customer_GetById", new { MaKhachHang = id });
                if (customer == null)
                    return ApiResponse<Customer>.ErrorResponse("Không tìm thấy khách hàng");

                return ApiResponse<Customer>.SuccessResponse(customer, "Lấy thông tin khách hàng thành công");
            }
            catch (Exception ex)
            {
                return ApiResponse<Customer>.ErrorResponse($"Lỗi khi lấy thông tin khách hàng: {ex.Message}");
            }
        }

        public async Task<ApiResponse<bool>> UpdateAsync(Customer customer)
        {
            try
            {
                var parameters = new
                {
                    customer.MaKhachHang,
                    customer.HoTenKhachHang,
                    customer.Email,
                    customer.DienThoai,
                    customer.MaCT
                };

                int rowsAffected = await _db.ExecuteStoredProcedureAsync("sp_Customer_Update", parameters);
                if (rowsAffected <= 0)
                    return ApiResponse<bool>.ErrorResponse("Cập nhật khách hàng thất bại");

                return ApiResponse<bool>.SuccessResponse(true, "Cập nhật khách hàng thành công");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.ErrorResponse($"Lỗi khi cập nhật khách hàng: {ex.Message}");
            }
        }
        public async Task<ApiResponse<bool>> UpdateCustomerNameAsync(int maTaiKhoan, string hoTenKhachHang)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(hoTenKhachHang))
                {
                    Console.WriteLine("Họ tên khách hàng không hợp lệ");
                    return ApiResponse<bool>.ErrorResponse("Họ tên khách hàng không hợp lệ");
                }

                var parameters = new
                {
                    MaTaiKhoan = maTaiKhoan,
                    HoTenKhachHang = hoTenKhachHang
                };

                Console.WriteLine($"Cập nhật họ tên khách hàng cho MaTaiKhoan: {maTaiKhoan}");
                var rowsAffected = await _db.ExecuteAsync(
                    "UPDATE Customer SET HoTenKhachHang = @HoTenKhachHang WHERE MaTaiKhoan = @MaTaiKhoan",
                    parameters);

                if (rowsAffected <= 0)
                {
                    Console.WriteLine("Cập nhật họ tên thất bại: Không tìm thấy khách hàng");
                    return ApiResponse<bool>.ErrorResponse("Không tìm thấy khách hàng");
                }

                Console.WriteLine("Cập nhật họ tên thành công");
                return ApiResponse<bool>.SuccessResponse(true, "Cập nhật họ tên thành công");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi cập nhật họ tên: {ex.Message}");
                return ApiResponse<bool>.ErrorResponse($"Lỗi khi cập nhật họ tên: {ex.Message}");
            }
        }
        public async Task<ApiResponse<bool>> UpdateCustomerNameByCustomerIdAsync(string maKhachHang, string hoTenKhachHang)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(hoTenKhachHang))
                {
                    Console.WriteLine("Họ tên khách hàng không hợp lệ");
                    return ApiResponse<bool>.ErrorResponse("Họ tên khách hàng không hợp lệ");
                }

                var parameters = new
                {
                    MaKhachHang = maKhachHang,
                    HoTenKhachHang = hoTenKhachHang
                };

                Console.WriteLine($"Cập nhật họ tên khách hàng cho MaKhachHang: {maKhachHang}");
                var rowsAffected = await _db.ExecuteAsync(
                    "UPDATE Customer SET HoTenKhachHang = @HoTenKhachHang WHERE MaKhachHang = @MaKhachHang",
                    parameters);

                if (rowsAffected <= 0)
                {
                    Console.WriteLine("Cập nhật họ tên thất bại: Không tìm thấy khách hàng");
                    return ApiResponse<bool>.ErrorResponse("Không tìm thấy khách hàng");
                }

                Console.WriteLine("Cập nhật họ tên thành công");
                return ApiResponse<bool>.SuccessResponse(true, "Cập nhật họ tên thành công");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi cập nhật họ tên: {ex.Message}");
                return ApiResponse<bool>.ErrorResponse($"Lỗi khi cập nhật họ tên: {ex.Message}");
            }
        }
        public async Task<ApiResponse<bool>> DeleteAsync(string id)
        {
            try
            {
                int rowsAffected = await _db.ExecuteStoredProcedureAsync("sp_Customer_Delete", new { MaKhachHang = id });
                if (rowsAffected <= 0)
                    return ApiResponse<bool>.ErrorResponse("Xóa khách hàng thất bại");

                return ApiResponse<bool>.SuccessResponse(true, "Xóa khách hàng thành công");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.ErrorResponse($"Lỗi khi xóa khách hàng: {ex.Message}");
            }
        }

        public async Task<ApiResponse<bool>> IsEmailExistsAsync(string email)
        {
            try
            {
                var customer = await _db.QueryFirstOrDefaultAsync<Customer>(
                    "SELECT TOP 1 MaKhachHang FROM Customer WHERE Email = @Email",
                    new { Email = email });

                return ApiResponse<bool>.SuccessResponse(customer != null, customer != null ? "Email đã tồn tại" : "Email hợp lệ");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.ErrorResponse($"Lỗi khi kiểm tra email: {ex.Message}");
            }
        }

        public async Task<ApiResponse<bool>> IsPhoneExistsAsync(string phone)
        {
            try
            {
                var customer = await _db.QueryFirstOrDefaultAsync<Customer>(
                    "SELECT TOP 1 MaKhachHang FROM Customer WHERE DienThoai = @DienThoai",
                    new { DienThoai = phone });

                return ApiResponse<bool>.SuccessResponse(customer != null, customer != null ? "Số điện thoại đã tồn tại" : "Số điện thoại hợp lệ");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.ErrorResponse($"Lỗi khi kiểm tra số điện thoại: {ex.Message}");
            }
        }

        public async Task<ApiResponse<bool>> AddPointsAsync(string customerId, int points)
        {
            try
            {
                int rowsAffected = await _db.ExecuteStoredProcedureAsync("sp_Customer_AddPoints",
                    new { MaKhachHang = customerId, SoDiemThem = points });

                if (rowsAffected <= 0)
                    return ApiResponse<bool>.ErrorResponse("Thêm điểm thất bại");

                return ApiResponse<bool>.SuccessResponse(true, "Thêm điểm thành công");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.ErrorResponse($"Lỗi khi thêm điểm: {ex.Message}");
            }
        }
        public async Task<ApiResponse<bool>> AccumulatePointsAsync(string maKhachHang, decimal thanhTien)
        {
            try
            {
                if (!int.TryParse(maKhachHang, out int maKhachHangInt))
                {
                    Console.WriteLine($"Mã khách hàng không hợp lệ: {maKhachHang}");
                    return ApiResponse<bool>.ErrorResponse("Mã khách hàng không hợp lệ");
                }

                var customer = await _db.QueryFirstOrDefaultStoredProcedureAsync<Customer>("sp_Customer_GetById", new { MaKhachHang = maKhachHangInt });
                if (customer == null)
                {
                    Console.WriteLine($"Không tìm thấy khách hàng với MaKhachHang: {maKhachHangInt}");
                    return ApiResponse<bool>.ErrorResponse("Không tìm thấy khách hàng");
                }
                Console.WriteLine($"Tìm thấy khách hàng: MaKhachHang = {maKhachHangInt}, MaCT = {customer.MaCT}");

                // Gọi stored procedure sp_ThanhToanVaTichDiem để tích điểm và nâng cấp hạng
                var result = await _db.QueryFirstOrDefaultStoredProcedureAsync<dynamic>(
                    "sp_ThanhToanVaTichDiem",
                    new
                    {
                        MaKhachHang = maKhachHangInt,
                        ThanhTien = thanhTien,
                        SoDiemSuDung = 0 // Không sử dụng điểm trong trường hợp này
                    });

                if (result == null)
                {
                    Console.WriteLine($"Tích điểm thất bại: MaKhachHang = {maKhachHangInt}, ThanhTien = {thanhTien}");
                    return ApiResponse<bool>.ErrorResponse("Tích điểm thất bại");
                }

                Console.WriteLine($"Tích điểm thành công: MaKhachHang = {maKhachHangInt}, DiemTichLuy = {result.DiemTichLuy}, SoTienGiam = {result.SoTienGiam}");

                return ApiResponse<bool>.SuccessResponse(true, "Tích điểm thành công");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi tích điểm cho MaKhachHang {maKhachHang}: {ex.Message}");
                return ApiResponse<bool>.ErrorResponse($"Lỗi khi tích điểm: {ex.Message}");
            }
        }
        public async Task<ApiResponse<decimal>> UsePointsAsync(string maKhachHang, int soDiemSuDung)
        {
            try
            {
                if (!int.TryParse(maKhachHang, out int maKhachHangInt))
                {
                    Console.WriteLine($"Mã khách hàng không hợp lệ: {maKhachHang}");
                    return ApiResponse<decimal>.ErrorResponse("Mã khách hàng không hợp lệ");
                }

                if (soDiemSuDung <= 0)
                {
                    Console.WriteLine($"Số điểm sử dụng không hợp lệ: {soDiemSuDung}");
                    return ApiResponse<decimal>.ErrorResponse("Số điểm sử dụng không hợp lệ");
                }

                var customer = await _db.QueryFirstOrDefaultStoredProcedureAsync<Customer>("sp_Customer_GetById", new { MaKhachHang = maKhachHangInt });
                if (customer == null)
                {
                    Console.WriteLine($"Không tìm thấy khách hàng với MaKhachHang: {maKhachHangInt}");
                    return ApiResponse<decimal>.ErrorResponse("Không tìm thấy khách hàng");
                }
                Console.WriteLine($"Tìm thấy khách hàng: MaKhachHang = {maKhachHangInt}, TongDiem = {customer.TongDiem}");

                // Kiểm tra điểm đủ dùng
                if (customer.TongDiem < soDiemSuDung)
                {
                    Console.WriteLine($"Không đủ điểm: TongDiem = {customer.TongDiem}, SoDiemSuDung = {soDiemSuDung}");
                    return ApiResponse<decimal>.ErrorResponse("Không đủ điểm để sử dụng");
                }

                var pointProgram = await _db.QueryFirstOrDefaultStoredProcedureAsync<PointProgram>("sp_PointProgram_GetById", new { MaCT = customer.MaCT });
                if (pointProgram == null)
                {
                    Console.WriteLine($"Không tìm thấy chương trình điểm với MaCT: {customer.MaCT}");
                    return ApiResponse<decimal>.ErrorResponse("Không tìm thấy chương trình điểm");
                }
                Console.WriteLine($"Tìm thấy chương trình điểm: MaCT = {customer.MaCT}, MucGiamGia = {pointProgram.MucGiamGia.ToString(CultureInfo.InvariantCulture)}");

                // Tính số tiền giảm
                decimal soTienGiam = soDiemSuDung * pointProgram.MucGiamGia;
                Console.WriteLine($"Tính tiền giảm: SoDiemSuDung = {soDiemSuDung}, MucGiamGia = {pointProgram.MucGiamGia}, SoTienGiam = {soTienGiam}");

                // Trừ điểm
                int rowsAffected = await _db.ExecuteStoredProcedureAsync("sp_Customer_AddPoints",
                    new { MaKhachHang = maKhachHangInt, SoDiemThem = -soDiemSuDung }); // Trừ điểm
                if (rowsAffected <= 0)
                {
                    Console.WriteLine($"Cập nhật TongDiem thất bại: MaKhachHang = {maKhachHangInt}, RowsAffected = {rowsAffected}");
                    return ApiResponse<decimal>.ErrorResponse("Cập nhật điểm thất bại");
                }
                Console.WriteLine($"Cập nhật TongDiem thành công: MaKhachHang = {maKhachHangInt}, RowsAffected = {rowsAffected}");

                // Ghi lịch sử giao dịch
                await _db.ExecuteStoredProcedureAsync("sp_PointHistory_Create",
                    new
                    {
                        MaKhachHang = maKhachHangInt,
                        SoDiem = soDiemSuDung,
                        NgayGiaoDich = DateTime.Now,
                        LoaiGiaoDich = "Use"
                    });
                Console.WriteLine($"Ghi PointHistory thành công: MaKhachHang = {maKhachHangInt}, SoDiem = {soDiemSuDung}, LoaiGiaoDich = Use");

                return ApiResponse<decimal>.SuccessResponse(soTienGiam, "Sử dụng điểm thành công");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi sử dụng điểm cho MaKhachHang {maKhachHang}: {ex.Message}");
                return ApiResponse<decimal>.ErrorResponse($"Lỗi khi sử dụng điểm: {ex.Message}");
            }
        }
        public async Task<ApiResponse<PointProgram>> GetPointProgramByIdAsync(string maCT)
        {
            try
            {
                if (!int.TryParse(maCT, out int maCTInt))
                {
                    Console.WriteLine($"Mã chương trình không hợp lệ: {maCT}");
                    return ApiResponse<PointProgram>.ErrorResponse("Mã chương trình không hợp lệ");
                }

                var pointProgram = await _db.QueryFirstOrDefaultStoredProcedureAsync<PointProgram>(
                    "sp_PointProgram_GetById", new { MaCT = maCTInt });
                if (pointProgram == null)
                {
                    Console.WriteLine($"Không tìm thấy chương trình điểm với MaCT: {maCT}");
                    return ApiResponse<PointProgram>.ErrorResponse("Không tìm thấy chương trình điểm");
                }

                Console.WriteLine($"Tìm thấy chương trình điểm: MaCT = {maCT}, MucGiamGia = {pointProgram.MucGiamGia}");
                return ApiResponse<PointProgram>.SuccessResponse(pointProgram, "Lấy thông tin chương trình điểm thành công");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi lấy chương trình điểm: {ex.Message}");
                return ApiResponse<PointProgram>.ErrorResponse($"Lỗi khi lấy chương trình điểm: {ex.Message}");
            }
        }
        public async Task<ApiResponse<CustomerPointsInfo>> GetCustomerPointsInfoAsync(string maKhachHang)
        {
            try
            {
                if (!int.TryParse(maKhachHang, out int maKhachHangInt))
                    return ApiResponse<CustomerPointsInfo>.ErrorResponse("Mã khách hàng không hợp lệ");

                var customerInfo = await _db.QueryFirstOrDefaultAsync<CustomerPointsInfo>(
                    "SELECT * FROM customer_diem_view WHERE MaKhachHang = @MaKhachHang",
                    new { MaKhachHang = maKhachHangInt });

                if (customerInfo == null)
                    return ApiResponse<CustomerPointsInfo>.ErrorResponse("Không tìm thấy thông tin khách hàng");

                return ApiResponse<CustomerPointsInfo>.SuccessResponse(customerInfo, "Lấy thông tin điểm khách hàng thành công");
            }
            catch (Exception ex)
            {
                return ApiResponse<CustomerPointsInfo>.ErrorResponse($"Lỗi khi lấy thông tin điểm khách hàng: {ex.Message}");
            }
        }
    }
}