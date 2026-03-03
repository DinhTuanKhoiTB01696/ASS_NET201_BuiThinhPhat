using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Assignment_NET201.Models
{
    public class Order
    {
        public int Id { get; set; }

        public string UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual AppUser User { get; set; }

        public DateTime OrderDate { get; set; } = DateTime.Now;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        public int? VoucherId { get; set; }
        [ForeignKey("VoucherId")]
        public virtual Voucher Voucher { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; } = 0;

        public int PointsEarned { get; set; } = 0;

        public string Status { get; set; } = "Pending"; // Pending, Shipping, Delivered, Cancelled

        public virtual ICollection<OrderDetail> OrderDetails { get; set; }
    }
}
