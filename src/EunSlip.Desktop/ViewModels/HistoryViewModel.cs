using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using EunSlip.Core.Persistence;
using Microsoft.Extensions.Logging;

namespace EunSlip.Desktop.ViewModels;

public sealed partial class HistoryViewModel(
    IAppRepository repository, ILogger<HistoryViewModel> logger) : ViewModelBase
{
    private readonly IAppRepository _repository = repository;
    private readonly ILogger<HistoryViewModel> _logger = logger;

    public ObservableCollection<PayrollBatchRecord> Batches { get; } = [];

    [RelayCommand]
    private void Loaded()
    {
        try
        {
            Batches.Clear();
            foreach (PayrollBatchRecord batch in _repository.ListBatches())
            {
                Batches.Add(batch);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load history");
        }
    }

    [RelayCommand]
    private void DeleteBatch(PayrollBatchRecord batch)
    {
        _repository.DeleteBatch(batch.Id);
        _ = Batches.Remove(batch);
    }
}
