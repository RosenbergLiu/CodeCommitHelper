using Amazon.CodeCommit;
using Amazon.CodeCommit.Model;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using CodeCommitHelper.Contracts.Services;
using CodeCommitHelper.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;

namespace CodeCommitHelper.Views;

public sealed partial class SquashMergePage : Page
{
    private string? _selectedRepository;
    private readonly AmazonCodeCommitClient _codeCommitClient;
    private PullRequest? _selectedPullRequest;
    private readonly ILocalSettingsService _localSettingsService;
    private List<Approval> _selectedPullRequestApprovals;

    public SquashMergeViewModel ViewModel
    {
        get;
    }

    public SquashMergePage()
    {
        _localSettingsService = App.GetService<ILocalSettingsService>();
        _codeCommitClient = new AmazonCodeCommitClient();
        ViewModel = App.GetService<SquashMergeViewModel>();
        _selectedPullRequestApprovals = new List<Approval>();
        InitializeComponent();

        RepositorySelector.PlaceholderText = "Loading pull requests...";
        RepositorySelector.IsEnabled = false;
        PullRequestSelector.IsEnabled = false;
        MergeButton.IsEnabled = false;
        CloseButton.IsEnabled = false;
        DeleteBranch.IsChecked = true;
        MergeButton.IsEnabled = false;
        _ = LoadRepositoriesAsync();
        _ = LoadAuthorAndEmail();
    }

    private async void PullRequest_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selectedPullRequest = PullRequestSelector.SelectedItem as PullRequest;

        _selectedPullRequest = selectedPullRequest;

        MessageInput.Text = (PullRequestSelector.SelectedItem as PullRequest)?.Title.Replace("-", " ") ?? string.Empty;

        var getApprovalStateRequest = new GetPullRequestApprovalStatesRequest()
        {
            PullRequestId = selectedPullRequest!.PullRequestId,
            RevisionId = selectedPullRequest.RevisionId
        };

        var approvalStateResponse = await _codeCommitClient.GetPullRequestApprovalStatesAsync(getApprovalStateRequest);

        _selectedPullRequestApprovals = approvalStateResponse.Approvals;

        var approvalStateText = string.Join("; ", approvalStateResponse.Approvals.Select(a => a.ApprovalState));

        var branchText = string.Join("; ", selectedPullRequest.PullRequestTargets.Select(t => t.SourceReference.Split("/").Last()));
        var destinationText = string.Join("; ",
            selectedPullRequest.PullRequestTargets.Select(t => t.DestinationReference.Split("/").Last()));

        PullRequestDetail.Text = $"Pull request Id: {selectedPullRequest!.PullRequestId}\nDestination: {destinationText}\nBranch: {branchText}\nApproval state: {approvalStateText}";

        PullRequestLink.Inlines.Clear();

        var selectedRepository = RepositorySelector.SelectedItem as string;

        PullRequestLink.Inlines.Add(new Run()
        {
            Text = $"https://ap-southeast-2.console.aws.amazon.com/codesuite/codecommit/repositories/{selectedRepository}/pull-requests/{selectedPullRequest!.PullRequestId}/changes?region=ap-southeast-2"
        });

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
        var listRequest = new ListRepositoriesRequest()
        {
            NextToken = null,
            SortBy = SortByEnum.RepositoryName,
            Order = OrderEnum.Ascending
        };

        var repositories = await _codeCommitClient.ListRepositoriesAsync(listRequest);

        RepositorySelector.ItemsSource = repositories.Repositories.Select(r => r.RepositoryName).ToList();
        RepositorySelector.IsEnabled = true;
        RepositorySelector.PlaceholderText = "Select a repository";

        var lastSelectedRepository = await _localSettingsService.ReadSettingAsync<string>("LastSelectedRepository");

