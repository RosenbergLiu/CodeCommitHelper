using Amazon.CodeCommit;
using Amazon.CodeCommit.Model;
using Amazon.Runtime.CredentialManagement;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using CodeCommitHelper.ViewModels;

using Microsoft.UI.Xaml.Controls;
using Windows.Media.Protection.PlayReady;
using Microsoft.UI.Xaml;

namespace CodeCommitHelper.Views;

public sealed partial class SquashMergePage : Page
{
    private string? _selectedRepository;
    private readonly AmazonCodeCommitClient _client;
    private PullRequest? _selectedPullRequest;

    public SquashMergeViewModel ViewModel
    {
        get;
    }

    public SquashMergePage()
    {
        _client = new AmazonCodeCommitClient();
        ViewModel = App.GetService<SquashMergeViewModel>();
        InitializeComponent();

        RepositorySelector.PlaceholderText = "Loading pull requests...";
        RepositorySelector.IsEnabled = false;
        PullRequestSelector.IsEnabled = false;
        MergeButton.IsEnabled = false;
        CloseButton.IsEnabled = false;
        _ = LoadRepositoriesAsync();
    }

    private void PullRequest_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedPullRequest = PullRequestSelector.SelectedItem as PullRequest;

        MergeButton.IsEnabled = true;
        CloseButton.IsEnabled = true;
    }

    private async Task LoadRepositoriesAsync()
    {
        try
        {
            string? nextToken = null;

            var listRequest = new ListRepositoriesRequest()
            {
                NextToken = nextToken,
                SortBy = SortByEnum.RepositoryName,
                Order = OrderEnum.Ascending
            };

            var repositories = await _client.ListRepositoriesAsync(listRequest);

            RepositorySelector.ItemsSource = repositories.Repositories.Select(r => r.RepositoryName).ToList();
            RepositorySelector.IsEnabled = true;
            RepositorySelector.PlaceholderText = "Select a repository";
        }
        catch
        {
            
        }
    }

    private async Task LoadPullRequestsAsync()
    {
        try
        {
            var sharedFile = new SharedCredentialsFile();

            if (!sharedFile.TryGetProfile("default", out var profile))
            {

            }
            var stsClient = new AmazonSecurityTokenServiceClient();
            var identityResponse = await stsClient.GetCallerIdentityAsync(new GetCallerIdentityRequest());

            var listRequest = new ListPullRequestsRequest()
            {
                RepositoryName = _selectedRepository,
                PullRequestStatus = PullRequestStatusEnum.OPEN,
                AuthorArn = identityResponse.Arn
            };

            var pullRequests = await _client.ListPullRequestsAsync(listRequest);

            var getPullRequestDetailTasks = new List<Task<GetPullRequestResponse>>();

            foreach (var pullRequestId in pullRequests.PullRequestIds)
            {
                var getRequest = new GetPullRequestRequest()
                {
                    PullRequestId = pullRequestId
                };

                getPullRequestDetailTasks.Add(_client.GetPullRequestAsync(getRequest));
            }

            var getResponses = await Task.WhenAll(getPullRequestDetailTasks);

            PullRequestSelector.ItemsSource = getResponses.Select(r => r.PullRequest).ToList();
            PullRequestSelector.DisplayMemberPath = "Title";
            PullRequestSelector.PlaceholderText = "Select a pull request";
            PullRequestSelector.IsEnabled = true;
        }
        catch
        {

        }
    }

    private async void Repository_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            _selectedRepository = RepositorySelector.SelectedItem as string;

            PullRequestSelector.IsEnabled = true;
            PullRequestSelector.PlaceholderText = "Select a pull request";
            await LoadPullRequestsAsync();
        }
        catch (Exception e)
        {
        }
    }

    private async void MergeButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var mergeRequest = new MergePullRequestBySquashRequest()
            {
                PullRequestId = _selectedPullRequest!.PullRequestId
            };

            await _client.MergePullRequestBySquashAsync(mergeRequest);
        }
        catch (Exception e)
        {
        }
        finally
        {
            MergeButton.IsEnabled = false;
            CloseButton.IsEnabled = false;
        }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        throw new NotImplementedException();
    }
}
