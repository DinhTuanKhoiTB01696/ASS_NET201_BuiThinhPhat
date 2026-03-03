using Assignment_NET201.Data;
using Microsoft.EntityFrameworkCore;

namespace Assignment_NET201.Services
{
    public interface IReportService
    {
        Task<int> GetActiveProductCountAsync();
    }

    public class ReportService : IReportService
    {
        private readonly ApplicationDbContext _context;

        public ReportService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<int> GetActiveProductCountAsync()
        {
            return await _context.Products.CountAsync(p => p.IsActive);
        }
    }
}
