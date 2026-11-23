using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Windows.Input;

using Avalonia.Threading;

using SignalClean.Client.Services;
using SignalClean.Core;

namespace SignalClean.Client.ViewModels;

/// <summary>Головне вікно: список датчиків, графік сирого та очищеного сигналу, метрики якості, аномалії.</summary>
public sealed class MainViewModel : ViewModelBase, IDisposable
{
    public const int ChartPoints = 120;

    private readonly ApiClient _api;
    private readonly DispatcherTimer _timer;
    private bool _refreshing;

    private string _apiUrl = "http://localhost:5080";
    private string _status = "Не підключено";
    private bool _isConnected;
    private bool _autoRefresh = true;
    private SensorItemViewModel? _selectedSensor;
    private QualityReportDto? _quality;
    private IReadOnlyList<double?> _rawSeries = [];
    private IReadOnlyList<double?> _cleanSeries = [];
    private IReadOnlyList<bool> _flagged = [];

    public MainViewModel(ApiClient api)
    {
        _api = api;
        RefreshCommand = new AsyncCommand(RefreshAsync);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();
    }

    public ObservableCollection<SensorItemViewModel> Sensors { get; } = [];

    public ObservableCollection<AnomalyDto> Anomalies { get; } = [];

    public ICommand RefreshCommand { get; }

    public string ApiUrl
    {
        get => _apiUrl;
        set => SetProperty(ref _apiUrl, value);
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        private set => SetProperty(ref _isConnected, value);
    }

    public bool AutoRefresh
    {
        get => _autoRefresh;
        set
        {
            if (SetProperty(ref _autoRefresh, value))
            {
                if (value)
                {
                    _timer.Start();
                }
                else
                {
                    _timer.Stop();
                }
            }
        }
    }

    public SensorItemViewModel? SelectedSensor
    {
        get => _selectedSensor;
        set
        {
            if (SetProperty(ref _selectedSensor, value))
            {
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(SelectedTitle));
                _ = RefreshAsync();
            }
        }
    }

    public bool HasSelection => SelectedSensor is not null;

    public string SelectedTitle => SelectedSensor is null
        ? "Оберіть датчик зліва"
        : $"{SelectedSensor.Code} — {SelectedSensor.Name}";

    public IReadOnlyList<double?> RawSeries
    {
        get => _rawSeries;
        private set => SetProperty(ref _rawSeries, value);
    }

    public IReadOnlyList<double?> CleanSeries
    {
        get => _cleanSeries;
        private set => SetProperty(ref _cleanSeries, value);
    }

    public IReadOnlyList<bool> FlaggedPoints
    {
        get => _flagged;
        private set => SetProperty(ref _flagged, value);
    }

    public string ScoreText => _quality is null || _quality.TotalReadings == 0 ? "—" : Format(_quality.Score, "F1");

    public string GradeText => _quality?.Grade ?? "—";

    public string CompletenessText => Percent(_quality?.Completeness);

    public string ValidityText => Percent(_quality?.Validity);

    public string AnomalyRateText => Percent(_quality?.AnomalyRate);

    public string TotalText => _quality is null ? "—" : $"{_quality.TotalReadings} вимір., {_quality.AnomalyCount} аномал.";

    public async Task RefreshAsync()
    {
        if (_refreshing)
        {
            return; // попереднє оновлення ще триває
        }

        if (!_api.TrySetBaseUrl(ApiUrl))
        {
            SetDisconnected("Некоректна адреса API (приклад: http://localhost:5080)");
            return;
        }

        _refreshing = true;
        try
        {
            var sensors = await _api.GetSensorsAsync() ?? [];
            MergeSensors(sensors);

            if (SelectedSensor is { } selected)
            {
                await LoadDetailsAsync(selected.Code);
            }

            IsConnected = true;
            Status = $"Підключено · {Sensors.Count} датчиків · оновлено {DateTime.Now:HH:mm:ss}";
        }
        catch (HttpRequestException ex)
        {
            SetDisconnected($"API недоступний: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            SetDisconnected("API не відповідає (тайм-аут)");
        }
        catch (JsonException)
        {
            SetDisconnected("Неочікувана відповідь сервера");
        }
        finally
        {
            _refreshing = false;
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        _api.Dispose();
    }

    private async Task LoadDetailsAsync(string code)
    {
        var readings = await _api.GetReadingsAsync(code, ChartPoints) ?? [];
        var anomalies = await _api.GetAnomaliesAsync(code, 50) ?? [];
        _quality = await _api.GetQualityAsync(code);

        RawSeries = readings.Select(r => r.RawValue).ToList();
        CleanSeries = readings.Select(r => r.CleanValue).ToList();
        FlaggedPoints = readings.Select(r => r.Status is "Corrected" or "Invalid").ToList();

        Anomalies.Clear();
        foreach (var a in anomalies)
        {
            Anomalies.Add(a);
        }

        OnPropertyChanged(nameof(ScoreText));
        OnPropertyChanged(nameof(GradeText));
        OnPropertyChanged(nameof(CompletenessText));
        OnPropertyChanged(nameof(ValidityText));
        OnPropertyChanged(nameof(AnomalyRateText));
        OnPropertyChanged(nameof(TotalText));
    }

    /// <summary>Оновлює список, не втрачаючи виділення: існуючі рядки оновлюються, нові додаються, зниклі видаляються.</summary>
    private void MergeSensors(List<SensorDto> sensors)
    {
        var byCode = sensors.ToDictionary(s => s.Code, StringComparer.OrdinalIgnoreCase);

        for (int i = Sensors.Count - 1; i >= 0; i--)
        {
            if (!byCode.ContainsKey(Sensors[i].Code))
            {
                Sensors.RemoveAt(i);
            }
        }

        foreach (var dto in sensors)
        {
            var existing = Sensors.FirstOrDefault(s => string.Equals(s.Code, dto.Code, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                Sensors.Add(new SensorItemViewModel(dto));
            }
            else
            {
                existing.Update(dto);
            }
        }

        if (SelectedSensor is null && Sensors.Count > 0)
        {
            _selectedSensor = Sensors[0];
            OnPropertyChanged(nameof(SelectedSensor));
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(SelectedTitle));
        }
    }

    private void SetDisconnected(string message)
    {
        IsConnected = false;
        Status = message;
    }

    private static string Percent(double? value) =>
        value is double v ? Format(v * 100, "F1") + " %" : "—";

    private static string Format(double value, string format) =>
        value.ToString(format, CultureInfo.InvariantCulture);
}