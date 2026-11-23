# Цілісність і безпека роботи з даними (C5)

Схема описана в `src/SignalClean.Data/SignalCleanDbContext.cs`. Подивитися реальну схему можна у файлі `signalclean.db` через DB Browser for SQLite або розширення SQLite для VS Code.

```
SensorTypes (Id PK, Name UNIQUE, Unit, MinValid, MaxValid, CHECK MinValid < MaxValid)
    1 ──< Sensors (Id PK, Code UNIQUE, Name, Location, SensorTypeId FK → SensorTypes ON DELETE RESTRICT, CreatedAtUtc)
              1 ──< Readings  (Id PK, SensorId FK → Sensors ON DELETE CASCADE, TimestampUtc, RawValue, CleanValue, Status CHECK IN ('Valid','Corrected','Invalid','Missing'))
              1 ──< Anomalies (Id PK, SensorId FK → Sensors ON DELETE CASCADE, TimestampUtc, Kind, Value, Details)
```

| Критерій | Чи виконано | Підтвердження |
|---|---|---|
| Нормалізація ≥3НФ | так | Одиниці виміру та фізичний діапазон залежать від типу, а не від конкретного датчика. Тому вони винесені в довідник `SensorTypes`, і транзитивної залежності `Sensor → Type → Unit` у таблиці `Sensors` немає. Атрибути атомарні, неключові атрибути залежать лише від PK. |
| PK/FK та обмеження цілісності | так | PK у всіх таблицях; FK `Sensors.SensorTypeId` (RESTRICT: не можна видалити тип, поки є датчики), `Readings.SensorId` і `Anomalies.SensorId` (CASCADE); `UNIQUE(Code)`, `UNIQUE(Name)`; `CHECK (MinValid < MaxValid)`; `CHECK (Status IN ('Valid','Corrected','Invalid','Missing'))` на `Readings` (`CK_Readings_Status`); `NOT NULL` та обмеження довжини рядків. |
| Індекси на полях пошуку/JOIN | так | `IX_Readings_SensorId_TimestampUtc` та `IX_Anomalies_SensorId_TimestampUtc` під запит «останні N записів датчика»; унікальні індекси на `Sensors.Code` і `SensorTypes.Name`. |
| Параметризовані запити | так | Усі запити побудовані через LINQ/EF Core, який завжди передає значення як параметри SQL. Рядки SQL вручну ніде не склеюються. |
| Транзакції | так | `IngestService.IngestAsync`: увесь пакет (вимірювання + аномалії) записується в одній транзакції. |
| Захист запису | так | Ендпоінти запису вимагають `X-Api-Key`; ключ порівнюється за сталий час (`CryptographicOperations.FixedTimeEquals`). |

Для звіту можна додати скріншот схеми з DB Browser for SQLite і вивід `PRAGMA index_list('Readings');`.