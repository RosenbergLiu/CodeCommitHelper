using CodeCommitHelper.ViewModels;

using Microsoft.UI.Xaml.Controls;

namespace CodeCommitHelper.Views;

public sealed partial class CreatePullRequestPage : Page
{
    public CreatePullRequestViewModel ViewModel
    {
        get;
    }

    public CreatePullRequestPage()
    {
        ViewModel = App.GetService<CreatePullRequestViewModel>();
        InitializeComponent();
    }
}
