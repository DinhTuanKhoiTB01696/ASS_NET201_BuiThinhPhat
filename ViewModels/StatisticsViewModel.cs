using Assignment_NET201.Models;
using System.Collections.Generic;

namespace Assignment_NET201.ViewModels
{
    public class StatisticsViewModel
    {
        // Summary Cards
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal CollectedRevenue { get; set; } // Orders "Delivered"
        public decimal PendingRevenue { get; set; } // Orders "Pending", "Shipping"
        
        // Today Stats
        public int TodayOrders { get; set; }
        public decimal TodayRevenue { get; set; }

        // Growth percentages
        public decimal RevenueGrowth { get; set; }
        public decimal OrderGrowth { get; set; }

        // Status Counts
        public int DeliveredOrdersCount { get; set; }
        public int CancelledOrdersCount { get; set; }
        public int OtherOrdersCount { get; set; }

        // Charts Data
        public Dictionary<string, decimal> MonthlySales { get; set; } = new();
        public Dictionary<string, decimal> DailySales { get; set; } = new();
        public Dictionary<string, decimal> CategorySales { get; set; } = new();

        // Top Products
        public List<TopProductItem> TopProducts { get; set; } = new();
    }

    public class TopProductItem
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = "";
        public string? ImageUrl { get; set; }
        public string CategoryName { get; set; } = "";
        public int QuantitySold { get; set; }
        public decimal Revenue { get; set; }
        public decimal Percentage { get; set; }
    }
}
