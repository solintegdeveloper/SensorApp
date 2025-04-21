using System.Text;
using System.IO;
using Microsoft.Maui.Storage;
using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace SensorApp;

public partial class SensorDataPage : ContentPage
{
    private SensorDatabase _sensorDatabase = new SensorDatabase();
    private List<SensorData> _todosLosDatos;

    public SensorDataPage()
    {
        InitializeComponent();
    }

    //protected override async void OnAppearing()
    //{
    //    base.OnAppearing();

    //    _todosLosDatos = await _sensorDatabase.GetAllSensorDataAsync();
    //    SensorCollectionView.ItemsSource = _todosLosDatos.OrderByDescending(d => d.Timestamp);
    //    SensorFilterPicker.SelectedIndex = 0;
    //}
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _todosLosDatos = await _sensorDatabase.GetAllSensorDataAsync();

        if (_todosLosDatos != null && _todosLosDatos.Any())
        {
            SensorCollectionView.ItemsSource = _todosLosDatos.OrderByDescending(d => d.Timestamp);
            SensorFilterPicker.SelectedIndex = 0;
            MostrarResumen();
        }
        else
        {
            ResumenLabel.Text = "No hay registros disponibles.";
            await DisplayAlert("Sin datos", "No hay datos guardados aún.", "OK");
        }
    }

    private void OnSensorFilterChanged(object sender, EventArgs e)
    {
        if (_todosLosDatos == null) return;

        var filtro = SensorFilterPicker.SelectedItem.ToString();

        IEnumerable<SensorData> datosFiltrados = filtro switch
        {
            "Acelerómetro" => _todosLosDatos.Where(d => d.SensorType.Contains("Accelerometer")),
            "Giroscopio" => _todosLosDatos.Where(d => d.SensorType.Contains("Gyroscope")),
            "Luz" => _todosLosDatos.Where(d => d.SensorType.Contains("Light")),
            _ => _todosLosDatos
        };

        SensorCollectionView.ItemsSource = datosFiltrados.OrderByDescending(d => d.Timestamp);
    }

    private async void OnClearDataClicked(object sender, EventArgs e)
    {
        bool confirm = await DisplayAlert("Confirmar", "¿Deseas borrar todo el historial de sensores?", "Sí", "No");
        if (confirm)
        {
            await _sensorDatabase.ClearSensorDataAsync();
            _todosLosDatos.Clear();
            SensorCollectionView.ItemsSource = null;
            MostrarResumen();
            await DisplayAlert("Historial borrado", "Todos los datos han sido eliminados.", "OK");
        }
    }

    private async void OnExportToCsvClicked(object sender, EventArgs e)
    {
        try
        {
            var datosAExportar = SensorCollectionView.ItemsSource.Cast<SensorData>().ToList();

            if (datosAExportar == null || datosAExportar.Count == 0)
            {
                await DisplayAlert("Sin datos", "No hay datos para exportar.", "OK");
                return;
            }

            var csv = new StringBuilder();
            csv.AppendLine("SensorType,Value1,Value2,Value3,Timestamp");

            foreach (var item in datosAExportar)
            {
                csv.AppendLine($"{item.SensorType},{item.Value1},{item.Value2},{item.Value3},{item.Timestamp:yyyy-MM-dd HH:mm:ss}");
            }

            var filtro = SensorFilterPicker.SelectedItem?.ToString() ?? "Todos";
            var nombreLimpio = filtro.Replace(" ", "_");
            var filename = Path.Combine(FileSystem.Current.AppDataDirectory, $"SensorData_{nombreLimpio}_{DateTime.Now:yyyyMMddHHmmss}.csv");
            File.WriteAllText(filename, csv.ToString());

            await DisplayAlert("Exportado", $"Archivo guardado en:\n{filename}", "OK");

            await Share.RequestAsync(new ShareFileRequest
            {
                Title = $"Datos de sensores - {filtro}",
                File = new ShareFile(filename)
            });
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async void OnExportAllClicked(object sender, EventArgs e)
    {
        try
        {
            var todosLosDatos = await _sensorDatabase.GetAllSensorDataAsync();

            if (todosLosDatos == null || todosLosDatos.Count == 0)
            {
                await DisplayAlert("Sin datos", "No hay datos para exportar.", "OK");
                return;
            }

            var csv = new StringBuilder();
            csv.AppendLine("SensorType,Value1,Value2,Value3,Timestamp");

            foreach (var item in todosLosDatos)
            {
                csv.AppendLine($"{item.SensorType},{item.Value1},{item.Value2},{item.Value3},{item.Timestamp:yyyy-MM-dd HH:mm:ss}");
            }

            var filename = Path.Combine(FileSystem.Current.AppDataDirectory, $"SensorData_Todos_{DateTime.Now:yyyyMMddHHmmss}.csv");
            File.WriteAllText(filename, csv.ToString());

            await DisplayAlert("Exportado", $"Archivo guardado en:\n{filename}", "OK");

            await Share.RequestAsync(new ShareFileRequest
            {
                Title = "Datos de sensores (todos)",
                File = new ShareFile(filename)
            });
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private void MostrarResumen()
    {
        if (_todosLosDatos == null || !_todosLosDatos.Any())
        {
            ResumenLabel.Text = "No hay registros disponibles.";
            return;
        }

        int total = _todosLosDatos.Count;
        int acelerometro = _todosLosDatos.Count(d => d.SensorType.Contains("Accelerometer"));
        int giroscopio = _todosLosDatos.Count(d => d.SensorType.Contains("Gyroscope"));
        int luz = _todosLosDatos.Count(d => d.SensorType.Contains("Light"));

        ResumenLabel.Text = $"Registros:\nAcelerómetro: {acelerometro}\nGiroscopio: {giroscopio}\nLuz: {luz}\nTotal: {total}";
    }

    private async void OnShowDatabasePathClicked(object sender, EventArgs e)
    {
        string dbPath = Path.Combine(FileSystem.AppDataDirectory, "sensors.db3");

        await DisplayAlert("Ruta de la base de datos", dbPath, "OK");

        // Opcional: también puedes copiar al portapapeles
        await Clipboard.SetTextAsync(dbPath);
    }

}
