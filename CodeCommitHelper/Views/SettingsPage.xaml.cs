using Amazon;
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

        var sharedFile = new SharedCredentialsFile();
        CredentialProfile profile;

        if (sharedFile.TryGetProfile("default", out profile))
        {
            AwsAccessKeyId.Password = profile.Options.AccessKey;
            AwsSecretAccessKey.Password = profile.Options.SecretKey;
        }
    }

    private void SaveCredentialButton_Click(object sender, RoutedEventArgs e)
    {
        var options = new CredentialProfileOptions
        {
            AccessKey = AwsAccessKeyId.Password,
            SecretKey = AwsSecretAccessKey.Password
        };

        var profile = new CredentialProfile("default", options)
        {
            Region = RegionEndpoint.APSoutheast2
        };

        var sharedFile = new SharedCredentialsFile();

        sharedFile.RegisterProfile(profile);
    }
}
