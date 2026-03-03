using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Assignment_NET201.Models
{
    public class Voucher
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Code { get; set; }

        [Required]
        public string DiscountType { get; set; } // "Percentage" or "FixedAmount"

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountValue { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? MaxDiscountAmount { get; set; } // Only for Percentage type

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal MinOrderValue { get; set; }

        public DateTime ExpiryDate { get; set; }
        
        public int UsageLimit { get; set; } = 100;
        public int UsedCount { get; set; } = 0;

        public bool IsActive { get; set; } = true;
    }
}
