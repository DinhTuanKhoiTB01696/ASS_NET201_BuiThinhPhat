namespace Assignment_NET201.Models
{
    public class ComboProduct
    {
        public int ComboId { get; set; }
        public virtual Combo Combo { get; set; }

        public int ProductId { get; set; }
        public virtual Product Product { get; set; }
    }
}
