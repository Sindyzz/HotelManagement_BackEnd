using HotelManagement.DataReader;
using HotelManagement.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HotelManagement.Services
{
    public interface IPointProgramRepository
    {
        Task<ApiResponse<int>> CreateAsync(PointProgram pointProgram);
        Task<ApiResponse<bool>> UpdateAsync(PointProgram pointProgram);
        Task<ApiResponse<bool>> DeleteAsync(int maCT);
        Task<ApiResponse<PointProgram>> GetByIdAsync(int maCT);
        Task<(ApiResponse<IEnumerable<PointProgram>> Items, int TotalCount)> GetAllAsync(
            int pageNumber, int pageSize, string? searchTerm = null, string? sortBy = "MaCT", string? sortOrder = "ASC");
    }

    public class PointProgramRepository : IPointProgramRepository
    {
        private readonly DatabaseDapper _db;

        public PointProgramRepository(DatabaseDapper db)
        {
            _db = db;
        }

        public async Task<ApiResponse<int>> CreateAsync(PointProgram pointProgram)
        {
            try
            {
                Console.WriteLine($"TyLeTichDiem value: {pointProgram.TyLeTichDiem}");

                var parameters = new
                {
                    pointProgram.TenCT,
                    pointProgram.DiemToiThieu,
                    pointProgram.MucGiamGia,
                    pointProgram.TyLeTichDiem
                };

                var maCT = await _db.QueryFirstOrDefaultStoredProcedureAsync<int>("sp_PointProgram_Create", parameters);
                return ApiResponse<int>.SuccessResponse(maCT, "Tạo chương trình điểm thành công");
            }
            catch (Exception ex)
            {
                return ApiResponse<int>.ErrorResponse($"Lỗi khi tạo chương trình điểm: {ex.Message}");
            }
        }

        public async Task<ApiResponse<bool>> UpdateAsync(PointProgram pointProgram)
        {
            try
            {
                var parameters = new
                {
                    pointProgram.MaCT,
                    pointProgram.TenCT,
                    pointProgram.DiemToiThieu,
                    pointProgram.MucGiamGia
                };

                var rowsAffected = await _db.ExecuteStoredProcedureAsync("sp_PointProgram_Update", parameters);
                if (rowsAffected <= 0)
                    return ApiResponse<bool>.ErrorResponse("Cập nhật chương trình điểm thất bại");

                return ApiResponse<bool>.SuccessResponse(true, "Cập nhật chương trình điểm thành công");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.ErrorResponse($"Lỗi khi cập nhật chương trình điểm: {ex.Message}");
            }
        }

        public async Task<ApiResponse<bool>> DeleteAsync(int maCT)
        {
            try
            {
                var rowsAffected = await _db.ExecuteStoredProcedureAsync("sp_PointProgram_Delete", new { MaCT = maCT });
                if (rowsAffected <= 0)
                    return ApiResponse<bool>.ErrorResponse("Xóa chương trình điểm thất bại");

                return ApiResponse<bool>.SuccessResponse(true, "Xóa chương trình điểm thành công");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.ErrorResponse($"Lỗi khi xóa chương trình điểm: {ex.Message}");
            }
        }

        public async Task<ApiResponse<PointProgram>> GetByIdAsync(int maCT)
        {
            try
            {
                var pointProgram = await _db.QueryFirstOrDefaultStoredProcedureAsync<PointProgram>("sp_PointProgram_GetById", new { MaCT = maCT });
                if (pointProgram == null)
                    return ApiResponse<PointProgram>.ErrorResponse("Không tìm thấy chương trình điểm");

                return ApiResponse<PointProgram>.SuccessResponse(pointProgram, "Lấy thông tin chương trình điểm thành công");
            }
            catch (Exception ex)
            {
                return ApiResponse<PointProgram>.ErrorResponse($"Lỗi khi lấy thông tin chương trình điểm: {ex.Message}");
            }
        }

        public async Task<(ApiResponse<IEnumerable<PointProgram>> Items, int TotalCount)> GetAllAsync(
            int pageNumber, int pageSize, string? searchTerm = null, string? sortBy = "MaCT", string? sortOrder = "ASC")
        {
            try
            {
                using var reader = await _db.QueryMultipleAsync("sp_PointProgram_GetAll",
                    new
                    {
                        PageNumber = pageNumber,
                        PageSize = pageSize,
                        SearchTerm = searchTerm,
                        SortBy = sortBy,
                        SortOrder = sortOrder
                    });

                var items = (await reader.ReadAsync<PointProgram>()).ToList();
                var totalCount = await reader.ReadSingleAsync<int>();

                return (ApiResponse<IEnumerable<PointProgram>>.SuccessResponse(items, "Lấy danh sách chương trình điểm thành công"), totalCount);
            }
            catch (Exception ex)
            {
                return (ApiResponse<IEnumerable<PointProgram>>.ErrorResponse($"Lỗi khi lấy danh sách chương trình điểm: {ex.Message}"), 0);
            }
        }
    }
}