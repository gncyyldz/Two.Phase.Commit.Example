namespace Coordinator.Services.Abstractions
{
    public interface ITransactionService
    {
        public Task<Guid> CreateTransaction();
        public Task PrepareServices(Guid transactionId);
        public Task<bool> CheckReadyServices(Guid transactionId);
        public Task<bool> CheckTransactionStateServices(Guid transactionId);
        public Task Commit(Guid transactionId);
        public Task Rollback(Guid transactionId);
    }
}
