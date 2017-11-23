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
using Google.Apis.Requests;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;

using System.IO;
using System.Threading;

using System.Net.Mail;

using System.Configuration;
using System.Windows.Threading;

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

        bool quitOnSend;

        public MainWindow()
        {
            bool IsError = false;
            if (!IsValidEmail(ConfigurationManager.AppSettings["kindleAddress"]))
            {
                const string emailMessage = "Please add your Kindle's email address to Email.config";
                const string emailCaption = "Error";
                MessageBox.Show(emailMessage, emailCaption);
                IsError = true;
            }

            if (!System.IO.File.Exists("client_secret.json"))
            {
                const string authMessage = "Please add your Google account's client_secret.json file to the install directory";
                const string authCaption = "Error";
                const string authURL = "https://developers.google.com/gmail/api/quickstart/dotnet";
                MessageBox.Show(authMessage, authCaption);
                System.Diagnostics.Process.Start(authURL);
                IsError = true;
            }

            if(!Boolean.TryParse(ConfigurationManager.AppSettings["quitOnSend"], out quitOnSend)){
                const string quitMessage = "Please set quitOnSend in Email.config";
                const string quitCaption = "Error";
                MessageBox.Show(quitMessage, quitCaption);
                IsError = true;
            }

            if (IsError)
                System.Windows.Application.Current.Shutdown();

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

            if (Clipboard.ContainsText(TextDataFormat.Text))
            {
                var clipString = Clipboard.GetText();
                if (System.IO.File.Exists(clipString))
                    this.filePath.Text = clipString;
            }

            if (Clipboard.ContainsFileDropList())
            {
                var list = Clipboard.GetFileDropList();
                this.filePath.Text = list[0];
            }

            CommandManager.AddPreviewCanExecuteHandler(filePath, onPreviewCanExecute);
            CommandManager.AddPreviewExecutedHandler(filePath, onPreviewExecuted);
        }

        private void onPreviewCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            // Allow pasting anything
            if (e.Command == ApplicationCommands.Paste && (Clipboard.ContainsFileDropList() || Clipboard.ContainsText(TextDataFormat.Text) || Clipboard.ContainsImage()))
            {
                e.CanExecute = true;
                e.Handled = true;
            }
        }

        private void onPreviewExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Command == ApplicationCommands.Paste)
            {
                if (Clipboard.ContainsFileDropList())
                {
                    this.filePath.Text = Clipboard.GetFileDropList()[0];
                    e.Handled = true;
                }

                if (Clipboard.ContainsImage())
                {

                    var img = Clipboard.GetImage();

                    var tempPath = System.IO.Path.GetTempPath() + Guid.NewGuid().ToString() + ".png";

                    using (var fileStream = new FileStream(tempPath, FileMode.Create))
                    {
                        BitmapEncoder encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(img));
                        encoder.Save(fileStream);
                    }

                    this.filePath.Text = tempPath;
                }
            }
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
            dlg.Filter = "PDF Documents (*.pdf)|*.pdf|Kindle Format (*.mobi, *.azw)|*.mobi;*.azw|Microsoft Word (*.doc, *.docx)|*.doc;*.docx|HTML Document (*.html, *.htm)|*.html;*.htm|Rich Text Document (*.rtf)|*.rtf|Image File (*.jpeg, *.jpg, *.gif, *.png, *.bmp)|*.jpeg;*.jpg;*.gif;*.png;*.bmp|All Files (*.*)|*.*";

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
            this.instruction.Text = "Sending...";
            this.instruction.Foreground = Brushes.Black;
            this.filePath.IsEnabled = false;
            this.SendButton.IsEnabled = false;
            this.BrowseButton.IsEnabled = false;

            //Wait for UI to update
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => { })).Wait();

            var msg = new MailMessage();

            msg.To.Add(new MailAddress(ConfigurationManager.AppSettings["kindleAddress"]));
            msg.Body = "hello from kindlesender";
            //msg.Attachments.Add(new Attachment(this.filePath.Text));

            var mimeMsg = MimeKit.MimeMessage.CreateFromMailMessage(msg);

            var sendMsg= new Message();
            sendMsg.Raw = Base64UrlEncode(mimeMsg.ToString());

            FileStream stream = new FileStream(this.filePath.Text, FileMode.Open, FileAccess.Read);
            
            this.instruction.Text = "Uploading?...";
            //Wait for UI to update
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => { })).Wait();

            var streamMessage = service.Users.Messages.Send(sendMsg, "me", stream, "message/rfc822");
            streamMessage.Upload();

            this.instruction.Text = "Sending Traditionally?...";
            //Wait for UI to update
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => { })).Wait();

            var outmessage = service.Users.Messages.Send(sendMsg, "me").Execute();

            this.filePath.Text = "";
            this.filePath.IsEnabled = true;
            this.BrowseButton.IsEnabled = true;

            if (String.IsNullOrEmpty(outmessage.Id))
            {
                this.instruction.Text = "Error, couldn't send message";
                this.instruction.Foreground = Brushes.Red;
            } else
            {
                this.instruction.Text = "Please select a file to send";
            }

            if(quitOnSend)
                System.Windows.Application.Current.Shutdown();

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
