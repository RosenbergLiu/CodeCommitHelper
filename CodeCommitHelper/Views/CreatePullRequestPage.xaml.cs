﻿using Amazon.CodeCommit;
using Amazon.CodeCommit.Model;
using CodeCommitHelper.Contracts.Services;
using CodeCommitHelper.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace CodeCommitHelper.Views;

public sealed partial class CreatePullRequestPage : Page
{
    private readonly ILocalSettingsService _localSettingsService;
    private readonly AmazonCodeCommitClient _client;

    public CreatePullRequestViewModel ViewModel
    {
        get;
    }

    public CreatePullRequestPage()
    {
        _client = new AmazonCodeCommitClient();
        _localSettingsService = App.GetService<ILocalSettingsService>();
        ViewModel = App.GetService<CreatePullRequestViewModel>();
        InitializeComponent();
        DestinationBranchSelector.IsEnabled = false;
        DestinationBranchSelector.PlaceholderText = "Loading branches...";

        _ = LoadRepositoriesAsync();
    }

    private async void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            CreateButton.IsEnabled = false;
            RepositorySelector.IsEnabled = false;
            DestinationBranchSelector.IsEnabled = false;
            BranchNameInput.IsEnabled = false;
            TitleInput.IsEnabled = false;
            CreateButton.Content = "Creating pull request";
            OnCreating.Visibility = Visibility.Visible;

            var repositoryName = RepositorySelector.SelectedItem as string;
            var destinationBranch = DestinationBranchSelector.SelectedItem as string;
            var branchName = BranchNameInput.Text.Trim();
            var title = TitleInput.Text.Trim();

            await _localSettingsService.SaveSettingAsync("LastSelectedRepository", repositoryName);
            await _localSettingsService.SaveSettingAsync("LastSelectedDestinationBranch", destinationBranch);

            var request = new CreatePullRequestRequest()
            {
                Title = title,
                Targets =
                [
                    new Target()
                    {
                        RepositoryName = repositoryName,
                        SourceReference = branchName,
                        DestinationReference = destinationBranch
                    }
                ],
                ClientRequestToken = Guid.NewGuid().ToString(),
                Description = DescriptionInput.Text
            };

            var response = await _client.CreatePullRequestAsync(request);

            OutputText.Text =
                $"https://ap-southeast-2.console.aws.amazon.com/codesuite/codecommit/repositories/{repositoryName}/pull-requests/{response.PullRequest.PullRequestId}/details?region=ap-southeast-2";
        }
        catch (Exception ex)
        {
            await App.MainWindow.ShowMessageDialogAsync(ex.Message, ex.GetType().ToString());
        }
        finally
        {
            CreateButton.IsEnabled = true;
            RepositorySelector.IsEnabled = true;
            DestinationBranchSelector.IsEnabled = true;
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
        DescriptionInput.Text = string.Empty;
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        var package = new DataPackage();
        package.SetText(OutputText.Text);
        Clipboard.SetContent(package);
    }

    private void CheckIfOkToCreate()
    {
        var okToCreate = RepositorySelector.SelectedIndex != -1 && DestinationBranchSelector.SelectedIndex != -1 &&
                         !string.IsNullOrWhiteSpace(BranchNameInput.Text) &&
                         !string.IsNullOrWhiteSpace(TitleInput.Text);

        CreateButton.IsEnabled = okToCreate;
    }

    private async void Repository_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            await LoadDestinationBranches();
        }
        catch (Exception ex)
        {
            await App.MainWindow.ShowMessageDialogAsync(ex.Message, ex.GetType().ToString());
        }
        finally
        {
            CheckIfOkToCreate();
        }
    }

    private async Task LoadRepositoriesAsync()
    {
        var listRequest = new ListRepositoriesRequest()
        {
            NextToken = null,
            SortBy = SortByEnum.RepositoryName,
            Order = OrderEnum.Ascending
        };

        var repositories = await _client.ListRepositoriesAsync(listRequest);

        RepositorySelector.ItemsSource = repositories.Repositories.Select(r => r.RepositoryName).ToList();
        RepositorySelector.IsEnabled = true;
        RepositorySelector.PlaceholderText = "Select a repository";

        var lastSelectedRepository = await _localSettingsService.ReadSettingAsync<string>("LastSelectedRepository");

        if (lastSelectedRepository != null && RepositorySelector.Items.Contains(lastSelectedRepository))
        {
            RepositorySelector.SelectedItem = lastSelectedRepository;
        }
    }

    private async Task LoadDestinationBranches()
    {
        var listRequest = new ListBranchesRequest()
        {
            RepositoryName = RepositorySelector.SelectedItem as string
        };

        var response = await _client.ListBranchesAsync(listRequest);

        var destinationBranchesList = response.Branches
            .Select(b => b.Replace("refs/heads/", string.Empty))
            .OrderBy(b => b)
            .ToList();

        DestinationBranchSelector.ItemsSource = destinationBranchesList;

        var lastSelectedDestinationBranch = await _localSettingsService.ReadSettingAsync<string>("LastSelectedDestinationBranch");

        if (lastSelectedDestinationBranch != null &&
            destinationBranchesList.Contains(lastSelectedDestinationBranch))
        {
            DestinationBranchSelector.SelectedItem = lastSelectedDestinationBranch;
        }

        DestinationBranchSelector.IsEnabled = true;
        DestinationBranchSelector.PlaceholderText = "Select a destination branch";
    }

    private void BranchNameInput_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        TitleInput.Text = BranchNameInput.Text.Replace("-", " ");

        CheckIfOkToCreate();
    }

    private void TitleInput_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        CheckIfOkToCreate();
    }

    private void DestinationBranch_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        CheckIfOkToCreate();
    }
}
