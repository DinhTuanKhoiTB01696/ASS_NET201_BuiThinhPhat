using System.ComponentModel.DataAnnotations.Schema;

namespace Assignment_NET201.Models
{
    public class OrderDetail
    {
        public int Id { get; set; }

        public int OrderId { get; set; }
        public virtual Order Order { get; set; }

        public int? ProductId { get; set; }
        public virtual Product? Product { get; set; }

        public int? ComboId { get; set; }
        public virtual Combo? Combo { get; set; }

        public int Quantity { get; set; }

        public string ProductName { get; set; } // Snapshot of product name
        public string Size { get; set; }        // Snapshot of size
        public string Color { get; set; }       // Snapshot of color

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; } // Snapshot of price at time of purchase
    }
}
