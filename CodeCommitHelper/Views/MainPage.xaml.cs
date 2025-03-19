using Amazon.CodeCommit;
using Amazon.CodeCommit.Model;
using CodeCommitHelper.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CodeCommitHelper.Views;

public sealed partial class MainPage : Page
{
    public MainViewModel ViewModel
    {
        get;
    }

    public MainPage()
    {
        ViewModel = App.GetService<MainViewModel>();
        InitializeComponent();
    }

    private async void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var repositoryName = RepositoryInput.Text.Trim();
            var branchName = BranchNameInput.Text.Trim();
            var title = TitleInput.Text.Trim();

            var client = new AmazonCodeCommitClient();

            var request = new CreatePullRequestRequest()
            {
                Title = title,
                Targets = [new Target()
                {
                    RepositoryName = repositoryName,
                    SourceReference = branchName,
                    DestinationReference = "dev"
                }]
            };
        }
        catch
        {

        }
    }
}
