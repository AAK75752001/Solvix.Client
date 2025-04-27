using Solvix.Client.MVVM.ViewModels;

namespace Solvix.Client.MVVM.Views;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _viewModel;

    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
    }

    protected override bool OnBackButtonPressed()
    {
        // Show a confirmation dialog when the user presses the back button
        ShowExitConfirmationAsync();
        return true;
    }

    private async Task ShowExitConfirmationAsync()
    {
        bool answer = await DisplayAlert("Exit", "Are you sure you want to exit the app?", "Yes", "No");

        if (answer)
        {
            Application.Current.Quit();
        }
    }
}