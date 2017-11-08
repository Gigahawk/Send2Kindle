using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System.IO;
using System.Threading;

using System.Net.Mail;

using System.Configuration;

namespace Send2Kindle
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // If modifying these scopes, delete your previously saved credentials
        // at ~/.credentials/gmail-dotnet-quickstart.json
        static string[] Scopes = {GmailService.Scope.GmailSend};
        static string ApplicationName = "Send2Kindle";

        GmailService service;

        public MainWindow()
        {
            if (!IsValidEmail(ConfigurationManager.AppSettings["kindleAddress"]))
            {
                const string message = "Please add your Kindle's email address to App.config";
                const string caption = "Error";
                MessageBox.Show(message, caption);
                System.Windows.Application.Current.Shutdown();
            }

            UserCredential credential;

            using (var stream =
                new FileStream("client_secret.json", FileMode.Open, FileAccess.Read))
            {
                string credPath = System.Environment.GetFolderPath(
                    System.Environment.SpecialFolder.Personal);
                credPath = System.IO.Path.Combine(credPath, ".credentials/Send2Kindle.json");

                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true)).Result;
                Console.WriteLine("Credential file saved to: " + credPath);
            }

            // Create Gmail API service.
            service = new GmailService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });

            InitializeComponent();
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

            dlg.DefaultExt = ".pdf";
            dlg.Filter = "PDF Documents (.pdf)|*.pdf";

            Nullable<bool> result = dlg.ShowDialog();

            if (result == true)
            {
                this.filePath.Text = dlg.FileName;
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                this.filePath.Text = files[0];
            }
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            var msg = new MailMessage();

            msg.To.Add(new MailAddress(ConfigurationManager.AppSettings["kindleAddress"]));
            msg.Attachments.Add(new Attachment(this.filePath.Text));

            var mimeMsg = MimeKit.MimeMessage.CreateFromMailMessage(msg);

            var sendMsg= new Message();
            sendMsg.Raw = Base64UrlEncode(mimeMsg.ToString());

            Console.WriteLine("Sending id: " + sendMsg.Id);
            var outMsg = service.Users.Messages.Send(sendMsg,"me").Execute();
            Console.WriteLine("Sent id: " + outMsg.Id);
        }

        private string Base64UrlEncode(string input)
        {
            var inputBytes = System.Text.Encoding.UTF8.GetBytes(input);
            return Convert.ToBase64String(inputBytes)
              .Replace('+', '-')
              .Replace('/', '_')
              .Replace("=", "");
        }

        private void filePath_TextChanged(object sender, TextChangedEventArgs e)
        {
            bool isPath = System.IO.File.Exists(this.filePath.Text);

            if (isPath)
                this.SendButton.IsEnabled = true;
            else
                this.SendButton.IsEnabled = false;
        }

        private void filePath_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }


    }
}
