using System.Globalization;

using Avalonia.Media;

using SignalClean.Core;

namespace SignalClean.Client.ViewModels;

/// <summary>Рядок у списку датчиків.</summary>
public sealed class SensorItemViewModel(SensorDto dto) : ViewModelBase
{
    private SensorDto _dto = dto;

    public string Code => _dto.Code;

    public string Name => _dto.Name;

    public string Unit => _dto.Unit;

    public string TypeAndLocation => string.IsNullOrEmpty(_dto.Location) ? _dto.Type : $"{_dto.Type} · {_dto.Location}";

    public string ScoreText => _dto.QualityScore is double s
        ? string.Create(CultureInfo.InvariantCulture, $"{s:F0}")
        : "—";

    public string Grade => _dto.Grade;

    /// <summary>Колір бейджа за класом якості.</summary>
    public IBrush GradeBrush => new SolidColorBrush(Color.Parse(_dto.Grade switch
    {
        "A" => "#2E7D32",
        "B" => "#689F38",
        "C" => "#F9A825",
        "D" => "#C62828",
        _ => "#9E9E9E",
    }));

    public void Update(SensorDto dto)
    {
        _dto = dto;
        OnPropertyChanged(string.Empty); // оновити всі прив'язки рядка
    }
}