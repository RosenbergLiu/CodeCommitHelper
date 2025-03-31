using Amazon.CodeCommit;
using Amazon.CodeCommit.Model;
using CodeCommitHelper.Core.Contracts.Services;
using CodeCommitHelper.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace CodeCommitHelper.Views;

public sealed partial class CreatePullRequestPage : Page
{
    private readonly IFileService _fileService;

    public CreatePullRequestViewModel ViewModel
    {
        get;
    }

    public CreatePullRequestPage()
    {
        _fileService = App.GetService<IFileService>();
        ViewModel = App.GetService<CreatePullRequestViewModel>();
        InitializeComponent();

        RepositoryInput.Text = _fileService.ReadAsString("settings", "last-repository.txt");
    }

    private async void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            CreateButton.IsEnabled = false;
            RepositoryInput.IsEnabled = false;
            BranchNameInput.IsEnabled = false;
            TitleInput.IsEnabled = false;
            CreateButton.Content = "Creating pull request";
            OnCreating.Visibility = Visibility.Visible;

            var repositoryName = RepositoryInput.Text.Trim();
            var branchName = BranchNameInput.Text.Trim();
            var title = TitleInput.Text.Trim();

            _fileService.SaveAsString("settings", "last-repository.txt", repositoryName);

            var client = new AmazonCodeCommitClient();

            var request = new CreatePullRequestRequest()
            {
                Title = title,
                Targets =
                [
                    new Target()
                    {
                        RepositoryName = repositoryName,
                        SourceReference = branchName
                    }
                ],
                ClientRequestToken = Guid.NewGuid().ToString()
            };

            var response = await client.CreatePullRequestAsync(request);

            OutputText.Text =
                $"https://ap-southeast-2.console.aws.amazon.com/codesuite/codecommit/repositories/{repositoryName}/pull-requests/{response.PullRequest.PullRequestId}/details?region=ap-southeast-2";
        }
        catch (Exception ex)
        {
            OutputText.Text = ex.GetType().ToString() + ":\n" + ex.Message;
        }
        finally
        {
            CreateButton.IsEnabled = true;
            RepositoryInput.IsEnabled = true;
            BranchNameInput.IsEnabled = true;
            TitleInput.IsEnabled = true;
            CreateButton.Content = "Create pull request";
            OnCreating.Visibility = Visibility.Collapsed;
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        BranchNameInput.Text = string.Empty;
        TitleInput.Text = string.Empty;
        OutputText.Text = string.Empty;
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        var package = new DataPackage();
        package.SetText(OutputText.Text);
        Clipboard.SetContent(package);
    }
}
