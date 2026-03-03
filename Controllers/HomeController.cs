using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Assignment_NET201.Models;
using Assignment_NET201.Data;
using Microsoft.EntityFrameworkCore;

namespace Assignment_NET201.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;

    public HomeController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        // Fetch products for "Best Sellers" (Simulated by taking top 8)
        var products = await _context.Products.Where(p => p.IsActive).Take(8).ToListAsync();
        return View(products);
    }

    public async Task<IActionResult> Shop(string searchString, decimal? minPrice, decimal? maxPrice, int? categoryId, string sortOrder)
    {
        var products = _context.Products.Where(p => p.IsActive).AsQueryable();

        if (!string.IsNullOrEmpty(searchString))
        {
            products = products.Where(p => p.Name.Contains(searchString) || p.Description.Contains(searchString));
        }

        if (minPrice.HasValue)
        {
            products = products.Where(p => p.Price >= minPrice.Value);
        }

        if (maxPrice.HasValue)
        {
            products = products.Where(p => p.Price <= maxPrice.Value);
        }

        if (categoryId.HasValue)
        {
            products = products.Where(p => p.CategoryId == categoryId.Value);
        }

        // Apply Sorting
        ViewData["CurrentSort"] = sortOrder;
        products = sortOrder switch
        {
            "price_asc" => products.OrderBy(p => p.Price),
            "price_desc" => products.OrderByDescending(p => p.Price),
            _ => products.OrderBy(p => p.Id) // Default sorting
        };

        ViewBag.Categories = await _context.Categories.ToListAsync();
        ViewBag.SearchString = searchString;
        ViewBag.MinPrice = minPrice;
        ViewBag.MaxPrice = maxPrice;
        ViewBag.CategoryId = categoryId;

        return View(await products.ToListAsync());
    }

    [HttpGet]
    public async Task<IActionResult> SearchJson(string q)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return Json(new List<object>());
        }

        var results = await _context.Products
            .Where(p => p.IsActive && (p.Name.Contains(q) || p.Description.Contains(q)))
            .Take(6)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Price,
                p.ImageUrl
            })
            .ToListAsync();

        return Json(results);
    }

    public async Task<IActionResult> ProductDetail(int id)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.ProductVariants)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (product == null)
        {
            return NotFound();
        }

        // Fetch active vouchers for display
        var activeVouchers = await _context.Vouchers
            .Where(v => v.IsActive && v.ExpiryDate > DateTime.Now && v.UsedCount < v.UsageLimit)
            .OrderBy(v => v.MinOrderValue)
            .Take(5)
            .ToListAsync();

        ViewBag.Vouchers = activeVouchers;

        return View(product);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
