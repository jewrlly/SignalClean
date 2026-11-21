namespace SignalClean.Data;

/// <summary>Створює БД (якщо її немає) і заповнює початковими довідниками та датчиками.</summary>
public static class DbSeeder
{
    public static void Seed(SignalCleanDbContext db, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(db);
        clock ??= TimeProvider.System;

        db.Database.EnsureCreated();

        if (db.SensorTypes.Any())
        {
            return;
        }

        var temperature = new SensorType { Name = "Temperature", Unit = "°C", MinValid = -40, MaxValid = 85 };
        var humidity = new SensorType { Name = "Humidity", Unit = "%", MinValid = 0, MaxValid = 100 };
        var pressure = new SensorType { Name = "Pressure", Unit = "hPa", MinValid = 300, MaxValid = 1100 };
        var co2 = new SensorType { Name = "CO2", Unit = "ppm", MinValid = 0, MaxValid = 5000 };
        db.SensorTypes.AddRange(temperature, humidity, pressure, co2);

        var now = clock.GetUtcNow().UtcDateTime;
        db.Sensors.AddRange(
            new Sensor { Code = "TEMP-01", Name = "Температура, лабораторія 101", Location = "Корпус 1, ауд. 101", SensorType = temperature, CreatedAtUtc = now },
            new Sensor { Code = "HUM-01", Name = "Вологість, лабораторія 101", Location = "Корпус 1, ауд. 101", SensorType = humidity, CreatedAtUtc = now },
            new Sensor { Code = "PRES-01", Name = "Атмосферний тиск, дах", Location = "Корпус 1, дах", SensorType = pressure, CreatedAtUtc = now },
            new Sensor { Code = "CO2-01", Name = "CO2, читальна зала", Location = "Корпус 2, бібліотека", SensorType = co2, CreatedAtUtc = now });

        db.SaveChanges();
    }
}