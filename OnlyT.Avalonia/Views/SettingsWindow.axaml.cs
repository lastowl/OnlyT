using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using OnlyT.Avalonia.ViewModels;

namespace OnlyT.Avalonia.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        Opened += OnWindowOpened;
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        // Apply native title bar theme
        if (Application.Current is App app)
        {
            app.ApplyTitleBarThemeToWindow(this);
        }
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        // Call the ViewModel's Save command
        if (DataContext is SettingsViewModel viewModel)
        {
            viewModel.SaveCommand.Execute(null);
        }

        // Close the window after saving
        Close();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
