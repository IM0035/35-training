using OrderHub.Core.Domain;

namespace OrderHub.Tests;

public class OrderServiceQueryTests
{
    [Fact]
    public async Task GetOrders_WithStatusFilter_ReturnsOnlyMatchingStatus()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);
        var customer = TestSetup.AddCustomer(db);

        db.Orders.AddRange(
            new Order { CustomerId = customer.Id, Status = OrderStatus.Pending, CreatedAt = DateTime.UtcNow },
            new Order { CustomerId = customer.Id, Status = OrderStatus.Shipped, CreatedAt = DateTime.UtcNow },
            new Order { CustomerId = customer.Id, Status = OrderStatus.Shipped, CreatedAt = DateTime.UtcNow });
        db.SaveChanges();

        var result = await service.GetOrdersAsync(1, 20, OrderStatus.Shipped);

        Assert.All(result.Items, o => Assert.Equal(OrderStatus.Shipped, o.Status));
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task GetOrders_ReportsTotalCountAndTotalPages()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);
        var customer = TestSetup.AddCustomer(db);

        for (var i = 0; i < 45; i++)
            db.Orders.Add(new Order { CustomerId = customer.Id, Status = OrderStatus.Confirmed, CreatedAt = DateTime.UtcNow.AddMinutes(-i) });
        db.SaveChanges();

        var result = await service.GetOrdersAsync(1, 20, null);

        Assert.Equal(45, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public async Task GetOrders_FirstPage_IncludesNewestOrder()
    {
        // 迴歸測試：分頁 offset 若用 page * pageSize（0-based），第 1 頁會跳過最新的
        // pageSize 筆訂單，導致剛建立的訂單不會出現在列表上。
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);
        var customer = TestSetup.AddCustomer(db);

        var baseTime = DateTime.UtcNow;
        for (var i = 0; i < 25; i++)
            db.Orders.Add(new Order { CustomerId = customer.Id, Status = OrderStatus.Confirmed, CreatedAt = baseTime.AddMinutes(-i) });
        db.SaveChanges();

        // 最新的訂單（CreatedAt 最大）
        var newestId = db.Orders.OrderByDescending(o => o.CreatedAt).First().Id;

        var result = await service.GetOrdersAsync(1, 20, null);

        Assert.Equal(20, result.Items.Count);
        Assert.Contains(result.Items, o => o.Id == newestId);
        Assert.Equal(newestId, result.Items[0].Id);
    }

    [Fact]
    public async Task GetOrders_LastPage_ReturnsRemainingItems()
    {
        // 迴歸測試：分頁 offset 若用 page * pageSize（0-based），最後一頁會 Skip 掉
        // 全部資料，導致最後一頁是空的。45 筆 / 每頁 20 → 共 3 頁，第 3 頁應有 5 筆。
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);
        var customer = TestSetup.AddCustomer(db);

        var baseTime = DateTime.UtcNow;
        for (var i = 0; i < 45; i++)
            db.Orders.Add(new Order { CustomerId = customer.Id, Status = OrderStatus.Confirmed, CreatedAt = baseTime.AddMinutes(-i) });
        db.SaveChanges();

        var lastPage = await service.GetOrdersAsync(3, 20, null);

        Assert.Equal(3, lastPage.TotalPages);
        Assert.Equal(5, lastPage.Items.Count);

        // 各頁不重疊，且合計等於總筆數（確認 offset 正確）
        var page1 = await service.GetOrdersAsync(1, 20, null);
        var page2 = await service.GetOrdersAsync(2, 20, null);
        var allIds = page1.Items.Concat(page2.Items).Concat(lastPage.Items).Select(o => o.Id).ToList();
        Assert.Equal(45, allIds.Distinct().Count());
    }

    [Fact]
    public async Task GetCustomerOrders_ReturnsOnlyThatCustomersOrders()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);
        var customerA = TestSetup.AddCustomer(db, name: "客戶A");
        var customerB = TestSetup.AddCustomer(db, name: "客戶B");

        db.Orders.AddRange(
            new Order { CustomerId = customerA.Id, Status = OrderStatus.Pending, CreatedAt = DateTime.UtcNow },
            new Order { CustomerId = customerB.Id, Status = OrderStatus.Pending, CreatedAt = DateTime.UtcNow },
            new Order { CustomerId = customerA.Id, Status = OrderStatus.Shipped, CreatedAt = DateTime.UtcNow });
        db.SaveChanges();

        var orders = await service.GetCustomerOrdersAsync(customerA.Id);

        Assert.Equal(2, orders.Count);
        Assert.All(orders, o => Assert.Equal(customerA.Id, o.CustomerId));
    }
}
