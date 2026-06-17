using CommunityToolkit.Mvvm.ComponentModel;

namespace Reader.ViewModels;

public partial class BaseViewModel : ObservableObject
{
    [ObservableProperty] bool busy;
}
