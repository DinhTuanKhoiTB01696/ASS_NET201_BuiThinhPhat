using System;
using System.ComponentModel.DataAnnotations;

namespace Assignment_NET201.Models
{
    public class InventoryTransaction
    {
        public int Id { get; set; }

        public int ProductVariantId { get; set; }
        public virtual ProductVariant ProductVariant { get; set; }

        [Required]
        public string Type { get; set; } // "Import" or "Export"

        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }

        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string? CreatedBy { get; set; }
    }
}
