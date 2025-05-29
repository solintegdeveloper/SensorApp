using Microsoft.Maui.Controls;
using System.Collections.ObjectModel;

namespace SensorApp;

public partial class DailyStepsPage : ContentPage
{
    private SensorDatabase _sensorDatabase = new SensorDatabase();

    public ObservableCollection<DailySteps> DailyStepsList { get; set; } = new ObservableCollection<DailySteps>();

    public DailyStepsPage()
    {
        InitializeComponent();
        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        DailyStepsList.Clear();

        var steps = await _sensorDatabase.GetAllDailyStepsAsync();

        foreach (var step in steps)
        {
            DailyStepsList.Add(step);
        }
    }

    private async void OnClearHistoryClicked(object sender, EventArgs e)
    {
        bool answer = await DisplayAlert("Confirmar", "¿Seguro que quieres eliminar todo el historial de pasos?", "Sí", "No");
        if (answer)
        {
            await _sensorDatabase.DeleteAllDailyStepsAsync();
            DailyStepsList.Clear();
            await DisplayAlert("Eliminado", "El historial de pasos ha sido eliminado.", "OK");
        }
    }

}
