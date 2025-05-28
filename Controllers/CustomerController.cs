using HotelManagement.Model;
using HotelManagement.Services;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace HotelManagement.Controllers
{
    [ApiController]
    [Route("api/customers")]
    public class CustomerController : ControllerBase
    {
        private readonly ICustomerRepository _customerRepository;

        public CustomerController(ICustomerRepository customerRepository)
        {
            _customerRepository = customerRepository;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllCustomers(
            [FromQuery, Range(1, int.MaxValue)] int pageNumber = 1,
            [FromQuery, Range(1, 100)] int pageSize = 10,
            [FromQuery] string? searchTerm = null,
            [FromQuery] string? sortBy = "MaKhachHang",
            [FromQuery] string? sortOrder = "ASC")
        {
            var (result, totalCount) = await _customerRepository.GetAllAsync(pageNumber, pageSize, searchTerm, sortBy, sortOrder);
            if (!result.Success)
                return StatusCode(500, new { success = false, message = result.Message, data = (IEnumerable<Customer>)null });

            return StatusCode(200, new
            {
                success = result.Success,
                message = result.Message,
                data = result.Data,
                totalCount,
                pageNumber,
                pageSize
            });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetCustomerById(string id)
        {
            var response = await _customerRepository.GetByIdAsync(id);
            if (!response.Success)
                return StatusCode(404, new { success = false, message = response.Message, data = (Customer)null });

            return StatusCode(200, new
            {
                success = response.Success,
                message = response.Message,
                data = response.Data
            });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCustomer(string id, [FromBody] Customer customer)
        {
            if (id != customer.MaKhachHang)
                return StatusCode(400, new { success = false, message = "Mã khách hàng không khớp", data = false });

            var existingCustomer = await _customerRepository.GetByIdAsync(id);
            if (!existingCustomer.Success)
                return StatusCode(404, new { success = false, message = "Không tìm thấy khách hàng", data = false });

            if (existingCustomer.Data.Email != customer.Email)
            {
                var emailExists = await _customerRepository.IsEmailExistsAsync(customer.Email);
                if (emailExists.Success && emailExists.Data)
                    return StatusCode(400, new { success = false, message = "Email đã tồn tại", data = false });
            }

            if (existingCustomer.Data.DienThoai != customer.DienThoai)
            {
                var phoneExists = await _customerRepository.IsPhoneExistsAsync(customer.DienThoai);
                if (phoneExists.Success && phoneExists.Data)
                    return StatusCode(400, new { success = false, message = "Số điện thoại đã tồn tại", data = false });
            }

            var response = await _customerRepository.UpdateAsync(customer);
            if (!response.Success)
                return StatusCode(400, new { success = false, message = response.Message, data = false });

            return StatusCode(200, new
            {
                success = response.Success,
                message = response.Message,
                data = response.Data
            });
        }
        [HttpPut("{maTaiKhoan}")]
        public async Task<IActionResult> UpdateCustomerName(int maTaiKhoan, [FromBody] UpdateCustomerNameRequest request)
        {
            var response = await _customerRepository.UpdateCustomerNameAsync(maTaiKhoan, request.HoTenKhachHang);
            if (!response.Success)
                return StatusCode(400, new { success = false, message = response.Message, data = (bool?)null });

            return StatusCode(200, new
            {
                success = response.Success,
                message = response.Message,
                data = response.Data
            });
        }
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCustomer(string id)
        {
            var existingCustomer = await _customerRepository.GetByIdAsync(id);
            if (!existingCustomer.Success)
                return StatusCode(404, new { success = false, message = "Không tìm thấy khách hàng", data = false });

            var response = await _customerRepository.DeleteAsync(id);
            if (!response.Success)
                return StatusCode(400, new { success = false, message = response.Message, data = false });

            return StatusCode(200, new
            {
                success = response.Success,
                message = response.Message,
                data = response.Data
            });
        }

        [HttpPost("{id}/point")]
        public async Task<IActionResult> AddPoints([FromBody] AddPointsRequest request)
        {
            var existingCustomer = await _customerRepository.GetByIdAsync(request.MaKhachHang);
            if (!existingCustomer.Success)
                return StatusCode(404, new { success = false, message = "Không tìm thấy khách hàng", data = false });

            var response = await _customerRepository.AddPointsAsync(request.MaKhachHang, request.SoDiem);
            if (!response.Success)
                return StatusCode(400, new { success = false, message = response.Message, data = false });

            return StatusCode(200, new
            {
                success = response.Success,
                message = response.Message,
                data = response.Data
            });
        }

        [HttpGet("validate/email/{email}")]
        public async Task<IActionResult> CheckEmail(string email)
        {
            var response = await _customerRepository.IsEmailExistsAsync(email);
            return StatusCode(200, new
            {
                success = response.Success,
                message = response.Message,
                data = response.Data
            });
        }

        [HttpGet("validate/phone/{phone}")]
        public async Task<IActionResult> CheckPhone(string phone)
        {
            var response = await _customerRepository.IsPhoneExistsAsync(phone);
            return StatusCode(200, new
            {
                success = response.Success,
                message = response.Message,
                data = response.Data
            });
        }
    }
}