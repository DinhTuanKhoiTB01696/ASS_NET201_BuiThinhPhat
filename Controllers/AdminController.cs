using Assignment_NET201.Data;
using Assignment_NET201.Models;
using Assignment_NET201.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace Assignment_NET201.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IReportService _reportService;

        // 7. Dependency Injection
        public AdminController(ApplicationDbContext context, 
            IWebHostEnvironment webHostEnvironment, 
            UserManager<AppUser> userManager, 
            RoleManager<IdentityRole> roleManager,
            IReportService reportService)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _userManager = userManager;
            _roleManager = roleManager;
            _reportService = reportService;
        }

        // Dashboard
        public async Task<IActionResult> Index()
        {
            ViewBag.ProductCount = await _reportService.GetActiveProductCountAsync(); // Using DI Service
            // 8. Các kiểu truy vấn trong EF core (Count, Where, Sum, etc.)
            ViewBag.OrderCount = _context.Orders.Count();
            ViewBag.UserCount = _context.Users.Count();
            ViewBag.Revenue = _context.Orders.Where(o => o.Status == "Delivered").Sum(o => (decimal?)o.TotalAmount) ?? 0;
            
            // 9. Execute SQL Stored Procedures - EF Core (Hoặc SQL Raw)
            var topCategory = await _context.Categories
                .FromSqlRaw("SELECT TOP 1 * FROM Categories")
                .FirstOrDefaultAsync();
            ViewBag.TopCategory = topCategory?.Name;

            return View();
        }

        // Demonstration of Explicit Loading & Lazy Loading
        public async Task<IActionResult> QueryDemo(int productId)
        {
            // 12. Eager Loading-EF Core
            var productEager = await _context.Products.Include(p => p.Category).FirstOrDefaultAsync(p => p.Id == productId);

            // 14. Explicit Loading-EF Core
            var productExplicit = await _context.Products.FindAsync(productId);
            if (productExplicit != null)
            {
                await _context.Entry(productExplicit).Reference(p => p.Category).LoadAsync();
            }

            // 13. Lazy Loading-EF Core (Cần cài đặt Proxies trong Program.cs)
            var productLazy = await _context.Products.FindAsync(productId);
            var categoryName = productLazy.Category.Name; // Accessing Category will trigger a database query automatically

            return Content($"Eager: {productEager?.Category?.Name}, Explicit: {productExplicit?.Category?.Name}, Lazy: {categoryName}");
        }

        // Statistics Dashboard
        public async Task<IActionResult> Statistics()
        {
            var viewModel = new Assignment_NET201.ViewModels.StatisticsViewModel();

            var now = DateTime.Now;
            var today = now.Date;
            var thisMonthStart = new DateTime(now.Year, now.Month, 1);
            var lastMonthStart = thisMonthStart.AddMonths(-1);

            // Summary Cards
            // 10. LINQ to Entities- EF Core
            var allOrders = await _context.Orders.ToListAsync();
            viewModel.TotalOrders = allOrders.Count;
            viewModel.TotalRevenue = allOrders.Sum(o => o.TotalAmount);
            viewModel.CollectedRevenue = allOrders.Where(o => o.Status == "Delivered").Sum(o => o.TotalAmount);
            viewModel.PendingRevenue = allOrders.Where(o => o.Status == "Pending" || o.Status == "Shipping").Sum(o => o.TotalAmount);

            // Today Stats
            viewModel.TodayOrders = allOrders.Count(o => o.OrderDate.Date == today);
            viewModel.TodayRevenue = allOrders.Where(o => o.OrderDate.Date == today).Sum(o => o.TotalAmount);

            // Status Counts (for Pie Chart)
            viewModel.DeliveredOrdersCount = allOrders.Count(o => o.Status == "Delivered");
            viewModel.CancelledOrdersCount = allOrders.Count(o => o.Status == "Cancelled");
            viewModel.OtherOrdersCount = allOrders.Count(o => o.Status != "Delivered" && o.Status != "Cancelled");

            // Calculate Growth
            var thisMonthRevenue = allOrders.Where(o => o.OrderDate >= thisMonthStart).Sum(o => o.TotalAmount);
            var lastMonthRevenue = allOrders.Where(o => o.OrderDate >= lastMonthStart && o.OrderDate < thisMonthStart).Sum(o => o.TotalAmount);
            viewModel.RevenueGrowth = lastMonthRevenue > 0 ? ((thisMonthRevenue - lastMonthRevenue) / lastMonthRevenue) * 100 : 0;

            var thisMonthOrdersCount = allOrders.Count(o => o.OrderDate >= thisMonthStart);
            var lastMonthOrdersCount = allOrders.Count(o => o.OrderDate >= lastMonthStart && o.OrderDate < thisMonthStart);
            viewModel.OrderGrowth = lastMonthOrdersCount > 0 ? ((decimal)(thisMonthOrdersCount - lastMonthOrdersCount) / lastMonthOrdersCount) * 100 : 0;

            // Monthly Sales (current year) - Bar Chart
            for (int month = 1; month <= 12; month++)
            {
                var monthStart = new DateTime(now.Year, month, 1);
                var monthEnd = monthStart.AddMonths(1);
                var monthlyTotal = allOrders.Where(o => o.OrderDate >= monthStart && o.OrderDate < monthEnd).Sum(o => o.TotalAmount);
                viewModel.MonthlySales[$"T{month}"] = monthlyTotal;
            }

            // Daily Sales (Last 30 days) - Line Chart
            for (int i = 29; i >= 0; i--)
            {
                var date = today.AddDays(-i);
                var dailyTotal = allOrders.Where(o => o.OrderDate.Date == date).Sum(o => o.TotalAmount);
                viewModel.DailySales[date.ToString("dd/MM")] = dailyTotal;
            }

            // Category Sales
            var orderDetails = await _context.OrderDetails
                .Include(od => od.Product)
                    .ThenInclude(p => p.Category)
                .Where(od => od.Product != null)
                .ToListAsync();

            var categorySales = orderDetails
                .Where(od => od.Product?.Category != null)
                .GroupBy(od => od.Product!.Category.Name)
                .Select(g => new { Category = g.Key, Total = g.Sum(od => od.UnitPrice * od.Quantity) })
                .OrderByDescending(x => x.Total)
                .ToList();

            foreach (var cs in categorySales)
            {
                viewModel.CategorySales[cs.Category] = cs.Total;
            }

            // Top Products - Top 10 Bestsellers
            var topProducts = orderDetails
                .Where(od => od.Product != null)
                .GroupBy(od => od.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key ?? 0,
                    Product = g.First().Product,
                    QuantitySold = g.Sum(od => od.Quantity),
                    Revenue = g.Sum(od => od.UnitPrice * od.Quantity)
                })
                .OrderByDescending(x => x.Revenue)
                .Take(10)
                .ToList();

            var totalSales = topProducts.Sum(x => x.Revenue);
            foreach (var tp in topProducts)
            {
                viewModel.TopProducts.Add(new Assignment_NET201.ViewModels.TopProductItem
                {
                    ProductId = tp.ProductId,
                    ProductName = tp.Product?.Name ?? "Unknown",
                    ImageUrl = tp.Product?.ImageUrl,
                    CategoryName = tp.Product?.Category?.Name ?? "Unknown",
                    QuantitySold = tp.QuantitySold,
                    Revenue = tp.Revenue,
                    Percentage = totalSales > 0 ? (tp.Revenue / totalSales) * 100 : 0
                });
            }

            return View(viewModel);
        }

        public async Task<IActionResult> Products()
        {
            var products = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductVariants)
                .ToListAsync();

            // Update product quantity based on the sum of all its variants' quantities directly for display
            foreach (var p in products)
            {
                p.Quantity = p.ProductVariants.Sum(v => (int?)v.Quantity) ?? 0;
            }

            return View(products);
        }

        public IActionResult CreateProduct()
        {
            ViewBag.CategoryId = new SelectList(_context.Categories, "Id", "Name");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateProduct(Product product, IFormFile? imageFile)
        {
            ModelState.Remove("Category");
            // 5. Model Validation
            if (ModelState.IsValid)
            {
                if (imageFile != null)
                {
                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + imageFile.FileName;
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "products");

                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(fileStream);
                    }
                    product.ImageUrl = "/images/products/" + uniqueFileName;
                }

                _context.Add(product);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Products));
            }
            ViewBag.CategoryId = new SelectList(_context.Categories, "Id", "Name", product.CategoryId);
            return View(product);
        }

        // Edit Product GET
        public async Task<IActionResult> EditProduct(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            ViewBag.CategoryId = new SelectList(_context.Categories, "Id", "Name", product.CategoryId);
            return View(product);
        }

        // Edit Product POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProduct(int id, Product product, IFormFile? imageFile)
        {
            if (id != product.Id) return NotFound();

            ModelState.Remove("Category");
            if (ModelState.IsValid)
            {
                try
                {
                    if (imageFile != null)
                    {
                        // Delete old image if exists and not external URL
                        if (!string.IsNullOrEmpty(product.ImageUrl) && product.ImageUrl.StartsWith("/images/products/"))
                        {
                            var oldPath = Path.Combine(_webHostEnvironment.WebRootPath, product.ImageUrl.TrimStart('/'));
                            if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
                        }

                        // Save new image
                        string uniqueFileName = Guid.NewGuid().ToString() + "_" + imageFile.FileName;
                        string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "products");

                        if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                        string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await imageFile.CopyToAsync(fileStream);
                        }
                        product.ImageUrl = "/images/products/" + uniqueFileName;
                    }
                    else
                    {
                        // Keep existing image if no new file is uploaded
                        // We need to detach the entity to avoid tracking conflict if we fetched it similarly
                        // But here 'product' comes from form. We need to preserve ImageUrl if it wasn't in form (hidden field recommended)
                    }

                    _context.Update(product);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Products.Any(e => e.Id == product.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Products));
            }
            ViewBag.CategoryId = new SelectList(_context.Categories, "Id", "Name", product.CategoryId);
            return View(product);
        }

        // Delete Product POST
        [HttpPost, ActionName("DeleteProduct")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProductConfirmed(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                // Optional: Delete image file
                if (!string.IsNullOrEmpty(product.ImageUrl) && product.ImageUrl.StartsWith("/images/products/"))
                {
                    var oldPath = Path.Combine(_webHostEnvironment.WebRootPath, product.ImageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
                }

                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Products));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleProductStatus(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                product.IsActive = !product.IsActive;
                _context.Update(product);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Products));
        }

        // --- VOUCHERS ---
        public async Task<IActionResult> Vouchers()
        {
            var vouchers = await _context.Vouchers.OrderByDescending(v => v.ExpiryDate).ToListAsync();
            return View(vouchers);
        }

        public IActionResult CreateVoucher()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateVoucher(Voucher voucher)
        {
            if (ModelState.IsValid)
            {
                _context.Vouchers.Add(voucher);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Vouchers));
            }
            return View(voucher);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleVoucherStatus(int id)
        {
            var voucher = await _context.Vouchers.FindAsync(id);
            if (voucher != null)
            {
                voucher.IsActive = !voucher.IsActive;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Vouchers));
        }

        // --- ORDERS ---
        public async Task<IActionResult> Orders()
        {
            var orders = await _context.Orders.Include(o => o.User).OrderByDescending(o => o.OrderDate).ToListAsync();
            return View(orders);
        }

        public async Task<IActionResult> OrderDetail(int? id)
        {
            if (id == null) return NotFound();

            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Combo)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (order == null) return NotFound();

            return View(order);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateOrderStatus(int orderId, string status)
        {
            var order = await _context.Orders.Include(o => o.User).FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return NotFound();

            string oldStatus = order.Status;
            order.Status = status;

            // Loyalty logic: Only reward points when marked as "Delivered"
            if (status == "Delivered" && oldStatus != "Delivered" && order.User != null)
            {
                order.User.Points += order.PointsEarned;
                
                // Rank progression logic
                UpdateUserRank(order.User);
            }

            _context.Update(order);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(OrderDetail), new { id = orderId });
        }

        private void UpdateUserRank(AppUser user)
        {
            if (user.Points >= 1000) user.Rank = "Diamond";
            else if (user.Points >= 500) user.Rank = "Gold";
            else if (user.Points >= 100) user.Rank = "Silver";
            else user.Rank = "Bronze";
        }

        // --- USERS ---
        public async Task<IActionResult> Users()
        {
            var users = await _context.Users.ToListAsync();
            var userRoles = new Dictionary<string, IList<string>>();
            foreach (var user in users)
            {
                userRoles[user.Id] = await _userManager.GetRolesAsync(user);
            }
            ViewBag.UserRoles = userRoles;
            return View(users);
        }

        public async Task<IActionResult> EditUser(string id)
        {
            if (id == null) return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var userRoles = await _userManager.GetRolesAsync(user);
            var allRoles = _roleManager.Roles.ToList();

            ViewBag.Roles = new SelectList(allRoles, "Name", "Name", userRoles.FirstOrDefault());
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(string id, AppUser appUser, string selectedRole)
        {
            if (id != appUser.Id) return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            user.FullName = appUser.FullName;
            user.Address = appUser.Address;
            user.Email = appUser.Email;
            user.PhoneNumber = appUser.PhoneNumber;
            // UserName is usually same as Email in default identity, but let's keep it simple or sync them
            user.UserName = appUser.Email;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                var currentRoles = await _userManager.GetRolesAsync(user);
                var resultRemove = await _userManager.RemoveFromRolesAsync(user, currentRoles);
                if (resultRemove.Succeeded)
                {
                    if (!string.IsNullOrEmpty(selectedRole))
                    {
                        await _userManager.AddToRoleAsync(user, selectedRole);
                    }
                }
                return RedirectToAction(nameof(Users));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            var allRoles = _roleManager.Roles.ToList();
            var userRoles = await _userManager.GetRolesAsync(user);
            ViewBag.Roles = new SelectList(allRoles, "Name", "Name", userRoles.FirstOrDefault());
            return View(appUser);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                await _userManager.DeleteAsync(user);
            }
            return RedirectToAction(nameof(Users));
        }

        // --- INVENTORY ---
        public async Task<IActionResult> Inventory()
        {
            var products = await _context.Products
                .Include(p => p.Category)
                .Select(p => new
                {
                    Product = p,
                    TotalStock = _context.ProductVariants.Where(pv => pv.ProductId == p.Id).Sum(pv => (int?)pv.Quantity) ?? 0
                }).ToListAsync();

            ViewBag.InventoryData = products;
            return View(await _context.Products.Include(p => p.Category).ToListAsync());
        }

        public async Task<IActionResult> ProductVariants(int productId)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null) return NotFound();

            var variants = await _context.ProductVariants
                .Where(pv => pv.ProductId == productId)
                .ToListAsync();

            ViewBag.Product = product;
            return View(variants);
        }

        public IActionResult CreateVariant(int productId)
        {
            ViewBag.ProductId = productId;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateVariant(ProductVariant variant, IFormFile? imageFile)
        {
            if (ModelState.IsValid)
            {
                if (imageFile != null && imageFile.Length > 0)
                {
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "variants");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }
                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + imageFile.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(fileStream);
                    }
                    variant.ImageUrl = "/images/variants/" + uniqueFileName;
                }
                else
                {
                    // Fallback to parent product's image automatically if none provided
                    var parentProduct = await _context.Products.FindAsync(variant.ProductId);
                    if (parentProduct != null)
                    {
                        variant.ImageUrl = parentProduct.ImageUrl;
                    }
                }

                _context.ProductVariants.Add(variant);
                await _context.SaveChangesAsync();

                // Log initial stock as transaction if quantity > 0
                if (variant.Quantity > 0)
                {
                    var transaction = new InventoryTransaction
                    {
                        ProductVariantId = variant.Id,
                        Type = "Import",
                        Quantity = variant.Quantity,
                        Note = "Initial stock",
                        CreatedBy = User.Identity?.Name
                    };
                    _context.InventoryTransactions.Add(transaction);
                    await _context.SaveChangesAsync();
                }

                return RedirectToAction(nameof(ProductVariants), new { productId = variant.ProductId });
            }
            return View(variant);
        }

        // --- EDIT VARIANT ---
        public async Task<IActionResult> EditVariant(int? id)
        {
            if (id == null) return NotFound();

            var variant = await _context.ProductVariants.Include(v => v.Product).FirstOrDefaultAsync(v => v.Id == id);
            if (variant == null) return NotFound();

            ViewBag.ProductId = variant.ProductId;
            return View(variant);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditVariant(int id, ProductVariant variant, IFormFile? imageFile)
        {
            if (id != variant.Id) return NotFound();

            if (ModelState.IsValid)
            {
                // Check if editing causes a duplicate Size + Color
                var duplicate = await _context.ProductVariants
                    .FirstOrDefaultAsync(v => v.ProductId == variant.ProductId && v.Size == variant.Size && v.Color == variant.Color && v.Id != variant.Id);

                if (duplicate != null)
                {
                    var original = await _context.ProductVariants.FindAsync(id);
                    if (original != null)
                    {
                        int transferQty = original.Quantity;
                        
                        duplicate.Quantity += transferQty;
                        _context.Update(duplicate);

                        // Drain original, keep its old attributes
                        original.Quantity = 0;
                        _context.Update(original);

                        if (transferQty > 0)
                        {
                            _context.InventoryTransactions.Add(new InventoryTransaction
                            {
                                ProductVariantId = duplicate.Id,
                                Type = "Import",
                                Quantity = transferQty,
                                Note = $"Gộp ({transferQty} SP) do sửa phân loại từ màu {original.Color}",
                                CreatedBy = User.Identity?.Name,
                                CreatedAt = DateTime.Now
                            });

                            _context.InventoryTransactions.Add(new InventoryTransaction
                            {
                                ProductVariantId = original.Id,
                                Type = "Export",
                                Quantity = transferQty,
                                Note = $"Gộp ({transferQty} SP) sang màu {duplicate.Color}",
                                CreatedBy = User.Identity?.Name,
                                CreatedAt = DateTime.Now
                            });
                        }

                        await _context.SaveChangesAsync();
                        TempData["Success"] = $"Màu {variant.Color} của Size {variant.Size} đã tồn tại! Đã tự động gộp số lượng ({transferQty} SP). Phân loại cũ đã về 0.";
                        return RedirectToAction(nameof(ProductVariants), new { productId = variant.ProductId });
                    }
                }

                try
                {
                    if (imageFile != null && imageFile.Length > 0)
                    {
                        // Delete old image if it's a variant specific image
                        if (!string.IsNullOrEmpty(variant.ImageUrl) && variant.ImageUrl.StartsWith("/images/variants/"))
                        {
                            var oldPath = Path.Combine(_webHostEnvironment.WebRootPath, variant.ImageUrl.TrimStart('/'));
                            if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
                        }

                        // Save new image
                        string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "variants");
                        if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
                        
                        string uniqueFileName = Guid.NewGuid().ToString() + "_" + imageFile.FileName;
                        string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await imageFile.CopyToAsync(fileStream);
                        }
                        variant.ImageUrl = "/images/variants/" + uniqueFileName;
                    }
                    // if imageFile is null, EF Core will just keep the existing ImageUrl because it's bound from the hidden input.

                    _context.Update(variant);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductVariantExists(variant.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(ProductVariants), new { productId = variant.ProductId });
            }
            ViewBag.ProductId = variant.ProductId;
            return View(variant);
        }

        private bool ProductVariantExists(int id)
        {
            return _context.ProductVariants.Any(e => e.Id == id);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleVariantLock(int id)
        {
            var variant = await _context.ProductVariants.FindAsync(id);
            if (variant == null) return NotFound();

            variant.IsLocked = !variant.IsLocked;
            _context.Update(variant);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(ProductVariants), new { productId = variant.ProductId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportStock(int variantId, int quantity, string? note, string? newColor)
        {
            var variant = await _context.ProductVariants.FindAsync(variantId);
            if (variant == null) return NotFound();

            if (!string.IsNullOrEmpty(newColor) && newColor != variant.Color)
            {
                // Check if a variant with the same Size and NEW Color already exists for this product
                var existingVariant = await _context.ProductVariants
                    .FirstOrDefaultAsync(v => v.ProductId == variant.ProductId && v.Size == variant.Size && v.Color == newColor);
                
                if (existingVariant != null)
                {
                    existingVariant.Quantity += quantity;
                    _context.Update(existingVariant);

                    var transaction = new InventoryTransaction
                    {
                        ProductVariantId = existingVariant.Id,
                        Type = "Import",
                        Quantity = quantity,
                        Note = note ?? $"Imported {quantity} (moved from color mismatch, variant ID {variantId})",
                        CreatedBy = User.Identity?.Name,
                        CreatedAt = DateTime.Now
                    };
                    _context.InventoryTransactions.Add(transaction);
                }
                else
                {
                    // Create a new variant dynamically
                    var newVariant = new ProductVariant
                    {
                        ProductId = variant.ProductId,
                        Size = variant.Size,
                        Color = newColor,
                        Quantity = quantity,
                        ImageUrl = variant.ImageUrl,
                        PriceOverride = variant.PriceOverride,
                        IsLocked = false
                    };
                    _context.ProductVariants.Add(newVariant);
                    await _context.SaveChangesAsync(); // save to get ID

                    var transaction = new InventoryTransaction
                    {
                        ProductVariantId = newVariant.Id,
                        Type = "Import",
                        Quantity = quantity,
                        Note = note ?? $"Initial import upon dynamic color creation",
                        CreatedBy = User.Identity?.Name,
                        CreatedAt = DateTime.Now
                    };
                    _context.InventoryTransactions.Add(transaction);
                }
            }
            else
            {
                // Normal import
                variant.Quantity += quantity;
                _context.Update(variant);

                var transaction = new InventoryTransaction
                {
                    ProductVariantId = variantId,
                    Type = "Import",
                    Quantity = quantity,
                    Note = note,
                    CreatedBy = User.Identity?.Name,
                    CreatedAt = DateTime.Now
                };
                _context.InventoryTransactions.Add(transaction);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(ProductVariants), new { productId = variant.ProductId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportStock(int variantId, int quantity, string? note)
        {
            var variant = await _context.ProductVariants.FindAsync(variantId);
            if (variant == null) return NotFound();

            if (variant.Quantity < quantity)
            {
                TempData["Error"] = "Insufficient stock.";
                return RedirectToAction(nameof(ProductVariants), new { productId = variant.ProductId });
            }

            variant.Quantity -= quantity;
            _context.Update(variant);

            var transaction = new InventoryTransaction
            {
                ProductVariantId = variantId,
                Type = "Export",
                Quantity = quantity,
                Note = note,
                CreatedBy = User.Identity?.Name
            };
            _context.InventoryTransactions.Add(transaction);

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(ProductVariants), new { productId = variant.ProductId });
        }

        public async Task<IActionResult> InventoryHistory(int? variantId)
        {
            var query = _context.InventoryTransactions
                .Include(t => t.ProductVariant)
                    .ThenInclude(pv => pv.Product)
                .AsQueryable();

            if (variantId.HasValue)
            {
                query = query.Where(t => t.ProductVariantId == variantId.Value);
            }

            var history = await query.OrderByDescending(t => t.CreatedAt).ToListAsync();
            return View(history);
        }
    }
}
