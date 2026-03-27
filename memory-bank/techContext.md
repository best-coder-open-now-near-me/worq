# Tech Context: GenericQueue

## Technologies Used
- **Frontend (Legacy)**: WPF, XAML, C# (.NET Framework 4.6.1).
- **Database**: SQL Server.
- **Data Interchange**: XML (System.Xml.Serialization).
- **Libraries**:
    - `CsvHelper`: For CSV processing.
    - `ExcelDataReader`: For reading Excel files.
    - `Entity Framework 6`: Found in configuration, though direct SQL client is also used.

## Development Setup
- Visual Studio (Solution file `GenericQueue.sln`).
- SQL Server instance (Connection defined in `db_config.xml`).

## Technical Constraints
- Legacy .NET Framework (not .NET Core/5+).
- Heavy reliance on Windows-specific technologies (WPF).
- Synchronous data access in many parts of the UI thread.
