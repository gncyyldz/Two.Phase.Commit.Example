using Coordinator.Models.Contexts;
using Coordinator.Services;
using Coordinator.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient("OrderAPI", client => client.BaseAddress = new("https://localhost:7287/"));
builder.Services.AddHttpClient("StockAPI", client => client.BaseAddress = new("https://localhost:7033/"));
builder.Services.AddHttpClient("PaymentAPI", client => client.BaseAddress = new("https://localhost:7121/"));

builder.Services.AddTransient<ITransactionService, TransactionService>();

builder.Services.AddDbContext<TwoPhaseCommitContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("SQLServer")));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/create-order-transaction", async (ITransactionService transactionService) =>
{
    //Phase 1 - Prepare
    var transactionId = await transactionService.CreateTransaction();
    await transactionService.PrepareServices(transactionId);
    bool transactionState = await transactionService.CheckReadyServices(transactionId);

    if (transactionState)
    {
        //Phase 2 - Commit
        await transactionService.Commit(transactionId);
        transactionState = await transactionService.CheckTransactionStateServices(transactionId);
    }

    if (!transactionState)
        await transactionService.Rollback(transactionId);
})
.WithName("GetWeatherForecast")
.WithOpenApi();

app.Run();