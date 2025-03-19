using CodeCommitHelper.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Amazon.Runtime.CredentialManagement;

namespace CodeCommitHelper.Views;

// TODO: Set the URL for your privacy policy by updating SettingsPage_PrivacyTermsLink.NavigateUri in Resources.resw.
public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel
    {
        get;
    }

    public SettingsPage()
    {
        ViewModel = App.GetService<SettingsViewModel>();
        InitializeComponent();
    }

    private void SaveCredentialButton_Click(object sender, RoutedEventArgs e)
    {
        var options = new CredentialProfileOptions
        {
            AccessKey = AwsAccessKeyId.Text,
            SecretKey = AwsSecretAccessKey.Text
        };

        var profile = new CredentialProfile("default", options);

        var sharedFile = new SharedCredentialsFile();

        sharedFile.RegisterProfile(profile);
    }
}
