namespace Assignment_NET201.ViewModels
{
    public class WishlistItem
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public decimal Price { get; set; }
        public string ImageUrl { get; set; }
        public DateTime DateAdded { get; set; } = DateTime.Now;
    }
}
