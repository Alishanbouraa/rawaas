﻿// Path: QuickTechSystems.Application/Services/Interfaces/ITransactionService.cs
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuickTechSystems.Application.Services.Interfaces
{
    public interface ITransactionService : IBaseService<TransactionDTO>
    {
        Task<TransactionDTO> UpdateAsync(TransactionDTO transactionDto);
        Task<bool> DeleteAsync(int transactionId);
        Task<TransactionDTO> ProcessSaleAsync(TransactionDTO transactionDto);
        Task<TransactionDTO> ProcessSaleAsync(TransactionDTO transactionDto, int cashierId);
        Task<IEnumerable<TransactionDTO>> GetByCustomerAsync(int customerId);
        Task<TransactionDTO> ProcessPaymentTransactionAsync(TransactionDTO transaction);
        Task<IEnumerable<TransactionDTO>> GetByCustomerAndDateRangeAsync(int customerId, DateTime startDate, DateTime endDate);
        Task<int> GetLatestTransactionIdAsync();
        Task<TransactionDTO?> GetLastTransactionAsync();

        // Use only one version with optional parameter
        Task<IEnumerable<TransactionDTO>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, string cashierId = null);

        Task<(IEnumerable<TransactionDTO> Transactions, int TotalCount)> GetByDateRangePagedAsync(
            DateTime startDate, DateTime endDate, int page, int pageSize, int? categoryId = null, string cashierId = null);
        Task<int> GetTransactionCountByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<decimal> GetTransactionSummaryByDateRangeAsync(DateTime startDate, DateTime endDate, string cashierId = null);
        Task<Dictionary<string, decimal>> GetCategorySalesByDateRangeAsync(DateTime startDate, DateTime endDate, string cashierId = null);
        Task<decimal> GetTransactionProfitByDateRangeAsync(DateTime startDate, DateTime endDate, int? categoryId = null, string cashierId = null);
        Task<IEnumerable<TransactionDTO>> GetByTypeAsync(TransactionType type);
        Task<bool> UpdateStatusAsync(int id, TransactionStatus status);
        Task<decimal> GetTotalSalesAsync(DateTime startDate, DateTime endDate);
    }
}