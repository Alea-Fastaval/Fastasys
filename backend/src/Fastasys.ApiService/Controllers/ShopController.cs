using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fastasys.ApiService.Controllers;

public record ProductDto(int Id, string Name, string Description, decimal Price, int Stock, string Category, bool IsActive);
public record CreateProductDto(string Name, string Description, decimal Price, int Stock, string Category);
public record CreateOrderDto(int ProductId, int Quantity, int? ParticipantId);

[ApiController]
[Route("api/[controller]")]
public class ShopController : ControllerBase
{
    private readonly InfosysDbContext _db;

    public ShopController(InfosysDbContext db)
    {
        _db = db;
    }

    [HttpGet("products")]
    public async Task<ActionResult<IEnumerable<ProductDto>>> GetProducts()
    {
        var products = await _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .Select(p => new ProductDto(p.Id, p.Name, p.Description, p.Price, p.Stock, p.Category, p.IsActive))
            .ToListAsync();

        return Ok(products);
    }

    [HttpPost("products")]
    [Authorize(Policy = "ShopManagement")]
    public async Task<ActionResult<ProductDto>> CreateProduct([FromBody] CreateProductDto dto)
    {
        var product = new Product
        {
            Name = dto.Name,
            Description = dto.Description,
            Price = dto.Price,
            Stock = dto.Stock,
            Category = dto.Category,
            IsActive = true
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        return Ok(new ProductDto(product.Id, product.Name, product.Description, product.Price, product.Stock, product.Category, product.IsActive));
    }

    [HttpPost("orders")]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDto dto)
    {
        var product = await _db.Products.FindAsync(dto.ProductId);
        if (product == null) return NotFound(new { message = "Product not found" });

        if (product.Stock < dto.Quantity) return BadRequest(new { message = "Insufficient stock" });

        product.Stock -= dto.Quantity;

        var sale = new Sale
        {
            ProductId = dto.ProductId,
            ParticipantId = dto.ParticipantId,
            Quantity = dto.Quantity,
            TotalAmount = product.Price * dto.Quantity,
            SaleDate = DateTime.UtcNow
        };

        _db.Sales.Add(sale);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Order placed successfully", saleId = sale.Id, totalAmount = sale.TotalAmount });
    }

    [HttpGet("sales")]
    public async Task<IActionResult> GetSales()
    {
        var sales = await _db.Sales
            .Include(s => s.Product)
            .Include(s => s.Participant)
            .OrderByDescending(s => s.SaleDate)
            .AsNoTracking()
            .Select(s => new
            {
                s.Id,
                s.ProductId,
                ProductName = s.Product.Name,
                s.Quantity,
                s.TotalAmount,
                s.SaleDate,
                ParticipantName = s.Participant != null ? s.Participant.DisplayName() : "Guest"
            })
            .ToListAsync();

        return Ok(sales);
    }
}
