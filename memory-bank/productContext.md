# Product Context: GenericQueue

## Why it Exists
GenericQueue provides a flexible way to manage various tasks or workflows (called "Types") through a unified interface. Instead of hardcoding forms for every workflow, the application dynamicly generates the UI based on XML definitions stored in the database.

## Problems it Solves
- **Complexity**: Managing diverse data entry tasks within a single tool.
- **Flexibility**: Allowing new queue types to be added via database configuration without recompiling the application.
- **Workflow Management**: Supporting basic state transitions (buttons) and task-specific metadata (Fields).

## User Experience Goals
- **Simplicity**: Users should see only the relevant queues and fields for their current task.
- **Efficiency**: Quick loading of lists and details, with logical data entry (dropdowns, dates, etc.).
- **Responsiveness**: Immediate feedback when switching between different queue types or selecting items.
