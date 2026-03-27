# System Patterns: GenericQueue

## Architecture
- **Client**: WPF Application (.NET Framework 4.6.1).
- **Backend/Logic**: SQL Server Stored Procedures (`dbo.q_Load_List`, `dbo.q_Load_Details`, etc.).
- **Data Access**: Manual `SqlConnection` and `SqlDataAdapter` usage, with some Entity Framework (referenced in `App.config`).
- **Dynamic UI**: UI is generated at runtime based on XML serialized into `FieldCollection` and `EnumCollection` objects.

## Key Technical Decisions
- **Stored Procedure Driven**: Business logic and data retrieval are concentrated in the database layer.
- **XML for Metadata**: Flexible data modeling is achieved by storing XML representations of form fields in the database.
- **Dynamic Grid Generation**: The WPF `DataGrid` is populated dynamically using `DataTable` and custom template columns.

## Component Relationships
- `MainWindow`: Orchestrates the main UI, coordinates with SQL Server.
- `Field`: Data model for a single UI input/display field.
- `Enum`: Data model for lookup values (dropdowns).
- `ExButton`: Custom control for grid actions.
