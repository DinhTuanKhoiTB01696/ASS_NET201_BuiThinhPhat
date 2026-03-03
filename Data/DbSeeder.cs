using Assignment_NET201.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Assignment_NET201.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = serviceProvider.GetRequiredService<UserManager<AppUser>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // Use Migrate instead of EnsureCreated to support migrations
            try 
            {
                await context.Database.MigrateAsync();
            }
            catch (Exception)
            {
                // If migration fails because tables already exist (due to previous EnsureCreated)
                // we manually add missing columns
                try 
                {
                    await context.Database.ExecuteSqlRawAsync("IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Products]') AND name = 'Quantity') BEGIN ALTER TABLE [Products] ADD [Quantity] int NOT NULL DEFAULT 10; END");
                }
                catch { }
            }

            // Seed Roles
            string[] roles = { "Admin", "Customer", "Guest" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // Seed Admin User
            var adminEmail = "admin@cloth.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new AppUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "System Admin",
                    Address = "HQ",
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(adminUser, "Admin@123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }

            // Seed Categories
            if (!context.Categories.Any())
            {
                context.Categories.AddRange(new List<Category>
                {
                    new Category { Name = "T-Shirts" },
                    new Category { Name = "Jeans" },
                    new Category { Name = "Jackets" },
                    new Category { Name = "Accessories" }
                });
                await context.SaveChangesAsync();
            }

            // Seed Products (10 items)
            if (!context.Products.Any())
            {
                var tshirts = context.Categories.First(c => c.Name == "T-Shirts");
                var jeans = context.Categories.First(c => c.Name == "Jeans");
                var jackets = context.Categories.First(c => c.Name == "Jackets");

                context.Products.AddRange(new List<Product>
                {
                    // T-Shirts
                    new Product { Name = "Basic White Tee", Description = "Premium cotton essential.", Price = 250000, CategoryId = tshirts.Id, ImageUrl = "/images/BASIC WHITE TEE.jpg" },
                    new Product { Name = "Graphic Print Tee", Description = "Urban style graphic tee.", Price = 350000, CategoryId = tshirts.Id, ImageUrl = "/images/GRAPHIC PRINT TEE.jpg" },
                    new Product { Name = "Oversized Black Tee", Description = "Streetwear fit.", Price = 300000, CategoryId = tshirts.Id, ImageUrl = "/images/OVERSIZED BLACK TEE.avif" },
                    
                    // Jeans
                    new Product { Name = "Slim Fit Ripped Jeans", Description = "Edgy look with comfort stretch.", Price = 650000, CategoryId = jeans.Id, ImageUrl = "/images/Slim Fit Ripped Jeans.jpg" },
                    new Product { Name = "Classic Straight Cut", Description = "Timeless denim style.", Price = 600000, CategoryId = jeans.Id, ImageUrl = "/images/Classic Straight Cut.jpg" },
                    new Product { Name = "Black Skinny Jeans", Description = "Rockstar aesthetic.", Price = 700000, CategoryId = jeans.Id, ImageUrl = "/images/Black Skinny Jeans.jpg" },

                    // Jackets
                    new Product { Name = "Denim Jacket", Description = "Rugged and durable.", Price = 900000, CategoryId = jackets.Id, ImageUrl = "/images/Denim Jacket.jpg" },
                    new Product { Name = "Bomber Jacket", Description = "Classic aviator style.", Price = 850000, CategoryId = jackets.Id, ImageUrl = "/images/BOMBER JACKET.jpg" },
                    
                    // More T-Shirts
                    new Product { Name = "Striped Polo", Description = "Smart casual choice.", Price = 400000, CategoryId = tshirts.Id, ImageUrl = "/images/STRIPED POLO.jpg" },
                    new Product { Name = "V-Neck Sweater", Description = "Lightweight knit.", Price = 450000, CategoryId = tshirts.Id, ImageUrl = "/images/V-NECK SWEATER.jpg" },

                    // Accessories (for cross-selling)
                    new Product { Name = "Silver Necklace", Description = "Minimalist silver chain.", Price = 150000, CategoryId = context.Categories.First(c => c.Name == "Accessories").Id, ImageUrl = "/images/placeholder.jpg" },
                    new Product { Name = "Classic Black Belt", Description = "Leather waist belt.", Price = 200000, CategoryId = context.Categories.First(c => c.Name == "Accessories").Id, ImageUrl = "/images/placeholder.jpg" },
                    new Product { Name = "Cotton Socks", Description = "Comfortable everyday socks.", Price = 50000, CategoryId = context.Categories.First(c => c.Name == "Accessories").Id, ImageUrl = "/images/placeholder.jpg" }
                });
                await context.SaveChangesAsync();
            }
            else
            {
                // Force update existing products if they have placeholder images or old paths
                var products = await context.Products.ToListAsync();
                bool changed = false;
                foreach (var p in products)
                {
                    string oldUrl = p.ImageUrl ?? "";
                    if (oldUrl.Contains("placehold.co") || oldUrl.Contains("studio-shoot"))
                    {
                        if (p.Name == "Basic White Tee") p.ImageUrl = "/images/BASIC WHITE TEE.jpg";
                        else if (p.Name == "Graphic Print Tee") p.ImageUrl = "/images/GRAPHIC PRINT TEE.jpg";
                        else if (p.Name == "Oversized Black Tee") p.ImageUrl = "/images/OVERSIZED BLACK TEE.avif";
                        else if (p.Name == "Slim Fit Ripped Jeans") p.ImageUrl = "/images/Slim Fit Ripped Jeans.jpg";
                        else if (p.Name == "Classic Straight Cut") p.ImageUrl = "/images/Classic Straight Cut.jpg";
                        else if (p.Name == "Black Skinny Jeans") p.ImageUrl = "/images/Black Skinny Jeans.jpg";
                        else if (p.Name == "Denim Jacket") p.ImageUrl = "/images/Denim Jacket.jpg";
                        else if (p.Name == "Bomber Jacket") p.ImageUrl = "/images/BOMBER JACKET.jpg";
                        else if (p.Name == "Striped Polo") p.ImageUrl = "/images/STRIPED POLO.jpg";
                        else if (p.Name == "V-Neck Sweater") p.ImageUrl = "/images/V-NECK SWEATER.jpg";
                        changed = true;
                    }
                }
                if (changed) await context.SaveChangesAsync();
            }
        }
    }
}
