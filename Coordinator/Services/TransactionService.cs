using Coordinator.Enums;
using Coordinator.Models;
using Coordinator.Models.Contexts;
using Coordinator.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Coordinator.Services
{
    public class TransactionService(IHttpClientFactory _httpClientFactory, TwoPhaseCommitContext _context) : ITransactionService
    {
        HttpClient _httpClientOrderAPI = _httpClientFactory.CreateClient("OrderAPI");
        HttpClient _httpClientStockAPI = _httpClientFactory.CreateClient("StockAPI");
        HttpClient _httpClientPaymentAPI = _httpClientFactory.CreateClient("PaymentAPI");
        public async Task<bool> CheckReadyServices(Guid transactionId) =>
            (await _context.NodeStates
                    .Where(ns => ns.TransactionId == transactionId)
                    .ToListAsync())
            .TrueForAll(n => n.IsReady == ReadyType.Ready);

        public async Task<bool> CheckTransactionStateServices(Guid transactionId) =>
            (await _context.NodeStates
                    .Where(ns => ns.TransactionId == transactionId)
                    .ToListAsync())
            .TrueForAll(n => n.TransactionState == TransactionState.Done);

        public async Task Commit(Guid transactionId)
        {
            var transactionNodes = await _context.NodeStates
                .Include(ns => ns.Node)
                .Where(ns => ns.TransactionId == transactionId)
                .ToListAsync();
            foreach (var transactionNode in transactionNodes)
            {
                try
                {
                    var response = transactionNode.Node.Name switch
                    {
                        "Order.API" => await _httpClientOrderAPI.GetAsync("commit"),
                        "Stock.API" => await _httpClientStockAPI.GetAsync("commit"),
                        "Payment.API" => await _httpClientPaymentAPI.GetAsync("commit")
                    };
                    //Katılımcıların ready endpoint'inden result olarak true ya da false dönmesini bekliyoruz.
                    var result = bool.Parse(await response.Content.ReadAsStringAsync());
                    transactionNode.TransactionState = result ? TransactionState.Done : TransactionState.Abort;
                }
                catch
                {
                    transactionNode.TransactionState = TransactionState.Abort;
                }
            }
            await _context.SaveChangesAsync();
        }

        public async Task<Guid> CreateTransaction()
        {
            Guid transactionId = Guid.NewGuid();
            var nodes = await _context.Nodes.ToListAsync();
            nodes.ForEach(node => node.NodeStates = new List<NodeState>
            {
                new(TransactionId : transactionId)
                {
                    IsReady = ReadyType.Pending,
                    TransactionState = TransactionState.Pending
                },
            });

            await _context.SaveChangesAsync();
            return transactionId;
        }

        public async Task PrepareServices(Guid transactionId)
        {
            var transactionNodes = await _context.NodeStates
                .Include(ns => ns.Node)
                .Where(n => n.TransactionId == transactionId)
                .ToListAsync();
            foreach (var transactionNode in transactionNodes)
            {
                try
                {
                    var response = transactionNode.Node.Name switch
                    {
                        "Order.API" => await _httpClientOrderAPI.GetAsync("ready"),
                        "Stock.API" => await _httpClientStockAPI.GetAsync("ready"),
                        "Payment.API" => await _httpClientPaymentAPI.GetAsync("ready")
                    };
                    //Katılımcıların ready endpoint'inden result olarak true ya da false dönmesini bekliyoruz.
                    var result = bool.Parse(await response.Content.ReadAsStringAsync());
                    await Console.Out.WriteLineAsync(result.ToString());
                    transactionNode.IsReady = result ? ReadyType.Ready : ReadyType.Unready;
                }
                catch
                {

                    transactionNode.IsReady = ReadyType.Unready;
                }
            }
            await _context.SaveChangesAsync();
        }

        public async Task Rollback(Guid transactionId)
        {
            var transactionNodes = await _context.NodeStates
                .Include(ns => ns.Node)
                .Where(n => n.TransactionId == transactionId)
                .ToListAsync();
            transactionNodes.ForEach(async transactionNode =>
            {
                try
                {
                    if (transactionNode.TransactionState == TransactionState.Done)
                        _ = transactionNode.Node.Name switch
                        {
                            "Order.API" => await _httpClientOrderAPI.GetAsync("rollback"),
                            "Stock.API" => await _httpClientStockAPI.GetAsync("rollback"),
                            "Payment.API" => await _httpClientPaymentAPI.GetAsync("rollback"),
                        };
                    transactionNode.TransactionState = TransactionState.Abort;
                }
                catch
                {
                    transactionNode.TransactionState = TransactionState.Abort;
                }
            });
            await _context.SaveChangesAsync();
        }
    }
}
