using System.ComponentModel.DataAnnotations;

namespace Assignment_NET201.Models
{
    public class ProductVariant
    {
        public int Id { get; set; }

        [Required]
        public int ProductId { get; set; }
        public virtual Product Product { get; set; }

        [Required]
        [StringLength(50)]
        public string Size { get; set; }

        [Required]
        [StringLength(50)]
        public string Color { get; set; }

        [Range(5, 10000, ErrorMessage = "Số lượng phải từ 5 đến 10.000 sản phẩm")]
        public int Quantity { get; set; }

        [Range(0, 100000000, ErrorMessage = "Giá không được vượt quá 100 triệu VNĐ")]
        public decimal? PriceOverride { get; set; }

        public string? ImageUrl { get; set; }

        public bool IsLocked { get; set; } = false;

        public virtual ICollection<InventoryTransaction> Transactions { get; set; }
    }
}
