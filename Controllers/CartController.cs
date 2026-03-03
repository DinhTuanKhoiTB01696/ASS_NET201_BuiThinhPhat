using Assignment_NET201.Data;
using Assignment_NET201.Extensions;
using Assignment_NET201.Models;
using Assignment_NET201.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Assignment_NET201.Controllers
{
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public CartController(ApplicationDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var cart = HttpContext.Session.Get<List<CartItem>>("Cart") ?? new List<CartItem>();
            
            // Auto-validate Voucher (Fraud prevention)
            await ValidateVoucherAsync(cart);

            // Cross-selling logic: Suggest accessories if buying main clothes
            // Example: If has shirts/pants, suggest socks or belts
            ViewBag.Suggestions = await _context.Products
                .Where(p => p.IsActive && p.Category.Name == "Accessories")
                .Take(3)
                .ToListAsync();

            return View(cart);
        }

        private async Task ValidateVoucherAsync(List<CartItem> cart)
        {
            var appliedVoucherCode = HttpContext.Session.GetString("AppliedVoucher");
            if (string.IsNullOrEmpty(appliedVoucherCode)) return;

            var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == appliedVoucherCode && v.IsActive);
            decimal totalAmount = cart.Sum(c => c.Total);

            if (voucher == null || totalAmount < voucher.MinOrderValue || DateTime.Now > voucher.ExpiryDate)
            {
                HttpContext.Session.Remove("AppliedVoucher");
                TempData["VoucherMessage"] = "Mã giảm giá đã bị gỡ do không còn đủ điều kiện hoặc đã hết hạn.";
            }
        }

        [HttpPost]
        public async Task<IActionResult> ApplyVoucher(string voucherCode)
        {
            var cart = HttpContext.Session.Get<List<CartItem>>("Cart") ?? new List<CartItem>();
            if (!cart.Any()) return RedirectToAction("Index");

            var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == voucherCode && v.IsActive);
            if (voucher == null)
            {
                TempData["VoucherError"] = "Mã giảm giá không hợp lệ.";
                return RedirectToAction("Index");
            }

            if (DateTime.Now > voucher.ExpiryDate)
            {
                TempData["VoucherError"] = "Mã giảm giá đã hết hạn.";
                return RedirectToAction("Index");
            }

            if (voucher.UsedCount >= voucher.UsageLimit)
            {
                TempData["VoucherError"] = "Mã giảm giá đã hết lượt sử dụng.";
                return RedirectToAction("Index");
            }

            decimal totalAmount = cart.Sum(c => c.Total);
            if (totalAmount < voucher.MinOrderValue)
            {
                TempData["VoucherError"] = $"Giá trị đơn hàng tối thiểu để áp dụng mã này là {voucher.MinOrderValue:N0} VND.";
                return RedirectToAction("Index");
            }

            HttpContext.Session.SetString("AppliedVoucher", voucher.Code);
            TempData["VoucherMessage"] = $"Đã áp dụng mã {voucher.Code} thành công!";
            return RedirectToAction("Index");
        }

        public IActionResult RemoveVoucher()
        {
            HttpContext.Session.Remove("AppliedVoucher");
            return RedirectToAction("Index");
        }

        public IActionResult AddToCart(int productId, int quantity = 1, string size = "M", string color = "Default")
        {
            var product = _context.Products.Include(p => p.ProductVariants).FirstOrDefault(p => p.Id == productId);
            if (product == null) return NotFound();

            // Find variant and calculate price override (Options & Pricing logic)
            var variant = product.ProductVariants?.FirstOrDefault(v => v.Size == size && v.Color == color);
            decimal price = variant?.PriceOverride ?? product.Price;
            int? variantId = variant?.Id;

            var cart = HttpContext.Session.Get<List<CartItem>>("Cart") ?? new List<CartItem>();
            
            // Logic gộp/tách giỏ hàng: Identify product by ID, Size, AND Color
            var existingItem = cart.FirstOrDefault(c => c.ProductId == productId && c.Size == size && c.Color == color);

            // User preference: Replace the existing quantity rather than accumulating it
            int totalNewQuantity = quantity;

            // Simple stock check (could be refined for variants)
            int stockLimit = variant?.Quantity ?? product.Quantity;
            if (totalNewQuantity > stockLimit)
            {
                TempData["Error"] = $"Sản phẩm <strong>{product.Name}</strong> đã đạt giới hạn số lượng trong kho ({stockLimit}).";
                totalNewQuantity = stockLimit;
            }

            if (totalNewQuantity <= 0 && stockLimit > 0) totalNewQuantity = 1;

            if (stockLimit <= 0)
            {
                TempData["Error"] = $"Sản phẩm <strong>{product.Name}</strong> đã hết hàng.";
                return RedirectToAction("Index");
            }

            if (existingItem != null)
            {
                existingItem.Quantity = totalNewQuantity;
            }
            else
            {
                cart.Add(new CartItem
                {
                    ProductId = product.Id,
                    VariantId = variantId,
                    ProductName = product.Name,
                    Price = price, // Price with possible variant override
                    Quantity = totalNewQuantity,
                    ImageUrl = product.ImageUrl,
                    Size = size,
                    Color = color
                });
            }

            HttpContext.Session.Set("Cart", cart);
            return RedirectToAction("Index");
        }

        public IActionResult RemoveFromCart(int productId, string size, string color)
        {
            var cart = HttpContext.Session.Get<List<CartItem>>("Cart") ?? new List<CartItem>();
            var item = cart.FirstOrDefault(c => c.ProductId == productId && c.Size == size && c.Color == color);
            if (item != null)
            {
                cart.Remove(item);
                HttpContext.Session.Set("Cart", cart);
            }
            return RedirectToAction("Index");
        }

        [Authorize]
        public async Task<IActionResult> Checkout()
        {
            var cart = HttpContext.Session.Get<List<CartItem>>("Cart") ?? new List<CartItem>();
            if (!cart.Any()) return RedirectToAction("Index");

            await ValidateVoucherAsync(cart);

            var user = await _userManager.GetUserAsync(User);
            ViewBag.CurrentUser = user;

            // Calculate discounts if voucher is applied
            var appliedVoucherCode = HttpContext.Session.GetString("AppliedVoucher");
            Voucher? appliedVoucher = null;
            decimal discountAmount = 0;

            if (!string.IsNullOrEmpty(appliedVoucherCode))
            {
                appliedVoucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == appliedVoucherCode);
                if (appliedVoucher != null)
                {
                    decimal subtotal = cart.Sum(c => c.Total);
                    if (appliedVoucher.DiscountType == "Percentage")
                    {
                        discountAmount = subtotal * (appliedVoucher.DiscountValue / 100);
                        if (appliedVoucher.MaxDiscountAmount.HasValue && discountAmount > appliedVoucher.MaxDiscountAmount.Value)
                        {
                            discountAmount = appliedVoucher.MaxDiscountAmount.Value;
                        }
                    }
                    else
                    {
                        discountAmount = appliedVoucher.DiscountValue;
                    }
                }
            }
            
            // Automatic discount based on User Rank (Loyalty)
            decimal rankDiscountPercent = user.Rank switch
            {
                "Silver" => 2,
                "Gold" => 5,
                "Diamond" => 10,
                _ => 0
            };
            
            decimal rankDiscount = (cart.Sum(c => c.Total) - discountAmount) * (rankDiscountPercent / 100);
            discountAmount += rankDiscount;

            ViewBag.AppliedVoucher = appliedVoucher;
            ViewBag.DiscountAmount = discountAmount;
            ViewBag.RankDiscountPercent = rankDiscountPercent;

            return View(cart);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> PlaceOrder()
        {
            var cart = HttpContext.Session.Get<List<CartItem>>("Cart") ?? new List<CartItem>();
            if (!cart.Any()) return RedirectToAction("Index");

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // Re-validate voucher one last time
            await ValidateVoucherAsync(cart);
            var voucherCode = HttpContext.Session.GetString("AppliedVoucher");
            Voucher? appliedVoucher = null;
            decimal discountAmount = 0;

            if (!string.IsNullOrEmpty(voucherCode))
            {
                appliedVoucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == voucherCode && v.IsActive);
                if (appliedVoucher != null)
                {
                    decimal subtotal = cart.Sum(c => c.Total);
                    if (appliedVoucher.DiscountType == "Percentage")
                    {
                        discountAmount = subtotal * (appliedVoucher.DiscountValue / 100);
                        if (appliedVoucher.MaxDiscountAmount.HasValue && discountAmount > appliedVoucher.MaxDiscountAmount.Value)
                            discountAmount = appliedVoucher.MaxDiscountAmount.Value;
                    }
                    else
                    {
                        discountAmount = appliedVoucher.DiscountValue;
                    }
                    
                    // Update voucher usage
                    appliedVoucher.UsedCount++;
                }
            }
            
            // User Rank Discount
            decimal rankDiscountPercent = user.Rank switch
            {
                "Silver" => 2,
                "Gold" => 5,
                "Diamond" => 10,
                _ => 0
            };
            decimal rankDiscount = (cart.Sum(c => c.Total) - discountAmount) * (rankDiscountPercent / 100);
            discountAmount += rankDiscount;

            // Validate and deduct stock
            var validCartItems = new List<CartItem>();
            var productIds = cart.Select(c => c.ProductId).ToList();
            var existingProducts = _context.Products
                .Include(p => p.ProductVariants)
                .Where(p => productIds.Contains(p.Id)).ToList();

            foreach (var item in cart)
            {
                var product = existingProducts.FirstOrDefault(p => p.Id == item.ProductId);
                if (product != null)
                {
                    if (item.VariantId.HasValue)
                    {
                        var variant = product.ProductVariants?.FirstOrDefault(v => v.Id == item.VariantId.Value);
                        if (variant != null && variant.Quantity >= item.Quantity && !variant.IsLocked)
                        {
                            validCartItems.Add(item);
                            variant.Quantity -= item.Quantity;
                            _context.Update(variant);
                        }
                    }
                    else
                    {
                        if (product.Quantity >= item.Quantity)
                        {
                            validCartItems.Add(item);
                            product.Quantity -= item.Quantity;
                            _context.Update(product);
                        }
                    }
                }
            }

            if (!validCartItems.Any())
            {
                HttpContext.Session.Remove("Cart");
                TempData["Message"] = "Giỏ hàng không còn sản phẩm hợp lệ.";
                return RedirectToAction("Index");
            }

            decimal finalTotal = validCartItems.Sum(c => c.Total) - discountAmount;
            if (finalTotal < 0) finalTotal = 0;

            // Points: 10,000 VND = 1 Point
            int pointsToEarn = (int)(finalTotal / 10000);

            var order = new Order
            {
                UserId = user.Id,
                OrderDate = DateTime.Now,
                Status = "Pending",
                TotalAmount = finalTotal,
                DiscountAmount = discountAmount,
                VoucherId = appliedVoucher?.Id,
                PointsEarned = pointsToEarn,
                OrderDetails = validCartItems.Select(c => new OrderDetail
                {
                    ProductId = c.ProductId,
                    Quantity = c.Quantity,
                    UnitPrice = c.Price,
                    ProductName = c.ProductName,
                    Size = c.Size,
                    Color = c.Color
                }).ToList()
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // Clear Cart and Voucher
            HttpContext.Session.Remove("Cart");
            HttpContext.Session.Remove("AppliedVoucher");

            return RedirectToAction("OrderConfirmation", new { id = order.Id });
        }

        [Authorize]
        public IActionResult OrderConfirmation(int id)
        {
            return View(id);
        }

        // --- Wishlist Actions ---
        public async Task<IActionResult> Wishlist()
        {
            var wishlistSession = HttpContext.Session.Get<List<WishlistItem>>("Wishlist") ?? new List<WishlistItem>();
            
            if (wishlistSession.Any())
            {
                var productIds = wishlistSession.Select(w => w.ProductId).ToList();
                var products = await _context.Products
                                             .Include(p => p.ProductVariants)
                                             .Where(p => productIds.Contains(p.Id))
                                             .ToDictionaryAsync(p => p.Id);
                ViewBag.WishlistProducts = products;
            }

            return View(wishlistSession);
        }

        public IActionResult AddToWishlist(int productId)
        {
            var product = _context.Products.Find(productId);
            if (product == null) return NotFound();

            var wishlist = HttpContext.Session.Get<List<WishlistItem>>("Wishlist") ?? new List<WishlistItem>();

            if (!wishlist.Any(w => w.ProductId == productId))
            {
                wishlist.Add(new WishlistItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Price = product.Price,
                    ImageUrl = product.ImageUrl
                });
                HttpContext.Session.Set("Wishlist", wishlist);
            }

            return RedirectToAction("Wishlist");
        }

        public IActionResult RemoveFromWishlist(int productId)
        {
            var wishlist = HttpContext.Session.Get<List<WishlistItem>>("Wishlist") ?? new List<WishlistItem>();
            var item = wishlist.FirstOrDefault(w => w.ProductId == productId);
            if (item != null)
            {
                wishlist.Remove(item);
                HttpContext.Session.Set("Wishlist", wishlist);
            }
            return RedirectToAction("Wishlist");
        }
    }
}