        if (lastSelectedRepository != null)
        {
            RepositorySelector.SelectedItem = lastSelectedRepository;
        }
    }

    private async Task LoadPullRequestsAsync()
    {
        try
        {
            PullRequestSelector.PlaceholderText = "Loading pull requests...";

            var stsClient = new AmazonSecurityTokenServiceClient();
            var identityResponse = await stsClient.GetCallerIdentityAsync(new GetCallerIdentityRequest());

            var listRequest = new ListPullRequestsRequest()
            {
                RepositoryName = _selectedRepository,
                PullRequestStatus = PullRequestStatusEnum.OPEN,
                AuthorArn = identityResponse.Arn
            };

            var pullRequests = await _codeCommitClient.ListPullRequestsAsync(listRequest);

            var getPullRequestDetailTasks = new List<Task<GetPullRequestResponse>>();

            foreach (var pullRequestId in pullRequests.PullRequestIds)
            {
                var getRequest = new GetPullRequestRequest()
                {
                    PullRequestId = pullRequestId
                };

                getPullRequestDetailTasks.Add(_codeCommitClient.GetPullRequestAsync(getRequest));
            }

            var getResponses = await Task.WhenAll(getPullRequestDetailTasks);

            PullRequestSelector.ItemsSource = getResponses.Select(r => r.PullRequest).ToList();
            PullRequestSelector.DisplayMemberPath = "Title";
            PullRequestSelector.PlaceholderText = "Select a pull request";
            PullRequestSelector.IsEnabled = true;
        }
        catch (Exception ex)
        {
            await App.MainWindow.ShowMessageDialogAsync(ex.Message, ex.GetType().ToString());
        }
    }

    private async void Repository_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            CheckIfOkToMergeOrClose();

            _selectedRepository = RepositorySelector.SelectedItem as string;

            PullRequestSelector.PlaceholderText = "Select a pull request";

            await LoadPullRequestsAsync();

            await _localSettingsService.SaveSettingAsync("LastSelectedRepository", _selectedRepository);
        }
        catch (Exception ex)
        {
            await App.MainWindow.ShowMessageDialogAsync(ex.Message, ex.GetType().ToString());
        }
    }

    private async void MergeButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            MergeButton.IsEnabled = false;
            MergeButton.Content = "Merging";
            CloseButton.IsEnabled = false;
            OnMergingOrClosing.Visibility = Visibility.Visible;

            var author = AuthorInput.Text.Trim();
            var email = EmailInput.Text.Trim();
            var message = MessageInput.Text.Trim();

            await _localSettingsService.SaveSettingAsync("Author", author);

            await _localSettingsService.SaveSettingAsync("Email", email);

            if (_selectedPullRequest == null)
            {
                return;
            }

            var selectedPullRequest = _selectedPullRequest;

            var mergeRequest = new MergePullRequestBySquashRequest()
            {
                PullRequestId = _selectedPullRequest!.PullRequestId,
                AuthorName = author,
                Email = email,
                RepositoryName = _selectedRepository,
                CommitMessage = message
            };

            await _codeCommitClient.MergePullRequestBySquashAsync(mergeRequest);

            if (DeleteBranch.IsChecked is true)
            {
                var deleteBranchRequests = selectedPullRequest.PullRequestTargets.Select(p => new DeleteBranchRequest()
                {
                    BranchName = p.SourceReference.Split("/").Last(),
                    RepositoryName = _selectedRepository
                });

                var deleteBranchRequestTasks = deleteBranchRequests.Select(r => _codeCommitClient.DeleteBranchAsync(r)).ToList();

                await Task.WhenAll(deleteBranchRequestTasks);
            }

            await LoadPullRequestsAsync();

            PullRequestLink.Inlines.Clear();
            PullRequestDetail.Text = string.Empty;
        }
        catch (Exception ex)
        {
            await App.MainWindow.ShowMessageDialogAsync(ex.Message, ex.GetType().ToString());
        }
        finally
        {
            MergeButton.IsEnabled = true;
            MergeButton.Content = "Merge pull request";
            CloseButton.IsEnabled = true;
            OnMergingOrClosing.Visibility = Visibility.Collapsed;
            PullRequestSelector.SelectedIndex = -1;
            MessageInput.Text = string.Empty;
        }
    }

    private async void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            MergeButton.IsEnabled = false;
            CloseButton.IsEnabled = false;
            CloseButton.Content = "Closing";
            OnMergingOrClosing.Visibility = Visibility.Visible;

            var closeRequest = new UpdatePullRequestStatusRequest()
            {
                PullRequestId = _selectedPullRequest!.PullRequestId,
                PullRequestStatus = PullRequestStatusEnum.CLOSED
            };

            await _codeCommitClient.UpdatePullRequestStatusAsync(closeRequest);

            await LoadPullRequestsAsync();
        }
        catch (Exception ex)
        {
            await App.MainWindow.ShowMessageDialogAsync(ex.Message, ex.GetType().ToString());
        }
        finally
        {
            MergeButton.IsEnabled = true;
            CloseButton.IsEnabled = true;
            CloseButton.Content = "Close pull request";
            OnMergingOrClosing.Visibility = Visibility.Collapsed;
            PullRequestSelector.SelectedIndex = -1;
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
        DeleteBranch.IsEnabled = okToMerge;

        CloseButton.IsEnabled = okToClose;

    }

    private void PullRequestLink_Clicked(Hyperlink sender, HyperlinkClickEventArgs args)
    {
        var uri =
            $"https://ap-southeast-2.console.aws.amazon.com/codesuite/codecommit/repositories/{_selectedRepository}/pull-requests/{_selectedPullRequest!.PullRequestId}/changes?region=ap-southeast-2";
        _ = Windows.System.Launcher.LaunchUriAsync(new Uri(uri));
    }
}
