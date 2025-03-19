using CodeCommitHelper.ViewModels;

using Microsoft.UI.Xaml.Controls;

namespace CodeCommitHelper.Views;

public sealed partial class SquashMergePage : Page
{
    public SquashMergeViewModel ViewModel
    {
        get;
    }

    public SquashMergePage()
    {
        ViewModel = App.GetService<SquashMergeViewModel>();
        InitializeComponent();
    }
}
