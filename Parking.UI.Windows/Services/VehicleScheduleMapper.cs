using Parking.Domain.Model.Models;
using Parking.UI.Windows.ViewModels;
using System.Collections.ObjectModel;

namespace Parking.UI.Windows.Services;

/// <summary>Convierte los horarios editables de la ficha en filas de la base de datos y viceversa.</summary>
public static class VehicleScheduleMapper
{
    /// <summary>Crea los siete días iniciales en orden de lunes a domingo.</summary>
    public static void CreateDefaults(ObservableCollection<VehicleScheduleItemViewModel> days)
    {
        days.Clear();
        string[] names = ["LUNES", "MARTES", "MIÉRCOLES", "JUEVES", "VIERNES", "SÁBADO", "DOMINGO"];
        for (int index = 0; index < names.Length; index++)
        {
            bool sunday = index == 6;
            bool weekend = index >= 5;
            days.Add(new VehicleScheduleItemViewModel
            {
                DayOfWeek = sunday ? 0 : index + 1,
                DayName = names[index],
                IsEnabled = !sunday,
                StartTime = TimeSpan.FromHours(weekend ? 8 : 7),
                EndTime = TimeSpan.FromHours(weekend ? 18 : 19),
                IsFullDay = false
            });
        }
    }

    /// <summary>Restablece los días y muestra los horarios activos y desactivados guardados.</summary>
    public static void Load(ObservableCollection<VehicleScheduleItemViewModel> days, registered_vehicle vehicle)
    {
        foreach (var day in days)
        {
            day.IsEnabled = false;
            day.IsFullDay = false;
            day.StartTime = new TimeSpan(7, 0, 0);
            day.EndTime = new TimeSpan(19, 0, 0);
        }

        if (vehicle.monthly_vehicle_schedules == null) return;
        foreach (var day in days)
        {
            var saved = vehicle.monthly_vehicle_schedules
                .FirstOrDefault(item => !item.is_deleted && item.day_of_week == day.DayOfWeek);
            if (saved == null) continue;

            day.IsEnabled = saved.is_active;
            day.StartTime = saved.start_time.ToTimeSpan();
            day.EndTime = saved.end_time.ToTimeSpan();
            day.IsFullDay = saved.is_full_day;
        }
    }

    /// <summary>Prepara las filas habilitadas para un contrato nuevo.</summary>
    public static List<monthly_vehicle_schedule> CreateEnabled(
        IEnumerable<VehicleScheduleItemViewModel> days, int vehicleId, int userId) =>
        days.Where(day => day.IsEnabled)
            .Select(day => CreateSchedule(day, vehicleId, userId)).ToList();

    /// <summary>Separa los días ya guardados de los días nuevos que deben insertarse.</summary>
    public static (List<monthly_vehicle_schedule> ToUpdate, List<monthly_vehicle_schedule> ToAdd)
        BuildChanges(registered_vehicle vehicle, IEnumerable<VehicleScheduleItemViewModel> days, int userId)
    {
        var toUpdate = new List<monthly_vehicle_schedule>();
        var toAdd = new List<monthly_vehicle_schedule>();
        foreach (var day in days)
        {
            var existing = vehicle.monthly_vehicle_schedules
                .FirstOrDefault(item => item.day_of_week == day.DayOfWeek);
            if (existing != null)
            {
                existing.start_time = TimeOnly.FromTimeSpan(day.StartTime);
                existing.end_time = TimeOnly.FromTimeSpan(day.EndTime);
                existing.is_full_day = day.IsFullDay;
                existing.is_active = day.IsEnabled;
                existing.updated_at = DateTime.Now;
                existing.updated_by = userId;
                toUpdate.Add(existing);
            }
            else if (day.IsEnabled)
            {
                toAdd.Add(CreateSchedule(day, vehicle.id, userId));
            }
        }
        return (toUpdate, toAdd);
    }

    /// <summary>Copia las horas y el estado de un día editable a una fila nueva.</summary>
    private static monthly_vehicle_schedule CreateSchedule(VehicleScheduleItemViewModel day, int vehicleId,
        int userId) => new()
    {
        registered_vehicle_id = vehicleId,
        day_of_week = day.DayOfWeek,
        start_time = TimeOnly.FromTimeSpan(day.StartTime),
        end_time = TimeOnly.FromTimeSpan(day.EndTime),
        is_active = day.IsEnabled,
        is_full_day = day.IsFullDay,
        created_at = DateTime.Now,
        created_by = userId,
        is_deleted = false
    };
}
