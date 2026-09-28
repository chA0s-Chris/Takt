# Support templates for manual entries

> Issue: [#7](https://github.com/chA0s-Chris/Takt/issues/7)

## Rationale

The widget lets users start tracking from a template or a recent task. A manual entry created in the Overview's "New entry" dialog has no such shortcut: the task name, issue key and note have to be typed by hand. That's slow and error-prone for the repeating work templates exist for.

The "New entry" dialog should offer the same templates and recent tasks as a starting point.

## Acceptance Criteria

- [x] The "New entry" dialog offers a "Start from…" list of active templates followed by recent tasks, built by the same rules as the widget's quick-switch list (archived templates excluded, duplicates by task name removed, at most eight items).
- [x] Picking a template sets the task name, issue key and note to the template's name, default issue key and default note. A value the template doesn't have clears the field.
- [x] Picking a recent task sets the task name and issue key, clearing the issue key when that task has none, and leaves the note unchanged.
- [x] Picking an item never changes the start or end date and time.
- [x] When there are no templates and no recent tasks, the dialog doesn't offer the list.
- [x] The "Edit entry" dialog doesn't offer the list.
- [x] The widget's quick-switch list behaves as before.
- [x] Automated tests cover picking a template, picking a recent task, the empty case, and that editing an existing entry offers no list.

## Technical Details

- Move the list building from `WidgetViewModel.LoadQuickSwitchItems` into a new shared App-level helper that returns `QuickSwitchItem`s from `ITemplateRepository` and `ITimeEntryRepository`, so the widget and the editor can't drift apart. The widget keeps its own extra rule of leaving out the running task; the editor doesn't apply it.
- `EntryEditorViewModel` receives the items (or the repositories) for new entries only. `OverviewViewModel` needs `ITemplateRepository` to build the editor, which changes its constructor; the DI container in `App.axaml.cs` already registers `ITemplateRepository` and resolves it automatically. Update the existing direct constructions in `OverviewViewModelTests` and `MainWindowTests` accordingly.
- In `EntryEditorDialog.axaml`, the list opens from a button beside the task name field, using the same flyout-with-list pattern as the existing Jira issue search. Picking an item goes through a command on the view model, and the flyout closes afterwards.
- The list is loaded once when the dialog opens. It doesn't need to react to template changes while the dialog is open.
