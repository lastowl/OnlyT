using System;
using Avalonia;
using Avalonia.Controls;

namespace OnlyT.Avalonia.Views;

public partial class CountdownWindow : Window
{
    public CountdownWindow()
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
}
