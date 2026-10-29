using EShop.Domain.Catalog;
using EShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EShop.Infrastructure.Catalog;

public sealed class CatalogSeeder(AppDbContext db, TimeProvider time)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await db.Categories.AnyAsync(ct))
        {
            return;
        }

        var now = time.GetUtcNow();

        var electronics = Category.Create("Electronics", "electronics", null, now);
        var home = Category.Create("Home & Kitchen", "home-kitchen", null, now);
        var books = Category.Create("Books", "books", null, now);

        var phones = Category.Create("Phones", "phones", electronics, now);
        var audio = Category.Create("Audio", "audio", electronics, now);
        var cookware = Category.Create("Cookware", "cookware", home, now);
        var lighting = Category.Create("Lighting", "lighting", home, now);
        var fiction = Category.Create("Fiction", "fiction", books, now);
        var technical = Category.Create("Technical", "technical", books, now);

        db.Categories.AddRange(
            electronics, home, books, phones, audio, cookware, lighting, fiction, technical);

        db.Products.AddRange(
            P(phones, "Nova X1 Smartphone", "nova-x1-smartphone", "6.1-inch display, 128 GB storage.", 449.00m, 25),
            P(phones, "Nova X1 Pro Smartphone", "nova-x1-pro-smartphone", "6.7-inch display, 256 GB storage.", 649.00m, 15),
            P(audio, "Wireless Earbuds", "wireless-earbuds", "Bluetooth 5.3, 24 hours with the case.", 59.90m, 100),
            P(audio, "Over-Ear Headphones", "over-ear-headphones", "Noise cancelling, 30 hours battery.", 129.00m, 0),
            P(cookware, "Cast Iron Pan 26 cm", "cast-iron-pan-26", "Pre-seasoned, works on induction.", 39.50m, 30),
            P(cookware, "Stainless Steel Pot 5 L", "stainless-pot-5l", "Glass lid, dishwasher safe.", 54.00m, 20),
            P(lighting, "LED Desk Lamp", "led-desk-lamp", "Dimmable, three colour temperatures.", 24.99m, 60),
            P(lighting, "Smart Bulb E27", "smart-bulb-e27", "Wi-Fi, 800 lumens, 16 million colours.", 12.95m, 80),
            P(fiction, "The Lighthouse Keeper", "the-lighthouse-keeper", "A quiet novel set on a remote island.", 14.90m, 50),
            P(fiction, "City of Glass Rain", "city-of-glass-rain", "A science fiction thriller.", 16.50m, 35),
            P(technical, "Clean Code in Practice", "clean-code-in-practice", "Readable code, step by step.", 34.00m, 45),
            P(technical, "PostgreSQL Internals", "postgresql-internals", "How the database works inside.", 42.00m, 10));

        await db.SaveChangesAsync(ct);

        Product P(Category category, string name, string slug, string description, decimal price, int stock)
        {
            var product = Product.Create(category.Id, name, slug, description, price, now);
            product.SetStock(stock, now);
            return product;
        }
    }
}