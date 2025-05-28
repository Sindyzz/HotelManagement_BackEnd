using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using HotelManagement.DataReader;
using HotelManagement.Model;
using HotelManagement.Utilities;
using System.Security.Cryptography;

namespace HotelManagement.Services
{
    public interface IAuthRepository
    {
        Task<ApiResponse<TokenResponse>> LoginAsync(LoginRequest request);
        Task<ApiResponse<int>> RegisterAsync(RegisterRequest request);
        Task<ApiResponse<TokenResponse>> RefreshTokenAsync(RefreshTokenRequest request);
        Task<ApiResponse<bool>> VerifyOtpAsync(OtpVerificationRequest request);
        Task<ApiResponse<bool>> ResendOtpAsync(OtpResendRequest request);
    }
    public class AuthRepository : IAuthRepository
    {
        private readonly DatabaseDapper _db;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;
        private readonly int _accessTokenExpiryMinutes = 60; // 1 giờ
        private readonly int _refreshTokenExpiryDays = 7; // 7 ngày

        public AuthRepository(DatabaseDapper db, IConfiguration configuration, IEmailService emailService)
        {
            _db = db;
            _configuration = configuration;
            _emailService = emailService;
        }


        // Phương thức mới để tạo RefreshToken
        private string GenerateRefreshToken()
        {
            var randomNumber = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomNumber);
                return Convert.ToBase64String(randomNumber);
            }
        }
        public async Task<ApiResponse<TokenResponse>> LoginAsync(LoginRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.TenTaiKhoan) || string.IsNullOrEmpty(request.MatKhau))
                {
                    Console.WriteLine("Tên tài khoản hoặc mật khẩu trống");
                    return ApiResponse<TokenResponse>.ErrorResponse("Tên tài khoản và mật khẩu không được để trống");
                }

                Console.WriteLine($"Đang tìm tài khoản với TenTaiKhoan: {request.TenTaiKhoan}");
                // Sử dụng SP thay vì truy vấn trực tiếp
                var account = await _db.QueryFirstOrDefaultStoredProcedureAsync<dynamic>(
                    "sp_Login",
                    new { TenTaiKhoan = request.TenTaiKhoan });

                if (account == null)
                {
                    Console.WriteLine($"Không tìm thấy tài khoản với TenTaiKhoan: {request.TenTaiKhoan}");
                    return ApiResponse<TokenResponse>.ErrorResponse("Tên đăng nhập hoặc mật khẩu không đúng");
                }

                Console.WriteLine($"Tài khoản tìm thấy: MaTaiKhoan = {account.MaTaiKhoan}, Hash lưu: {account.MatKhau}");
                if (!PasswordHasher.VerifyPassword(request.MatKhau, account.MatKhau))
                {
                    Console.WriteLine($"Xác minh mật khẩu thất bại cho TenTaiKhoan: {request.TenTaiKhoan}");
                    return ApiResponse<TokenResponse>.ErrorResponse("Tên đăng nhập hoặc mật khẩu không đúng");
                }

                Console.WriteLine($"Kiểm tra OTP cho MaTaiKhoan: {account.MaTaiKhoan}");
                // Sử dụng EXISTS thay vì trả về boolean
                var otpCount = await _db.QueryFirstOrDefaultAsync<int>(
                    "SELECT COUNT(1) FROM OtpVerification WHERE MaTaiKhoan = @MaTaiKhoan",
                    new { MaTaiKhoan = account.MaTaiKhoan });

                if (otpCount > 0)
                {
                    Console.WriteLine($"Tài khoản chưa xác minh OTP: MaTaiKhoan = {account.MaTaiKhoan}");
                    return ApiResponse<TokenResponse>.ErrorResponse("Tài khoản chưa được kích hoạt. Vui lòng xác minh OTP.");
                }

                var user = new TokenResponse
                {
                    MaTaiKhoan = account.MaTaiKhoan,
                    TenTaiKhoan = account.TenTaiKhoan,
                    TenHienThi = account.TenHienThi,
                    Email = account.Email,
                    Phone = account.Phone,
                    MaVaiTro = account.MaVaiTro
                };

                var accessToken = GenerateJwtToken(user);
                var refreshToken = GenerateRefreshToken();
                var expiresIn = _accessTokenExpiryMinutes * 60;

                Console.WriteLine($"Lưu RefreshToken cho MaTaiKhoan: {user.MaTaiKhoan}");

                try
                {
                    var result = await _db.ExecuteStoredProcedureAsync("sp_UserToken_Create", new
                    {
                        MaTaiKhoan = user.MaTaiKhoan,
                        AccessToken = accessToken,
                        RefreshToken = refreshToken,
                        NgayHetHan = DateTime.UtcNow.AddDays(_refreshTokenExpiryDays)
                    });
                    Console.WriteLine("Lưu RefreshToken thành công");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lỗi khi lưu RefreshToken: {ex.Message}");
                    // Kiểm tra xem token đã được lưu chưa trước khi chèn lại
                    var tokenExists = await _db.QueryFirstOrDefaultAsync<int>(
                        "SELECT COUNT(1) FROM UserToken WHERE MaTaiKhoan = @MaTaiKhoan AND AccessToken = @AccessToken",
                        new { MaTaiKhoan = user.MaTaiKhoan, AccessToken = accessToken });

                    if (tokenExists == 0)
                    {
                        await _db.ExecuteAsync(@"
            INSERT INTO UserToken (MaTaiKhoan, AccessToken, RefreshToken, NgayHetHan) 
            VALUES (@MaTaiKhoan, @AccessToken, @RefreshToken, @NgayHetHan)",
                            new
                            {
                                MaTaiKhoan = user.MaTaiKhoan,
                                AccessToken = accessToken,
                                RefreshToken = refreshToken,
                                NgayHetHan = DateTime.UtcNow.AddDays(_refreshTokenExpiryDays)
                            });
                        Console.WriteLine("Lưu RefreshToken trực tiếp thành công");
                    }
                    else
                    {
                        Console.WriteLine("Token đã được lưu bởi stored procedure, bỏ qua chèn trực tiếp");
                    }
                }

                user.AccessToken = accessToken;
                user.RefreshToken = refreshToken;
                user.ExpiresIn = expiresIn;
                Console.WriteLine($"Đăng nhập thành công: MaTaiKhoan = {user.MaTaiKhoan}");
                return ApiResponse<TokenResponse>.SuccessResponse(user, "Đăng nhập thành công");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi đăng nhập: {ex.Message}");
                return ApiResponse<TokenResponse>.ErrorResponse($"Lỗi khi đăng nhập: {ex.Message}");
            }
        }
        private string GenerateJwtToken(TokenResponse user)
        {
            var secret = _configuration["Jwt:SecretKey"];
            var issuer = _configuration["Jwt:Issuer"];
            var audience = _configuration["Jwt:Audience"];

            if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(issuer) || string.IsNullOrEmpty(audience))
            {
                Console.WriteLine("Cấu hình JWT bị thiếu hoặc không hợp lệ");
                throw new ArgumentNullException("Cấu hình JWT (SecretKey, Issuer, hoặc Audience) bị thiếu");
            }

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.MaTaiKhoan.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Name, user.TenTaiKhoan),
                new Claim(ClaimTypes.Role, user.MaVaiTro.ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_accessTokenExpiryMinutes),
                signingCredentials: creds);

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
            Console.WriteLine($"Tạo JWT token thành công cho MaTaiKhoan: {user.MaTaiKhoan}");
            return tokenString;
        }

        // Phần còn lại của RegisterAsync và ValidateRegisterRequest giữ nguyên
        public async Task<ApiResponse<int>> RegisterAsync(RegisterRequest request)
        {
            if (!ValidateRegisterRequest(request, out string errorMessage))
            {
                Console.WriteLine($"Lỗi xác thực đăng ký: {errorMessage}");
                return ApiResponse<int>.ErrorResponse(errorMessage);
            }

            try
            {
                // Kiểm tra TenTaiKhoan và Email tồn tại
                var usernameExists = await _db.QueryFirstOrDefaultAsync<bool>(
                    "SELECT 1 FROM Account WHERE TenTaiKhoan = @TenTaiKhoan", new { request.TenTaiKhoan });
                if (usernameExists)
                    return ApiResponse<int>.ErrorResponse("Tên tài khoản đã tồn tại");

                var emailExists = await _db.QueryFirstOrDefaultAsync<bool>(
                    "SELECT 1 FROM Account WHERE Email = @Email", new { request.Email });
                if (emailExists)
                    return ApiResponse<int>.ErrorResponse("Email đã tồn tại");

                // Mã hóa mật khẩu
                var hashedPassword = PasswordHasher.HashPassword(request.MatKhau);

                // Lưu tài khoản
                var parameters = new
                {
                    request.TenTaiKhoan,
                    MatKhau = hashedPassword,
                    request.TenHienThi,
                    request.Email,
                    request.Phone
                };

                Console.WriteLine($"Gọi sp_Account_InsertWithTempStatus với TenTaiKhoan: {request.TenTaiKhoan}");
                var result = await _db.QueryFirstOrDefaultStoredProcedureAsync<dynamic>("sp_Account_InsertWithTempStatus", parameters);
                if (result != null && result.MaTaiKhoan > 0)
                {
                    // Tạo OTP
                    string otpCode = GenerateOtp();
                    DateTime createdAt = DateTime.UtcNow;
                    DateTime expiresAt = createdAt.AddMinutes(2);

                    // Lưu OTP vào OtpVerification
                    await _db.ExecuteStoredProcedureAsync("sp_OtpVerification_Insert", new
                    {
                        MaTaiKhoan = result.MaTaiKhoan,
                        OtpCode = otpCode,
                        request.Email,
                        CreatedAt = createdAt,
                        ExpiresAt = expiresAt
                    });

                    // Gửi OTP qua email
                    await _emailService.SendEmailAsync(request.Email, "Xác minh OTP", $"Mã OTP của bạn là: {otpCode}. Hiệu lực trong 2 phút.");

                    Console.WriteLine($"Đăng ký thành công: MaTaiKhoan = {result.MaTaiKhoan}, OTP gửi tới {request.Email}");
                    return ApiResponse<int>.SuccessResponse((int)result.MaTaiKhoan, "Đăng ký thành công, vui lòng kiểm tra email để lấy OTP");
                }

                Console.WriteLine("Đăng ký thất bại");
                return ApiResponse<int>.ErrorResponse("Đăng ký thất bại");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi đăng ký: {ex.Message}");
                return ApiResponse<int>.ErrorResponse($"Lỗi khi đăng ký: {ex.Message}");
            }
        }
        public async Task<ApiResponse<bool>> VerifyOtpAsync(OtpVerificationRequest request)
        {
            try
            {
                var otp = await _db.QueryFirstOrDefaultStoredProcedureAsync<OtpVerification>("sp_OtpVerification_GetByAccountId", new { request.MaTaiKhoan });
                if (otp == null)
                    return ApiResponse<bool>.ErrorResponse("Không tìm thấy OTP");

                if (otp.ExpiresAt < DateTime.UtcNow)
                    return ApiResponse<bool>.ErrorResponse("Mã OTP đã hết hạn");

                if (otp.OtpCode != request.OtpCode)
                    return ApiResponse<bool>.ErrorResponse("Mã OTP không đúng");

                // Xóa OTP để đánh dấu tài khoản đã kích hoạt
                var rowsAffected = await _db.ExecuteAsync("DELETE FROM OtpVerification WHERE MaTaiKhoan = @MaTaiKhoan", new { request.MaTaiKhoan });
                if (rowsAffected <= 0)
                    return ApiResponse<bool>.ErrorResponse("Xác minh OTP thất bại");

                return ApiResponse<bool>.SuccessResponse(true, "Xác minh OTP thành công");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.ErrorResponse($"Lỗi khi xác minh OTP: {ex.Message}");
            }
        }

        public async Task<ApiResponse<bool>> ResendOtpAsync(OtpResendRequest request)
        {
            try
            {
                var otp = await _db.QueryFirstOrDefaultStoredProcedureAsync<OtpVerification>("sp_OtpVerification_GetByAccountId", new { request.MaTaiKhoan });
                if (otp == null)
                    return ApiResponse<bool>.ErrorResponse("Không tìm thấy OTP");

                // Tạo OTP mới
                string newOtpCode = GenerateOtp();
                DateTime createdAt = DateTime.UtcNow;
                DateTime expiresAt = createdAt.AddMinutes(2);

                // Cập nhật OTP
                await _db.ExecuteStoredProcedureAsync("sp_OtpVerification_Insert", new
                {
                    request.MaTaiKhoan,
                    OtpCode = newOtpCode,
                    request.Email,
                    CreatedAt = createdAt,
                    ExpiresAt = expiresAt
                });

                // Gửi OTP mới qua email
                await _emailService.SendEmailAsync(request.Email, "Xác minh OTP", $"Mã OTP mới của bạn là: {newOtpCode}. Hiệu lực trong 2 phút.");

                return ApiResponse<bool>.SuccessResponse(true, "Gửi lại OTP thành công");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.ErrorResponse($"Lỗi khi gửi lại OTP: {ex.Message}");
            }
        }

        private string GenerateOtp()
        {
            return RandomNumberGenerator.GetInt32(100000, 999999).ToString("D6");
        }

        private bool ValidateRegisterRequest(RegisterRequest request, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (string.IsNullOrEmpty(request.TenTaiKhoan) || request.TenTaiKhoan.Length < 5)
            {
                errorMessage = "Tên tài khoản phải có ít nhất 5 ký tự";
                return false;
            }

            if (string.IsNullOrEmpty(request.MatKhau) || request.MatKhau.Length < 6)
            {
                errorMessage = "Mật khẩu phải có ít nhất 6 ký tự";
                return false;
            }

            if (string.IsNullOrEmpty(request.Email) || !System.Text.RegularExpressions.Regex.IsMatch(request.Email, @"^[a-zA-Z0-9._%+-]+@gmail\.com$"))
            {
                errorMessage = "Email phải có định dạng @gmail.com";
                return false;
            }

            if (string.IsNullOrEmpty(request.Phone) || !System.Text.RegularExpressions.Regex.IsMatch(request.Phone, @"^\d{10}$"))
            {
                errorMessage = "Số điện thoại phải có 10 chữ số";
                return false;
            }

            return true;
        }
        public async Task<ApiResponse<TokenResponse>> RefreshTokenAsync(RefreshTokenRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.RefreshToken))
                {
                    Console.WriteLine("RefreshToken trống");
                    return ApiResponse<TokenResponse>.ErrorResponse("RefreshToken không hợp lệ");
                }

                // Kiểm tra RefreshToken trong UserToken
                var tokenInfo = await _db.QueryFirstOrDefaultStoredProcedureAsync<dynamic>(
                    "sp_UserToken_GetByRefreshToken",
                    new { RefreshToken = request.RefreshToken });

                if (tokenInfo == null || tokenInfo.MaTaiKhoan <= 0)
                {
                    Console.WriteLine("RefreshToken không tồn tại hoặc không hợp lệ");
                    return ApiResponse<TokenResponse>.ErrorResponse("RefreshToken không hợp lệ");
                }

                if (tokenInfo.NgayHetHan < DateTime.UtcNow)
                {
                    Console.WriteLine("RefreshToken đã hết hạn");
                    return ApiResponse<TokenResponse>.ErrorResponse("RefreshToken đã hết hạn");
                }

                // Lấy thông tin tài khoản
                var user = await _db.QueryFirstOrDefaultStoredProcedureAsync<TokenResponse>(
                    "sp_Login_GetById",
                    new { MaTaiKhoan = tokenInfo.MaTaiKhoan });

                if (user == null || user.MaTaiKhoan <= 0)
                {
                    Console.WriteLine("Không tìm thấy tài khoản");
                    return ApiResponse<TokenResponse>.ErrorResponse("Tài khoản không tồn tại");
                }

                // Tạo token mới
                var accessToken = GenerateJwtToken(user);
                var newRefreshToken = GenerateRefreshToken();
                var expiresIn = _accessTokenExpiryMinutes * 60;

                // Cập nhật UserToken
                await _db.ExecuteStoredProcedureAsync("sp_UserToken_Create", new
                {
                    MaTaiKhoan = user.MaTaiKhoan,
                    AccessToken = accessToken,
                    RefreshToken = newRefreshToken,
                    NgayHetHan = DateTime.UtcNow.AddDays(_refreshTokenExpiryDays)
                });

                user.AccessToken = accessToken;
                user.RefreshToken = newRefreshToken;
                user.ExpiresIn = expiresIn;

                Console.WriteLine($"Làm mới token thành công cho MaTaiKhoan: {user.MaTaiKhoan}");
                return ApiResponse<TokenResponse>.SuccessResponse(user, "Làm mới token thành công");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi làm mới token: {ex.Message}");
                return ApiResponse<TokenResponse>.ErrorResponse($"Lỗi khi làm mới token: {ex.Message}");
            }
        }
    }

}