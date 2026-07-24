using OrderHub.Core.Domain;
using OrderHub.Core.Services;

namespace OrderHub.Tests;

public class OrderServiceCreateTests
{
    [Fact]
    public async Task CreateOrder_HappyPath_CreatesPendingOrder()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);
        var customer = TestSetup.AddCustomer(db);
        var product = TestSetup.AddProduct(db);

        var result = await service.CreateOrderAsync(customer.Id, new[] { new NewOrderLine(product.Id, 2) });

        Assert.True(result.Success);
        Assert.NotNull(result.Value);
        Assert.Equal(OrderStatus.Pending, result.Value!.Status);
        Assert.Single(result.Value.Items);
        Assert.Equal(1, db.Orders.Count());
    }

    [Fact]
    public async Task CreateOrder_SnapshotsCurrentUnitPrice()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);
        var customer = TestSetup.AddCustomer(db);
        var product = TestSetup.AddProduct(db, unitPrice: 380m);

        var result = await service.CreateOrderAsync(customer.Id, new[] { new NewOrderLine(product.Id, 1) });

        Assert.True(result.Success);
        Assert.Equal(380m, result.Value!.Items.Single().UnitPriceSnapshot);
    }

    [Theory]
    [InlineData(CustomerTier.Standard, 3300)] // 無折扣
    [InlineData(CustomerTier.Silver, 3135)]   // 5%：3300 * 0.95
    [InlineData(CustomerTier.Gold, 2970)]     // 10%：3300 * 0.90
    public async Task CreateOrder_SnapshotIsRawPrice_DiscountAppliedOnce(CustomerTier tier, decimal expectedTotal)
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);
        var customer = TestSetup.AddCustomer(db, tier);
        var product = TestSetup.AddProduct(db, unitPrice: 3300m);

        var result = await service.CreateOrderAsync(customer.Id, new[] { new NewOrderLine(product.Id, 1) });

        Assert.True(result.Success);
        // 快照永遠是未折扣的原價，折扣不可烘焙進 UnitPriceSnapshot
        Assert.Equal(3300m, result.Value!.Items.Single().UnitPriceSnapshot);

        // 折扣只能經 CalculateTotal 套用一次（Gold：3300 * 0.9 = 2970，而非 2970 * 0.9 = 2673）
        var saved = await service.GetOrderAsync(result.Value.Id);
        Assert.Equal(expectedTotal, service.CalculateTotal(saved!));
    }

    [Fact]
    public async Task CreateOrder_DecrementsProductStock()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);
        var customer = TestSetup.AddCustomer(db);
        var product = TestSetup.AddProduct(db, stock: 10);

        var result = await service.CreateOrderAsync(customer.Id, new[] { new NewOrderLine(product.Id, 3) });

        Assert.True(result.Success);
        Assert.Equal(7, db.Products.Single(p => p.Id == product.Id).StockQuantity);
    }

    [Fact]
    public async Task CreateOrder_UnknownCustomer_Fails()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);
        var product = TestSetup.AddProduct(db);

        var result = await service.CreateOrderAsync(999, new[] { new NewOrderLine(product.Id, 1) });

        Assert.False(result.Success);
        Assert.Contains("客戶", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateOrder_EmptyLines_Fails()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);
        var customer = TestSetup.AddCustomer(db);

        var result = await service.CreateOrderAsync(customer.Id, Array.Empty<NewOrderLine>());

        Assert.False(result.Success);
    }

    [Fact]
    public async Task CreateOrder_NonPositiveQuantity_Fails()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);
        var customer = TestSetup.AddCustomer(db);
        var product = TestSetup.AddProduct(db);

        var result = await service.CreateOrderAsync(customer.Id, new[] { new NewOrderLine(product.Id, 0) });

        Assert.False(result.Success);
    }

    [Fact]
    public async Task CreateOrder_DuplicateProduct_Fails()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);
        var customer = TestSetup.AddCustomer(db);
        var product = TestSetup.AddProduct(db);

        var result = await service.CreateOrderAsync(customer.Id, new[]
        {
            new NewOrderLine(product.Id, 1),
            new NewOrderLine(product.Id, 2)
        });

        Assert.False(result.Success);
    }

    [Fact]
    public async Task CreateOrder_InactiveProduct_Fails()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);
        var customer = TestSetup.AddCustomer(db);
        var product = TestSetup.AddProduct(db, isActive: false);

        var result = await service.CreateOrderAsync(customer.Id, new[] { new NewOrderLine(product.Id, 1) });

        Assert.False(result.Success);
    }

    [Fact]
    public async Task CreateOrder_InsufficientStock_FailsWithMessage()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);
        var customer = TestSetup.AddCustomer(db);
        var product = TestSetup.AddProduct(db, stock: 2);

        var result = await service.CreateOrderAsync(customer.Id, new[] { new NewOrderLine(product.Id, 5) });

        Assert.False(result.Success);
        Assert.Contains("庫存不足", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateOrder_Failed_DoesNotPersistOrder()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);
        var customer = TestSetup.AddCustomer(db);
        var product = TestSetup.AddProduct(db, stock: 2);

        await service.CreateOrderAsync(customer.Id, new[] { new NewOrderLine(product.Id, 5) });

        Assert.Equal(0, db.Orders.Count());
    }
}
