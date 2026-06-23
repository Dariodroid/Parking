using Parking.Application.Dto;
using Parking.Application.Dto.Interfaces;
using Parking.UI.Windows.ViewModels.Base;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Parking.UI.Windows.ViewModels;

public class VehicleReportViewModel
    : BaseViewModel
{
    private readonly IVehicleReportRepository _repository;

    public ObservableCollection<VehicleReportDto>
        Vehicles
    { get; set; }
        = new();

    public VehicleReportFilterDto Filter
    {
        get;
        set;
    } = new();

    public ICommand SearchCommand { get; }

    public VehicleReportViewModel(
        IVehicleReportRepository repository)
    {
        _repository = repository;

        SearchCommand =
            new RelayCommand(
                async _ => await LoadData());
    }

    public async Task LoadData()
    {
        Vehicles.Clear();

        var data =
            await _repository.GetReportAsync(
                Filter);

        foreach (var item in data)
        {
            Vehicles.Add(item);
        }
    }
}