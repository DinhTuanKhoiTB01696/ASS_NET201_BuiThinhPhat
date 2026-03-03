using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Assignment_NET201.Models
{
    // 4. Data Validation & 1. Data Annotations
    public class Product
    {
        public int Id { get; set; }

        [Required] // Data Annotation
        [StringLength(200)]
        public string Name { get; set; }

        public string? Description { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(0, 100000000, ErrorMessage = "Giá không được vượt quá 100 triệu VNĐ")]
        public decimal Price { get; set; }

        public string? ImageUrl { get; set; }

        public int CategoryId { get; set; }
        public virtual Category Category { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Số lượng không được phép là số âm")]
        public int Quantity { get; set; }

        public bool IsActive { get; set; } = true;

        public virtual ICollection<ComboProduct> ComboProducts { get; set; }
        public virtual ICollection<ProductVariant> ProductVariants { get; set; }
    }
}
