# Maximized Window and Setup Guide Design

## Scope

- Open the main WPF window maximized by default.
- Restore the seven-step Google Cloud and OAuth tutorial inside the current Settings page expander.
- Preserve the current Settings layout, styles, bindings, and behavior.

## Design

Use native XAML only. Add `WindowState="Maximized"` to `MainWindow` and replace the shortened setup-guide sentence with the previous numbered tutorial, using wrapping text inside the existing OAuth card.

## Verification

Add XAML contract assertions for the maximized startup state and tutorial content, then run the Desktop test project and the full solution build/test suite.
