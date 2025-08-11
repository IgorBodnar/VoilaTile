using CommunityToolkit.Mvvm.ComponentModel;

public abstract partial class DialogViewModelBase : ObservableObject
{
    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private bool showPositiveResponse = true;

    [ObservableProperty]
    private bool showNegativeResponse = true;

    [ObservableProperty]
    private string positiveResponseText = "OK";

    [ObservableProperty]
    private string negativeResponseText = "Cancel";
}

