using Amazon.CodeCommit;
using Amazon.CodeCommit.Model;
using Amazon.Runtime.CredentialManagement;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using CodeCommitHelper.Contracts.Services;
using CodeCommitHelper.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CodeCommitHelper.Views;

public sealed partial class SquashMergePage : Page
{
    private string? _selectedRepository;
    private readonly AmazonCodeCommitClient _client;
    private PullRequest? _selectedPullRequest;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IAppNotificationService _appNotificationService;

    public SquashMergeViewModel ViewModel
    {
        get;
    }

    public SquashMergePage()
    {
        _localSettingsService = App.GetService<ILocalSettingsService>();
        _client = new AmazonCodeCommitClient();
        ViewModel = App.GetService<SquashMergeViewModel>();
        _appNotificationService = App.GetService<IAppNotificationService>();
        InitializeComponent();

        RepositorySelector.PlaceholderText = "Loading pull requests...";
        RepositorySelector.IsEnabled = false;
        PullRequestSelector.IsEnabled = false;
        MergeButton.IsEnabled = false;
        CloseButton.IsEnabled = false;
        _ = LoadRepositoriesAsync();
        _ = LoadAuthorAndEmail();
    }

    private void PullRequest_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedPullRequest = PullRequestSelector.SelectedItem as PullRequest;

        CheckIfOkToMergeOrClose();
    }

    private async Task LoadAuthorAndEmail()
    {
        var author = await _localSettingsService.ReadSettingAsync<string>("Author");
        var email = await _localSettingsService.ReadSettingAsync<string>("Email");

        AuthorInput.Text = author ?? string.Empty;
        EmailInput.Text = email ?? string.Empty;
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

            var lastSelectedRepository = await _localSettingsService.ReadSettingAsync<string>("LastSelectedRepository");

            if (lastSelectedRepository != null)
            {
                RepositorySelector.SelectedItem = lastSelectedRepository;
            }
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
        CheckIfOkToMergeOrClose();

        try
        {
            _selectedRepository = RepositorySelector.SelectedItem as string;

            PullRequestSelector.PlaceholderText = "Select a pull request";

            await LoadPullRequestsAsync();

            await _localSettingsService.SaveSettingAsync("LastSelectedRepository", _selectedRepository);
        }
        catch
        {
        }
    }

    private async void MergeButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            MergeButton.IsEnabled = false;
            MergeButton.Content = "Merging...";
            CloseButton.IsEnabled = false;
            OnMergingOrClosing.Visibility = Visibility.Visible;

            var author = AuthorInput.Text.Trim();
            var email = EmailInput.Text.Trim();
            var message = MessageInput.Text.Trim();

            await _localSettingsService.SaveSettingAsync("Author", author);

            await _localSettingsService.SaveSettingAsync("Email", email);

            var mergeRequest = new MergePullRequestBySquashRequest()
            {
                PullRequestId = _selectedPullRequest!.PullRequestId,
                AuthorName = author,
                Email = email,
                RepositoryName = _selectedRepository,
                CommitMessage = message
            };

            await _client.MergePullRequestBySquashAsync(mergeRequest);
        }
        catch (Exception ex)
        {
            _appNotificationService.Show(ex.Message);
        }
        finally
        {
            MergeButton.IsEnabled = true;
            MergeButton.Content = "Merge";
            CloseButton.IsEnabled = true;
            OnMergingOrClosing.Visibility = Visibility.Collapsed;
        }
    }

    private async void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            MergeButton.IsEnabled = false;
            CloseButton.IsEnabled = false;
            CloseButton.Content = "Closing...";
            OnMergingOrClosing.Visibility = Visibility.Visible;

            var closeRequest = new UpdatePullRequestStatusRequest()
            {
                PullRequestId = _selectedPullRequest!.PullRequestId,
                PullRequestStatus = PullRequestStatusEnum.CLOSED
            };

            await _client.UpdatePullRequestStatusAsync(closeRequest);
        }
        catch (Exception ex)
        {
            _appNotificationService.Show(ex.Message);
        }
        finally
        {
            MergeButton.IsEnabled = true;
            CloseButton.IsEnabled = true;
            CloseButton.Content = "Close";
            OnMergingOrClosing.Visibility = Visibility.Collapsed;
        }
    }

    private void AuthorInput_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        CheckIfOkToMergeOrClose();
    }

    private void EmailInput_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        CheckIfOkToMergeOrClose();
    }

    private void MessageInput_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        CheckIfOkToMergeOrClose();
    }

    private void CheckIfOkToMergeOrClose()
    {
        var okToMerge = true;
        var okToClose = true;

        if (string.IsNullOrWhiteSpace(AuthorInput.Text))
        {
            okToMerge = false;
        }

        if (string.IsNullOrWhiteSpace(EmailInput.Text))
        {
            okToMerge = false;
        }

        if (string.IsNullOrWhiteSpace(MessageInput.Text))
        {
            okToMerge = false;
        }

        if (RepositorySelector.SelectedIndex == -1)
        {
            okToClose = false;
            okToMerge = false;
        }

        if (PullRequestSelector.SelectedIndex == -1)
        {
            okToClose = false;
            okToMerge = false;
        }

        MergeButton.IsEnabled = okToMerge;

        CloseButton.IsEnabled = okToClose;
    }
}
