using Amazon.CodeCommit;
using Amazon.CodeCommit.Model;
using CodeCommitHelper.ViewModels;
using Microsoft.UI.Xaml;
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

    private async void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            CreateButton.IsEnabled = false;

            var repositoryName = RepositoryInput.Text.Trim();
            var branchName = BranchNameInput.Text.Trim();
            var title = TitleInput.Text.Trim();

            var client = new AmazonCodeCommitClient();

            var request = new CreatePullRequestRequest()
            {
                Title = title,
                Targets =
                [
                    new Target()
                    {
                        RepositoryName = repositoryName,
                        SourceReference = branchName,
                        DestinationReference = "dev"
                    }
                ],
                ClientRequestToken = Guid.NewGuid().ToString()
            };


            var response = await client.CreatePullRequestAsync(request);

            OutputText.Text =
                $"https://ap-southeast-2.console.aws.amazon.com/codesuite/codecommit/repositories/myprosperity-v2/pull-requests/{response.PullRequest.PullRequestId}/details?region=ap-southeast-2";
        }
        catch (Exception ex)
        {
            OutputText.Text = ex.Message;
        }
        finally
        {
            CreateButton.IsEnabled = true;
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        BranchNameInput.Text = string.Empty;
        RepositoryInput.Text = string.Empty;
        TitleInput.Text = string.Empty;
    }
}
